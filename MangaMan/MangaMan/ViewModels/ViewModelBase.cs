using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;

namespace MangaMan.ViewModels;

public abstract class ViewModelBase : ObservableObject
{
    public MainWindowViewModel MainWindowVM { get; set; }
}