using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
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
                {
                    vm.GoNextPageCommand.Execute(null);
                    ScrollContainer.ScrollToHome();
                }
                break;

            case < .3:
                if (vm.GoPreviousPageCommand.CanExecute(null))
                {
                    vm.GoPreviousPageCommand.Execute(null);
                    ScrollContainer.ScrollToHome();
                }
                break;
        }
    }
}