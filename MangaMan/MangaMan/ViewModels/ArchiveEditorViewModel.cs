using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.Templates;
using Avalonia.Media.Imaging;
using Avalonia.Metadata;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MangaMan.Service;
using MangaMan.Views;
using NetVips;
using Image = Avalonia.Controls.Image;

namespace MangaMan.ViewModels;

public partial class AeArchiveFileViewModel(
    MainWindowViewModel mainVm,
    AeArchiveFolderViewModel folder,
    ArchiveFile file,
    IArchiveReader reader)
    : ViewModelBase(mainVm), IDisposable
{
    public string OriginalName { get; } = file.Name;
    public AeArchiveFolderViewModel Folder { get; } = folder;
    public string Path { get; } = file.Path;
    public bool IsImage { get; } = file.IsImage;
    [ObservableProperty] private string _name = file.Name;
    [ObservableProperty] private bool _deleted;

    public bool CanJoinWithNext => FindNextUndeleted() is not null;
    public bool CanJoinWithPrevious => FindPreviousUndeleted() is not null;

    public Task<Bitmap?> Thumbnail
    {
        get
        {
            field ??= GetImageAsync();
            return field;
        }
    }

    [RelayCommand(CanExecute = nameof(CanJoinWithNext))]
    private void JoinWithNext() => Folder.JoinEntries(this, FindNextUndeleted()!);

    [RelayCommand(CanExecute = nameof(CanJoinWithPrevious))]
    private void JoinWithPrevious() => Folder.JoinEntries(this, FindPreviousUndeleted()!);

    [RelayCommand]
    private void ToggleDelete() => Deleted = !Deleted;

    private AeArchiveFileViewModel? FindNextUndeleted()
        => Folder.Entries
            .SkipWhile(e => !ReferenceEquals(this, e))
            .Skip(1)
            .FirstOrDefault(e => e is AeArchiveFileViewModel { Deleted: false }) as AeArchiveFileViewModel;

    private AeArchiveFileViewModel? FindPreviousUndeleted()
        => Folder.Entries
            .TakeWhile(e => !ReferenceEquals(this, e))
            .LastOrDefault(e => e is AeArchiveFileViewModel { Deleted: false }) as AeArchiveFileViewModel;

    private async Task<Bitmap?> GetImageAsync()
    {
        var bytes = await reader.ReadAllBytesAsync(Path);
        return bytes is null ? null : Bitmap.DecodeToHeight(new MemoryStream(bytes), 400);
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        Thumbnail.Dispose();
    }
}

public partial class AeArchiveJoinedFileViewModel(
    MainWindowViewModel mainVm,
    AeArchiveFolderViewModel Folder,
    string name,
    AeArchiveFileViewModel left,
    AeArchiveFileViewModel right
) : ViewModelBase(mainVm), IDisposable
{
    [ObservableProperty] private string _name = name;
    [ObservableProperty] private AeArchiveFileViewModel _left = left;
    [ObservableProperty] private AeArchiveFileViewModel _right = right;
    [ObservableProperty] private bool _deleted;

    [RelayCommand]
    private void SwapSides()
    {
        (Left, Right) = (Right, Left);
    }

    [RelayCommand]
    private void SplitIntoHalves()
    {
        Folder.SplitEntry(this);
    }

    [RelayCommand]
    private void ToggleDelete()
    {
        Deleted = !Deleted;
        Left.Deleted = Deleted;
        Right.Deleted = Deleted;
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        Left.Dispose();
        Right.Dispose();
    }
}

public partial class AeArchiveFolderViewModel : ViewModelBase, IDisposable
{
    public string OriginalName { get; }
    [ObservableProperty] private string _name;

    [ObservableProperty] private ObservableCollection<ViewModelBase> _entries;

    public AeArchiveFolderViewModel(MainWindowViewModel mainVm, ArchiveFolder folder, IArchiveReader reader) :
        base(mainVm)
    {
        OriginalName = folder.Name;
        _name = folder.Name;
        _entries = new ObservableCollection<ViewModelBase>(
            folder.Folders.Select<ArchiveFolder, ViewModelBase>(f => new AeArchiveFolderViewModel(mainVm, f, reader))
                .Concat(folder.Files.Select(f => new AeArchiveFileViewModel(MainWindowVM, this, f, reader)))
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

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        foreach (var entry in Entries)
            if (entry is IDisposable d)
                d.Dispose();
    }

    public AeArchiveJoinedFileViewModel JoinEntries(AeArchiveFileViewModel left, AeArchiveFileViewModel right)
    {
        if (!ReferenceEquals(left.Folder, this))
            throw new ArgumentException("file not from this folder", nameof(left));

        if (!ReferenceEquals(right.Folder, this))
            throw new ArgumentException("file not from this folder", nameof(right));

        var joinedPage = new AeArchiveJoinedFileViewModel(
            MainWindowVM,
            this,
            left.Name,
            left,
            right
        );

        Entries.Remove(right);
        Entries.Insert(Entries.IndexOf(left), joinedPage);
        Entries.Remove(left);

        return joinedPage;
    }

    public void SplitEntry(AeArchiveJoinedFileViewModel joined)
    {
        Entries.Insert(Entries.IndexOf(joined), joined.Left);
        Entries.Insert(Entries.IndexOf(joined), joined.Right);
        Entries.Remove(joined);
    }
}

public partial class ArchiveEditorViewModel : PageViewModelBase, IDisposable
{
    public Guid ArchiveId { get; }
    private readonly IArchiveReader _reader;
    public override string HeaderText { get; } = "Archive Editor";

    [ObservableProperty] private ObservableCollection<AeArchiveFolderViewModel> _rootFolder;

    public IEnumerable<ViewModelBase> Images => RootFolder[0].EnumerateImages();

    [ObservableProperty] private ObservableCollection<ViewModelBase> _selectedEntries = [];

    public bool CanJoinSelectedItems
        => SelectedEntries is
        [
            AeArchiveFileViewModel { Deleted: false } a,
            AeArchiveFileViewModel { Deleted: false } b,
            ..
        ] && ReferenceEquals(a.Folder, b.Folder);

    public ArchiveEditorViewModel(MainWindowViewModel mainVm, Guid archiveId, IArchiveReader reader) : base(mainVm)
    {
        ArchiveId = archiveId;
        _reader = reader;
        _rootFolder = [new AeArchiveFolderViewModel(MainWindowVM, _reader.ReadFolderTree(), reader)];
        RootFolder[0].PropertyChanged += OnRootFolderPropertyChanged;
        RootFolder[0].Entries.CollectionChanged += EntriesOnCollectionChanged;
    }

    [RelayCommand(CanExecute = nameof(CanJoinSelectedItems))]
    private void JoinSelectedItems()
    {
        var left = (SelectedEntries[0] as AeArchiveFileViewModel)!;
        var right = (SelectedEntries[1] as AeArchiveFileViewModel)!;

        SelectedEntries.Clear();
        SelectedEntries.Add(left.Folder.JoinEntries(left, right));
    }

    [RelayCommand(AllowConcurrentExecutions = false)]
    private async Task SaveChanges()
    {
        switch (_reader)
        {
            case ZipArchiveReader:
                await SaveZipArchive();
                break;
            case FolderArchiveReader:
                await SaveFolderArchive();
                break;
        }
    }

    private async Task SaveZipArchive()
    {
        var directoryName = Path.GetDirectoryName(_reader.Path)!;
        var newFileName = Path.GetFileNameWithoutExtension(_reader.Path) + "__TEMP__" + Path.GetExtension(_reader.Path);
        var newFilePath = Path.Combine(directoryName, newFileName);
        await using (var newZip = new ZipArchive(File.Open(newFilePath, FileMode.Create), ZipArchiveMode.Update, false))
        {
            await SaveZipArchive(newZip, RootFolder[0]);
        }

        File.Delete(_reader.Path);
        File.Move(newFilePath, _reader.Path);
    }

    private async Task SaveZipArchive(ZipArchive archive, AeArchiveFolderViewModel folder, string prefix = "")
    {
        foreach (var entry in folder.Entries)
        {
            switch (entry)
            {
                case AeArchiveFolderViewModel c:
                {
                    await SaveZipArchive(archive, c, prefix + $"/{c.Name}");
                    break;
                }
                case AeArchiveFileViewModel { Deleted: false } f:
                {
                    var newEntry = archive.CreateEntry(prefix + f.Name);
                    var bytes = (await _reader.ReadAllBytesAsync(f.Path))!;

                    await using var newStream = newEntry.Open();
                    newStream.Write(bytes, 0, bytes.Length);
                    break;
                }
                case AeArchiveJoinedFileViewModel { Deleted: false } j:
                {
                    var newEntry = archive.CreateEntry(prefix + j.Name);
                    using var leftStream = new MemoryStream((await _reader.ReadAllBytesAsync(j.Left.Path))!);
                    using var left = NetVips.Image.NewFromStream(leftStream, access: NetVips.Enums.Access.Sequential);
                    using var rightStream = new MemoryStream((await _reader.ReadAllBytesAsync(j.Right.Path))!);
                    using var right = NetVips.Image.NewFromStream(rightStream, access: NetVips.Enums.Access.Sequential);

                    using var joined = left.Join(right, Enums.Direction.Horizontal);
                    using var outputStream = newEntry.Open();
                    joined.WriteToStream(outputStream, j.Name);
                    break;
                }
            }
        }
    }

    private async Task SaveFolderArchive()
    {
        var directoryName = Path.GetDirectoryName(_reader.Path)!;
        var newFileName = Path.GetFileNameWithoutExtension(_reader.Path) + "__TEMP__";
        var newFilePath = Path.Combine(directoryName, newFileName);

        Directory.CreateDirectory(newFilePath);
        await SaveFolderArchive(RootFolder[0], newFilePath);

        Directory.Delete(_reader.Path, true);
        Directory.Move(newFilePath, _reader.Path);
    }

    private async Task SaveFolderArchive(AeArchiveFolderViewModel folder, string prefix)
    {
        foreach (var entry in folder.Entries)
        {
            switch (entry)
            {
                case AeArchiveFolderViewModel c:
                {
                    Directory.CreateDirectory(Path.Combine(prefix, c.Name));
                    await SaveFolderArchive(c, Path.Combine(prefix, c.Name));
                    break;
                }
                case AeArchiveFileViewModel { Deleted: false } f:
                {
                    File.Move(Path.Combine(_reader.Path, f.Path), Path.Combine(prefix, f.Name));
                    break;
                }
                case AeArchiveJoinedFileViewModel { Deleted: false } j:
                {
                    using var leftStream = new MemoryStream((await _reader.ReadAllBytesAsync(j.Left.Path))!);
                    using var left = NetVips.Image.NewFromStream(leftStream, access: NetVips.Enums.Access.Sequential);
                    using var rightStream = new MemoryStream((await _reader.ReadAllBytesAsync(j.Right.Path))!);
                    using var right = NetVips.Image.NewFromStream(rightStream, access: NetVips.Enums.Access.Sequential);

                    using var joined = left.Join(right, Enums.Direction.Horizontal);
                    joined.WriteToFile(Path.Combine(prefix, j.Name));
                    break;
                } 
            }
        }
    }

    private void OnRootFolderPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AeArchiveFolderViewModel.Entries))
            OnPropertyChanged(nameof(Images));
    }

    private void EntriesOnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(Images));
    }

    public override async Task<bool> CanCloseAsync()
    {
        var answer = await MessageBox.Show(
            null,
            "Want to exit?",
            "Any changes you might have made will be lost, still want to close this tab?",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question
        );

        return answer ?? false;
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        foreach (var folder in RootFolder)
            folder.Dispose();
    }
}