using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using MangaMan.Service;

namespace MangaMan.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    public required HomeViewModel HomeViewModel { get; set; }

    [ObservableProperty] private ObservableCollection<ViewModelBase> _tabs;

    [ObservableProperty] private bool _isWorking = false;

    [ObservableProperty] private ViewModelBase? _selectedTab;

    public static MainWindowViewModel Create()
    {
        var homeViewModel = new HomeViewModel();

        var vm = new MainWindowViewModel()
        {
            SelectedTab = homeViewModel,
            HomeViewModel = homeViewModel,
            Tabs = [homeViewModel],
        };

        homeViewModel.MainWindowVM = vm;
        homeViewModel.Initialize();

        return vm;
    }

    public void OpenArchive(string path)
    {
        var existingTab = Tabs
            .FirstOrDefault(t => t is ArchiveReaderViewModel v && v.Path == path);
        if (existingTab != null)
        {
            SelectedTab = existingTab;
            return;
        }

        var reader = ArchiveService.OpenArchive(path);
        var archiveVm = new ArchiveReaderViewModel(reader)
        {
            Path = path,
            Name = Path.GetFileName(path),
        };

        Tabs.Add(archiveVm); 
        SelectedTab = archiveVm;
    }

    public void CloseTab(ViewModelBase dataContext)
    {
        Tabs.Remove(dataContext);
    }
}