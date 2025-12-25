using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MangaMan.Models;
using MangaMan.Service;
using Microsoft.EntityFrameworkCore;

namespace MangaMan.ViewModels;

public partial class HomeViewModel : PageViewModelBase
{
    public override string HeaderText => "Home";

    [NotifyCanExecuteChangedFor(nameof(AddFolderCommand))] [ObservableProperty]
    private bool _isWorking = false;

    public bool IsNotWorking => !IsWorking;

    [ObservableProperty] private List<SyncFolderViewModel> _syncFolders = [];

    [ObservableProperty] private SyncFolderViewModel? _selectedSyncFolder;

    public HomeViewModel()
    {
        _isWorking = true;
        ReadFoldersFromDbAsync()
            .ContinueWith(task =>
            {
                SyncFolders = task.Result;
                IsWorking = false;
            });
    }

    [RelayCommand(CanExecute = nameof(IsNotWorking))]
    private async Task AddFolder(IStorageProvider storageProvider)
    {
        IsWorking = true;

        var folders = await storageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions()
        {
            AllowMultiple = true,
            Title = "Pick a folder to sync",
        });

        if (folders.Count < 1)
        {
            IsWorking = false;
            return;
        }

        await using var ctx = new MangaManDbContext();
        foreach (var folder in folders)
        {
            var parsedFolder = SyncFolderService.ReadFolder(folder.Path.LocalPath);
            await SyncFolderService.PersistFolderAsync(ctx, parsedFolder);
        }

        await ctx.SaveChangesAsync();
        SyncFolders = await ReadFoldersFromDbAsync(ctx);

        IsWorking = false;
    }

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.PropertyName != nameof(SelectedSyncFolder))
            return;

        if (SelectedSyncFolder is { } vm && vm.LoadArchivesCommand.CanExecute(null))
            vm.LoadArchivesCommand.Execute(null);
    }

    private static async Task<List<SyncFolderViewModel>> ReadFoldersFromDbAsync() =>
        await ReadFoldersFromDbAsync(new MangaManDbContext());

    private static async Task<List<SyncFolderViewModel>> ReadFoldersFromDbAsync(MangaManDbContext ctx,
        Guid? parentId = null)
    {
        var folderIterator = ctx.SyncFolders
            .Where(f => f.ParentId == parentId)
            .ToAsyncEnumerable();

        var folders = new List<SyncFolderViewModel>();
        await foreach (var folder in folderIterator)
        {
            folders.Add(new SyncFolderViewModel()
            {
                SyncFolderId = folder.Id,
                Name = folder.Name,
                Path = folder.Path,
                Children = await ReadFoldersFromDbAsync(ctx, folder.Id),
            });
        }

        return folders;
    }
}

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
                ArchiveId = a.Id,
                Name = a.Name,
                Path = a.Path,
            })
            .ToListAsync();
        IsWorking = false;
    }
}

public class ArchiveViewModel
{
    public required Guid ArchiveId { get; init; }
    public required string Name { get; init; }
    public required string Path { get; init; }
}