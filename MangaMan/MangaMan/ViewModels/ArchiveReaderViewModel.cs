using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MangaMan.Models;
using MangaMan.Service;
using MangaMan.Views;
using Microsoft.EntityFrameworkCore;

namespace MangaMan.ViewModels;

public partial class ArchiveReaderViewModel : PageViewModelBase
{
    public override string HeaderText => Name;

    private readonly Dictionary<string, Bitmap?> _cache = [];
    public required Guid ArchiveId { get; init; }
    public required string Path { get; init; }
    public required string Name { get; init; }
    private IArchiveReader Reader { get; }

    [NotifyPropertyChangedFor(nameof(CurrentImage))]
    [NotifyCanExecuteChangedFor(nameof(GoNextPageCommand))]
    [NotifyCanExecuteChangedFor(nameof(GoPreviousPageCommand))]
    [ObservableProperty]
    private int _selectedIndex;

    public bool CanGoNextPage => SelectedIndex < Reader.Images.Count - 1;
    public bool CanGoPreviousPage => SelectedIndex > 0;
    public string CurrentPagePath => Reader.Images.ElementAt(SelectedIndex);

    public Bitmap? CurrentImage
    {
        get
        {
            if (SelectedIndex < 0 || SelectedIndex >= Reader.Images.Count)
                return null;

            return _cache.TryGetValue(CurrentPagePath, out var image) ? image : null;
        }
    }

    public ArchiveReaderViewModel(IArchiveReader reader)
    {
        Reader = reader;
        _selectedIndex = -1;
    }

    public async Task EnsureInitializedAsync()
    {
        if (SelectedIndex >= 0)
        {
            if (_cache.ContainsKey(CurrentPagePath))
                return;

            var img = await LoadImageAsync(CurrentPagePath);

            OnPropertyChanging(nameof(CurrentImage));
            _cache.Add(CurrentPagePath, img);
            OnPropertyChanged(nameof(CurrentImage));
            return;
        }

        if (Reader.Images.Count > 0 && GoNextPageCommand.CanExecute(null))
            await GoNextPageCommand.ExecuteAsync(null);
    }

    [RelayCommand(AllowConcurrentExecutions = false)]
    private async Task EndOfArchiveReached()
    {
        var res = await MessageBox.Show(
            null,
            "Archive ended",
            "Would you like to close the archive?",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);

        if (res != true)
            return;

        await using (var ctx = new MangaManDbContext())
        {
            await ctx.MangaArchives
                .Where(a => a.Id == ArchiveId)
                .ExecuteUpdateAsync(setters => setters.SetProperty(a => a.WasRead, true));
            await ctx.SaveChangesAsync();
        }

        await MainWindowVM.CloseTab(this);
    }

    [RelayCommand(CanExecute = nameof(CanGoNextPage), AllowConcurrentExecutions = false)]
    private async Task GoNextPage()
    {
        var path = Reader.Images.ElementAt(SelectedIndex + 1);
        if (!_cache.ContainsKey(path))
            _cache.Add(path, await LoadImageAsync(path));

        SelectedIndex++;

        await using var ctx = new MangaManDbContext();
        var tab = await ctx.OpenTabs.Where(tab => tab.MangaArchiveId == ArchiveId).FirstAsync();
        tab.CurrentPage = CurrentPagePath;
        await ctx.SaveChangesAsync();
    }

    [RelayCommand(CanExecute = nameof(CanGoPreviousPage), AllowConcurrentExecutions = false)]
    private async Task GoPreviousPage()
    {
        var path = Reader.Images.ElementAt(SelectedIndex - 1);
        if (!_cache.ContainsKey(path))
            _cache.Add(path, await LoadImageAsync(path));

        SelectedIndex--;

        await using var ctx = new MangaManDbContext();
        var tab = await ctx.OpenTabs.Where(tab => tab.MangaArchiveId == ArchiveId).FirstAsync();
        tab.CurrentPage = CurrentPagePath;
        await ctx.SaveChangesAsync();
    }

    private async Task<Bitmap?> LoadImageAsync(string path)
    {
        var bytes = await Reader.ReadAllBytesAsync(path);
        var bitmap = bytes is not null ? new Bitmap(new MemoryStream(bytes)) : null;

        return bitmap;
    }
}