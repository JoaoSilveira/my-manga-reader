using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MangaMan.Service;
using Tmds.DBus.Protocol;

namespace MangaMan.ViewModels;

public partial class ArchiveEditorPageViewModel(IArchiveReader reader) : ViewModelBase
{
    public required string OriginalPath { get; init; }
    [ObservableProperty] private string _newPath;

    [ObservableProperty] private bool _deleted;

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
        var bytes = await reader.ReadAllBytesAsync(OriginalPath);
        return bytes is null ? null : Bitmap.DecodeToHeight(new MemoryStream(bytes), 400);
    }
}

public partial class ArchiveEditorDoublePageViewModel : ViewModelBase
{
    [ObservableProperty] private ArchiveEditorPageViewModel _left;
    [ObservableProperty] private ArchiveEditorPageViewModel _right;

    [ObservableProperty] private string _newPath;
    [ObservableProperty] private bool _deleted;

    [RelayCommand]
    private void SwapSides()
    {
        (Left, Right) = (Right, Left);
    }
}

public partial class ArchiveEditorViewModel : PageViewModelBase
{
    public Guid ArchiveId { get; }
    private readonly IArchiveReader _reader;
    public override string HeaderText { get; } = "Archive Editor";

    [NotifyCanExecuteChangedFor(nameof(JoinWithNextCommand))]
    [NotifyCanExecuteChangedFor(nameof(JoinWithPreviousCommand))]
    [ObservableProperty]
    private ObservableCollection<ViewModelBase> _pages;

    [NotifyCanExecuteChangedFor(nameof(JoinWithNextCommand))]
    [NotifyCanExecuteChangedFor(nameof(JoinWithPreviousCommand))]
    [ObservableProperty]
    private ViewModelBase? _selectedPage;

    public bool CanJoinWithNext
    {
        get
        {
            return SelectedPage switch
            {
                ArchiveEditorPageViewModel vm => !vm.Deleted && NextUndeleted(vm) is not null,
                _ => false
            };
        }
    }

    public bool CanJoinWithPrevious
    {
        get
        {
            return SelectedPage switch
            {
                ArchiveEditorPageViewModel vm => !vm.Deleted && PreviousUndeleted(vm) is not null,
                _ => false
            };
        }
    }

    public bool CanSplitHalves => SelectedPage is ArchiveEditorDoublePageViewModel { Deleted: false };

    public ArchiveEditorViewModel(Guid archiveId, IArchiveReader reader)
    {
        ArchiveId = archiveId;
        _pages = new ObservableCollection<ViewModelBase>(
            reader.Images
                .Select(i => new ArchiveEditorPageViewModel(reader) { OriginalPath = i, NewPath = i })
        );
    }

    [RelayCommand]
    private static void ToggleDeletePage(ViewModelBase page)
    {
        switch (page)
        {
            case ArchiveEditorPageViewModel vm:
                vm.Deleted = !vm.Deleted;
                break;
            case ArchiveEditorDoublePageViewModel vm:
                vm.Deleted = !vm.Deleted;
                break;
        }
    }

    [RelayCommand(CanExecute = nameof(CanJoinWithNext))]
    private void JoinWithNext(ArchiveEditorPageViewModel page)
    {
        var next = NextUndeleted(page)!;

        var doublePage = new ArchiveEditorDoublePageViewModel()
        {
            Deleted = false,
            Left = page,
            Right = next,
        };

        Pages.Remove(next);
        Pages.Insert(Pages.IndexOf(page), doublePage);
        Pages.Remove(page);
        SelectedPage = doublePage;
    }

    [RelayCommand(CanExecute = nameof(CanJoinWithPrevious))]
    private void JoinWithPrevious(ArchiveEditorPageViewModel page)
    {
        var previous = PreviousUndeleted(page)!;

        var doublePage = new ArchiveEditorDoublePageViewModel()
        {
            Deleted = false,
            Left = previous,
            Right = page,
        };

        Pages.Remove(previous);
        Pages.Insert(Pages.IndexOf(page), doublePage);
        Pages.Remove(page);
        SelectedPage = doublePage;
    }

    [RelayCommand(CanExecute = nameof(CanSplitHalves))]
    private void SplitHalves(ArchiveEditorDoublePageViewModel page)
    {
        page.Left.Deleted = page.Deleted;
        page.Right.Deleted = page.Deleted;

        Pages.Insert(Pages.IndexOf(page), page.Left);
        Pages.Insert(Pages.IndexOf(page), page.Right);
        Pages.Remove(page);
        SelectedPage = page.Left;
    }

    private ArchiveEditorPageViewModel? NextUndeleted(ArchiveEditorPageViewModel? page = null)
    {
        if (page is null)
            return null;

        var selectedIndex = Pages.IndexOf(page);
        return Pages
            .Skip(selectedIndex + 1)
            .FirstOrDefault(vm => vm is ArchiveEditorPageViewModel { Deleted: false }) as ArchiveEditorPageViewModel;
    }

    private ArchiveEditorPageViewModel? PreviousUndeleted(ArchiveEditorPageViewModel? page = null)
    {
        if (page is null)
            return null;

        return Pages
            .TakeWhile(vm => !ReferenceEquals(vm, page))
            .LastOrDefault(vm => vm is ArchiveEditorPageViewModel { Deleted: false }) as ArchiveEditorPageViewModel;
    }
}