using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;

namespace MangaMan.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    public required HomeViewModel HomeViewModel { get; set; }

    public required ObservableCollection<ViewModelBase> Tabs { get; init; }

    [ObservableProperty] private bool _isWorking = false;

    public static MainWindowViewModel Create()
    {
        var homeViewModel = new HomeViewModel();

        return new MainWindowViewModel()
        {
            HomeViewModel = homeViewModel,
            Tabs = [homeViewModel],
        };
    }
}