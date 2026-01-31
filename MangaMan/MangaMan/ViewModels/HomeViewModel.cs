using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MangaMan.Models;
using MangaMan.Service;
using Microsoft.EntityFrameworkCore;

namespace MangaMan.ViewModels;

public partial class HomeViewModel(MainWindowViewModel mainVm) : PageViewModelBase(mainVm)
{
    public override string HeaderText => "Home";

    [ObservableProperty] private List<SyncFolderViewModel> _syncFolders = [];
    
    public Vector ScrollOffset { get; set; } = new(0, 0);

    public SyncFolderViewModel? SelectedSyncFolder
    {
        get;
        set
        {
            if (value is null && field is not null)
                return;

            SetProperty(ref field, value);
        }
    }

    public IEnumerable<ArchiveViewModel>? SelectedFolderArchives =>
        SelectedSyncFolder?.Archives
            ?.Where(a => a.Name.Contains(FilterText, StringComparison.CurrentCultureIgnoreCase));

    [NotifyPropertyChangedFor(nameof(SelectedFolderArchives))] [ObservableProperty]
    private string _filterText = string.Empty;

    public async Task Initialize()
    {
        SyncFolders = await ReadFoldersFromDbAsync();
    }

    public void MakeSelectionVisible()
    {
        SelectedSyncFolder?.IsSelected = true;
        var folder = SelectedSyncFolder?.Parent;
        while (folder is not null)
        {
            folder.IsExpanded = true;
            folder = folder.Parent;
        }
    }

    [RelayCommand(AllowConcurrentExecutions = false)]
    private async Task AddFolder(IStorageProvider storageProvider)
    {
        var folders = await storageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions()
        {
            AllowMultiple = true,
            Title = "Pick a folder to sync",
        });

        if (folders.Count < 1)
            return;

        await using var ctx = new MangaManDbContext();
        foreach (var folder in folders)
        {
            var parsedFolder = SyncFolderService.ReadFolder(folder.Path.LocalPath);
            await SyncFolderService.PersistFolderAsync(ctx, parsedFolder);
        }

        await ctx.SaveChangesAsync();
        SyncFolders = await ReadFoldersFromDbAsync(ctx);
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
        SyncFolderViewModel? parent = null)
    {
        var parentId = parent?.SyncFolderId;
        var folderIterator = ctx.SyncFolders
            .Where(f => f.ParentId == parentId)
            .ToAsyncEnumerable();

        var folders = new List<SyncFolderViewModel>();
        await foreach (var folder in folderIterator)
        {
            var newFolder = new SyncFolderViewModel(MainWindowVM)
            {
                Parent = parent,
                SyncFolderId = folder.Id,
                Name = folder.Name,
                Path = folder.Path,
            };
            folders.Add(newFolder);
            newFolder.Children =
                new ObservableCollection<SyncFolderViewModel>(await ReadFoldersFromDbAsync(ctx, newFolder));
        }

        return folders;
    }
}