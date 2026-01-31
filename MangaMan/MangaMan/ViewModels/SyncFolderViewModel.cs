using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MangaMan.Models;
using MangaMan.Service;
using Microsoft.EntityFrameworkCore;

namespace MangaMan.ViewModels;

public partial class SyncFolderViewModel(MainWindowViewModel mainVm) : ViewModelBase(mainVm)
{
    public required Guid SyncFolderId { get; init; }
    public required string Name { get; init; }
    public required string Path { get; init; }
    public SyncFolderViewModel? Parent { get; init; }

    [ObservableProperty] private bool _isExpanded;
    [ObservableProperty] private bool _isSelected;

    public bool CanLoadArchives => Archives is null;

    public ObservableCollection<SyncFolderViewModel> Children { get; set; } = [];

    [ObservableProperty] private ObservableCollection<ArchiveViewModel>? _archives = null;

    [RelayCommand(CanExecute = nameof(CanLoadArchives), AllowConcurrentExecutions = false)]
    private async Task LoadArchives()
    {
        await using var ctx = new MangaManDbContext();
        var archiveList = await ctx.MangaArchives
            .Where(a => a.SyncFolderId == SyncFolderId)
            .ToAsyncEnumerable()
            .Select(a => new ArchiveViewModel(MainWindowVM)
            {
                ArchiveId = a.Id,
                Name = a.Name,
                Path = a.Path,
                WasRead = a.WasRead,
            })
            .ToListAsync();
        Archives = new ObservableCollection<ArchiveViewModel>(archiveList);
    }

    [RelayCommand]
    private async Task Refresh()
    {
        var syncDate = new DateTime();
        await using var ctx = new MangaManDbContext();
        var parsedFolder = SyncFolderService.ReadFolder(Path);

        await PersistSyncFolder(ctx, null, parsedFolder, syncDate);

        await ctx.SyncFolders
            .Where(f => f.Id == SyncFolderId)
            .ExecuteUpdateAsync(setter => setter.SetProperty(f => f.LastSyncAt, syncDate));

        await ctx.SaveChangesAsync();

        await LoadChildTree(ctx, this);
    }

    private async Task LoadChildTree(MangaManDbContext ctx, SyncFolderViewModel folder)
    {
        if (Archives is not null)
        {
            await foreach (var mangaArchive in ctx.MangaArchives.Where(a => a.SyncFolderId == folder.SyncFolderId)
                               .ToAsyncEnumerable())
            {
                if (Archives.Any(a => a.Path == mangaArchive.Path))
                    continue;

                Archives.Add(new ArchiveViewModel(MainWindowVM)
                {
                    ArchiveId = mangaArchive.Id,
                    Path = mangaArchive.Path,
                    Name = mangaArchive.Name,
                    WasRead = mangaArchive.WasRead,
                });
            }
        }

        await foreach (var child in ctx.SyncFolders.Where(f => f.ParentId == folder.SyncFolderId).ToAsyncEnumerable())
        {
            if (Children.Any(c => c.Path == child.Path))
                continue;

            var newFolder = new SyncFolderViewModel(MainWindowVM)
            {
                Parent = folder,
                SyncFolderId = child.Id,
                Name = child.Name,
                Path = child.Path,
            };
            Children.Add(newFolder);
            await LoadChildTree(ctx, newFolder);
        }
    }

    private static async Task PersistSyncFolder(MangaManDbContext ctx, Guid? parentId,
        SyncFolderService.Folder folder, DateTime syncDate)
    {
        var syncFolder = await ctx.SyncFolders.Where(f => f.Path == folder.Path).FirstOrDefaultAsync();
        if (syncFolder is null)
        {
            var thingy = await ctx.SyncFolders.AddAsync(new SyncFolder()
            {
                ParentId = parentId!,
                Name = folder.Name,
                Path = folder.Path,
                CreatedAt = DateTime.Now,
                LastSyncAt = syncDate,
            });

            syncFolder = thingy.Entity;
        }

        foreach (var child in folder.Children)
            await PersistSyncFolder(ctx, syncFolder.Id, child, syncDate);

        foreach (var archive in folder.Archives)
        {
            var archiveExists = await ctx.MangaArchives
                .AnyAsync(a => a.Path == archive.Path);
            if (archiveExists)
                continue;

            await ctx.MangaArchives.AddAsync(new MangaArchive()
            {
                SyncFolderId = syncFolder.Id,
                CreatedAt = DateTime.Now,
                Name = archive.Name,
                Path = archive.Path,
                WasRead = false,
                LastOpenedAt = null,
                SyncFolder = syncFolder,
            });
        }
    }
}