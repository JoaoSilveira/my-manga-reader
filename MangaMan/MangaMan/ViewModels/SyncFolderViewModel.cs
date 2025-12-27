using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MangaMan.Models;

namespace MangaMan.ViewModels;

public partial class SyncFolderViewModel : ViewModelBase
{
    public required Guid SyncFolderId { get; init; }
    public required string Name { get; init; }
    public required string Path { get; init; }

    [NotifyCanExecuteChangedFor(nameof(LoadArchivesCommand))] [ObservableProperty]
    private bool _isWorking = false;

    public bool CanLoadArchives => !IsWorking && Archives is null;

    public List<SyncFolderViewModel> Children { get; init; } = [];

    [ObservableProperty] private List<ArchiveViewModel>? _archives = null;

    [RelayCommand(CanExecute = nameof(CanLoadArchives))]
    private async Task LoadArchives()
    {
        IsWorking = true;
        await using var ctx = new MangaManDbContext();
        Archives = await ctx.MangaArchives
            .Where(a => a.SyncFolderId == SyncFolderId)
            .ToAsyncEnumerable()
            .Select(a => new ArchiveViewModel()
            {
                MainWindowVM = MainWindowVM,
                ArchiveId = a.Id,
                Name = a.Name,
                Path = a.Path,
            })
            .ToListAsync();
        IsWorking = false;
    }
}