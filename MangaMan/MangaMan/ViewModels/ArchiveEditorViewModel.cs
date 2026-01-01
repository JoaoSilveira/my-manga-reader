using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Media.Imaging;
using Avalonia.Metadata;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MangaMan.Service;

namespace MangaMan.ViewModels;

public partial class AeArchiveFileViewModel(AeArchiveFolderViewModel Folder, ArchiveFile file, IArchiveReader reader)
    : ViewModelBase
{
    public string OriginalName { get; } = file.Name;
    public string Path { get; } = file.Path;
    [ObservableProperty] private string _name = file.Name;
    [ObservableProperty] private bool _deleted;
    public bool IsImage { get; } = file.IsImage;

    public Task<Bitmap?> Thumbnail
    {
        get
        {
            field ??= GetImageAsync();
            return field;
        }
    }

    private async Task<Bitmap?> GetImageAsync()
    {
        var bytes = await reader.ReadAllBytesAsync(Path);
        return bytes is null ? null : Bitmap.DecodeToHeight(new MemoryStream(bytes), 400);
    }
}

public partial class AeArchiveJoinedFileViewModel(AeArchiveFolderViewModel Folder, string name) : ViewModelBase
{
    [ObservableProperty] private string _name = name;
    [ObservableProperty] private AeArchiveFileViewModel _left;
    [ObservableProperty] private AeArchiveFileViewModel _right;
    [ObservableProperty] private bool _deleted;

    [RelayCommand]
    private void SwapSides()
    {
        (Left, Right) = (Right, Left);
    }
}

public partial class AeArchiveFolderViewModel : ViewModelBase
{
    public string OriginalName { get; }
    [ObservableProperty] private string _name;

    [ObservableProperty] private ObservableCollection<ViewModelBase> _entries;

    public AeArchiveFolderViewModel(ArchiveFolder folder, IArchiveReader reader)
    {
        OriginalName = folder.Name;
        _name = folder.Name;
        _entries = new ObservableCollection<ViewModelBase>(
            folder.Folders.Select<ArchiveFolder, ViewModelBase>(f => new AeArchiveFolderViewModel(f, reader))
                .Concat(folder.Files.Select(f => new AeArchiveFileViewModel(this, f, reader)))
        );
    }

    public IEnumerable<ViewModelBase> EnumerateImages()
    {
        foreach (var entry in Entries)
        {
            switch (entry)
            {
                case AeArchiveFolderViewModel child:
                {
                    foreach (var item in child.EnumerateImages())
                        yield return item;
                    break;
                }
                case AeArchiveFileViewModel { IsImage: false }:
                    continue;
            }

            yield return entry;
        }
    }

    [RelayCommand]
    private void RenameAndOrderNumerically()
    {
        var newEntries = new ObservableCollection<ViewModelBase>(
            Entries
                .Order(Comparer<ViewModelBase>.Create((a, b) =>
                    (a, b) switch
                    {
                        (AeArchiveFolderViewModel, AeArchiveFileViewModel) => -1,
                        (AeArchiveFolderViewModel, AeArchiveJoinedFileViewModel) => -1,
                        (AeArchiveFileViewModel, AeArchiveFolderViewModel) => 1,
                        (AeArchiveJoinedFileViewModel, AeArchiveFolderViewModel) => 1,
                        (AeArchiveFolderViewModel va, AeArchiveFolderViewModel vb) => string.CompareOrdinal(va.Name,
                            vb.Name),
                        (AeArchiveFileViewModel va, AeArchiveFileViewModel vb) => CompareFileNameNumerically(va.Name,
                            vb.Name),
                        (AeArchiveJoinedFileViewModel va, AeArchiveFileViewModel vb) => CompareFileNameNumerically(
                            va.Name,
                            vb.Name),
                        (AeArchiveFileViewModel va, AeArchiveJoinedFileViewModel vb) => CompareFileNameNumerically(
                            va.Name,
                            vb.Name),
                        (AeArchiveJoinedFileViewModel va, AeArchiveJoinedFileViewModel vb) =>
                            CompareFileNameNumerically(va.Name,
                                vb.Name),
                        _ => throw new NotSupportedException(),
                    }
                ))
        );

        var i = 0;
        var padding = (int)Math.Ceiling(Math.Log10(Entries.Count(f => f switch
        {
            AeArchiveFileViewModel a => ArchiveService.IsImageFile(a.Name),
            AeArchiveJoinedFileViewModel a => ArchiveService.IsImageFile(a.Name),
            _ => false,
        })));
        foreach (var entry in newEntries)
        {
            switch (entry)
            {
                case AeArchiveFileViewModel f when ArchiveService.IsImageFile(f.Name):
                    f.Name = i.ToString().PadLeft(padding, '0') + Path.GetExtension(f.OriginalName);
                    i++;
                    break;
                case AeArchiveJoinedFileViewModel fl when ArchiveService.IsImageFile(fl.Name):
                    fl.Name = i.ToString().PadLeft(padding, '0') + Path.GetExtension(fl.Name);
                    i++;
                    break;
            }
        }

        Entries = newEntries;
    }

    private static int CompareFileNameNumerically(string a, string b)
    {
        a = Path.GetFileNameWithoutExtension(a);
        b = Path.GetFileNameWithoutExtension(b);
        if (!int.TryParse(a, out var left))
            return int.TryParse(b, out _) ? -1 : string.CompareOrdinal(a, b);

        if (!int.TryParse(b, out var right))
            return 1;

        return left - right;
    }
}

public partial class ArchiveEditorViewModel : PageViewModelBase
{
    public Guid ArchiveId { get; }
    private readonly IArchiveReader _reader;
    public override string HeaderText { get; } = "Archive Editor";

    [ObservableProperty] private ObservableCollection<AeArchiveFolderViewModel> _rootFolder;

    public IEnumerable<ViewModelBase> Images => RootFolder[0].EnumerateImages();

    [ObservableProperty] private ViewModelBase? _selectedImage;

    public ArchiveEditorViewModel(Guid archiveId, IArchiveReader reader)
    {
        ArchiveId = archiveId;
        _reader = reader;
        _rootFolder = [new AeArchiveFolderViewModel(_reader.ReadFolderTree(), reader)];
        RootFolder[0].PropertyChanged += OnRootFolderPropertyChanged;
    }

    private void OnRootFolderPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AeArchiveFolderViewModel.Entries))
            OnPropertyChanged(nameof(Images));
    }
}