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

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);

        var pos = e.GetPosition(this).X / Bounds.Width;

        var vm = (DataContext as ArchiveReaderViewModel)!;
        switch (pos)
        {
            case > .6:
                if (vm.GoNextPageCommand.CanExecute(null))
                    vm.GoNextPageCommand.ExecuteAsync(null)
                        .ContinueWith(_ => Dispatcher.UIThread.Post(ScrollContainer.ScrollToHome));
                break;

            case < .3:
                if (vm.GoPreviousPageCommand.CanExecute(null))
                    vm.GoPreviousPageCommand.ExecuteAsync(null)
                        .ContinueWith(_ => Dispatcher.UIThread.Post(ScrollContainer.ScrollToHome));
                break;
        }
    }

    protected override async void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        await (DataContext as ArchiveReaderViewModel)!.EnsureInitializedAsync();
    }
}