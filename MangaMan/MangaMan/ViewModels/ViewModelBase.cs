using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;

namespace MangaMan.ViewModels;

public abstract class ViewModelBase : ObservableObject
{
    protected MainWindowViewModel MainWindowVM { get; }

    protected ViewModelBase(MainWindowViewModel mainWindowVM)
    {
        MainWindowVM = mainWindowVM;
    }
}