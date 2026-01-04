using System.Threading.Tasks;

namespace MangaMan.ViewModels;

public abstract class PageViewModelBase(MainWindowViewModel mainVm) : ViewModelBase(mainVm)
{
    public abstract string HeaderText { get; }

    public virtual async Task<bool> CanCloseAsync() => await Task.FromResult(true);
}