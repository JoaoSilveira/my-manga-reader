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

    public IEnumerable<ArchiveViewModel>? SelectedFolderArchives =>
        SelectedSyncFolder?.Archives
            ?.Where(a => a.Name.Contains(FilterText, StringComparison.CurrentCultureIgnoreCase));

    [NotifyPropertyChangedFor(nameof(SelectedFolderArchives))] [ObservableProperty]
    private string _filterText = string.Empty;

    public async Task Initialize()
    {
        IsWorking = true;
        SyncFolders = await ReadFoldersFromDbAsync();
        IsWorking = false;
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

    protected override async void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.PropertyName != nameof(SelectedSyncFolder))
            return;

        if (SelectedSyncFolder is not { } vm)
            return;

        if (vm.LoadArchivesCommand.CanExecute(null))
            await vm.LoadArchivesCommand.ExecuteAsync(null);
        
        if (FilterText == string.Empty)
            OnPropertyChanged(nameof(SelectedFolderArchives));
        else
            FilterText = string.Empty;
    }

    private async Task<List<SyncFolderViewModel>> ReadFoldersFromDbAsync() =>
        await ReadFoldersFromDbAsync(new MangaManDbContext());

    private async Task<List<SyncFolderViewModel>> ReadFoldersFromDbAsync(MangaManDbContext ctx,
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
                MainWindowVM = MainWindowVM,
                SyncFolderId = folder.Id,
                Name = folder.Name,
                Path = folder.Path,
                Children = await ReadFoldersFromDbAsync(ctx, folder.Id),
            });
        }

        return folders;
    }
}