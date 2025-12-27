using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using MangaMan.ViewModels;

namespace MangaMan.Views;

public partial class ArchiveReaderView : UserControl
{
    public ArchiveReaderView()
    {
        InitializeComponent();
    }

    protected override async void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);

        var pos = e.GetPosition(this).X / Bounds.Width;

        var vm = (DataContext as ArchiveReaderViewModel)!;
        switch (pos)
        {
            case > .6:
                if (vm.GoNextPageCommand.CanExecute(null))
                {
                    await vm.GoNextPageCommand.ExecuteAsync(null);
                    ScrollContainer.ScrollToHome();
                }
                else if (vm.EndOfArchiveReachedCommand.CanExecute(null))
                {
                    await vm.EndOfArchiveReachedCommand.ExecuteAsync(null);
                }

                break;

            case < .3:
                if (vm.GoPreviousPageCommand.CanExecute(null))
                {
                    await vm.GoPreviousPageCommand.ExecuteAsync(null);
                    ScrollContainer.ScrollToHome();
                }
                break;
        }
    }

    protected override async void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        await (DataContext as ArchiveReaderViewModel)!.EnsureInitializedAsync();
    }
}