using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MangaMan.Service;

namespace MangaMan.ViewModels;

public partial class ArchiveReaderViewModel : PageViewModelBase
{
    public override string HeaderText => $"{Name[..10]}";
    
    private readonly Dictionary<string, Task<Bitmap?>> _cache = [];
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

    public Task<Bitmap?> CurrentImage
    {
        get
        {
            if (SelectedIndex < 0 || SelectedIndex >= Reader.Images.Count)
                return Task.FromResult<Bitmap?>(null);

            if (!_cache.ContainsKey(Reader.Images.ElementAt(SelectedIndex)))
                _cache[Reader.Images.ElementAt(SelectedIndex)] = LoadImageAsync(Reader.Images.ElementAt(SelectedIndex));

            return _cache[Reader.Images.ElementAt(SelectedIndex)];
        }
    }

    public ArchiveReaderViewModel(IArchiveReader reader)
    {
        Reader = reader;
        _selectedIndex = Math.Min(Reader.Images.Count - 1, 0);
    }

    [RelayCommand(CanExecute = nameof(CanGoNextPage))]
    private void GoNextPage()
    {
        SelectedIndex++;
    }

    [RelayCommand(CanExecute = nameof(CanGoPreviousPage))]
    private void GoPreviousPage()
    {
        SelectedIndex--;
    }

    private async Task<Bitmap?> LoadImageAsync(string path)
    {
        var bytes = await Reader.ReadAllBytesAsync(path);

        return bytes is not null ? new Bitmap(new MemoryStream(bytes)) : null;
    }
}