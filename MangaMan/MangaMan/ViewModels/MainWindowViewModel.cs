using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using MangaMan.Models;
using MangaMan.Service;
using Microsoft.EntityFrameworkCore;

namespace MangaMan.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    [ObservableProperty] private ObservableCollection<ViewModelBase> _tabs;

    [ObservableProperty] private bool _isWorking = false;

    [ObservableProperty] private ViewModelBase? _selectedTab;

    public static async Task<MainWindowViewModel> Create()
    {
        var vm = new MainWindowViewModel()
        {
            SelectedTab = null,
            Tabs = [],
        };
        var homeViewModel = new HomeViewModel(vm);
        await homeViewModel.Initialize();
        vm.Tabs.Add(homeViewModel);

        await using var ctx = new MangaManDbContext();
        var openTagsIterator = ctx.OpenTabs
            .Include(t => t.MangaArchive)
            .AsAsyncEnumerable()
            .Select(tab =>
            {
                var reader = ArchiveService.OpenArchive(tab.MangaArchive.Path);
                return new ArchiveReaderViewModel(vm, reader)
                {
                    ArchiveId = tab.MangaArchiveId,
                    Path = tab.MangaArchive.Path,
                    Name = tab.MangaArchive.Name,
                    SelectedIndex = reader.Images
                        .Index()
                        .First(tup => tup.Item == tab.CurrentPage)
                        .Index,
                };
            });

        await foreach (var tab in openTagsIterator)
            vm.Tabs.Add(tab);

        return vm;
    }

    public async Task OpenArchive(Guid archiveId, string path)
    {
        var existingTab = Tabs
            .FirstOrDefault(t => t is ArchiveReaderViewModel v && v.Path == path);
        if (existingTab != null)
        {
            SelectedTab = existingTab;
            return;
        }

        var reader = ArchiveService.OpenArchive(path);
        var archiveVm = new ArchiveReaderViewModel(this, reader)
        {
            ArchiveId = archiveId,
            Path = path,
            Name = Path.GetFileName(path),
        };

        await using (var ctx = new MangaManDbContext())
        {
            await ctx.OpenTabs.AddAsync(new OpenTab()
            {
                MangaArchiveId = archiveId,
                CurrentPage = reader.Images.ElementAt(0),
            });

            await ctx.MangaArchives
                .Where(t => t.Id == archiveId)
                .ExecuteUpdateAsync(setters => setters.SetProperty(a => a.LastOpenedAt, DateTime.Now));

            await ctx.SaveChangesAsync();
        }

        Tabs.Add(archiveVm);
        SelectedTab = archiveVm;
    }

    public async Task EditArchive(Guid archiveId)
    {
        var openTab = Tabs
            .FirstOrDefault(t => t is ArchiveEditorViewModel vm && vm.ArchiveId == archiveId);
        if (openTab is not null)
        {
            SelectedTab = openTab;
            return;
        }

        await using var ctx = new MangaManDbContext();
        var archive = await ctx.MangaArchives
            .FirstAsync(t => t.Id == archiveId);

        var reader = ArchiveService.OpenArchive(archive.Path);
        var vm = new ArchiveEditorViewModel(this, archiveId, reader);

        Tabs.Add(vm);
        SelectedTab = vm;
    }

    public async Task CloseTab(ViewModelBase dataContext)
    {
        if (dataContext is PageViewModelBase page && !(await page.CanCloseAsync()))
            return;

        switch (dataContext)
        {
            case ArchiveEditorViewModel:
                Tabs.Remove(dataContext);
                break;
            case ArchiveReaderViewModel reader:
            {
                await using var ctx = new MangaManDbContext();
                await ctx.OpenTabs.Where(tab => tab.MangaArchiveId == reader.ArchiveId)
                    .ExecuteDeleteAsync();

                await ctx.SaveChangesAsync();
                Tabs.Remove(dataContext);
                break;
            }
        }

        switch (dataContext)
        {
            case IAsyncDisposable d:
                await d.DisposeAsync();
                break;
            case IDisposable d:
                d.Dispose();
                break;
        }
    }
}