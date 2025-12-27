using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using MangaMan.ViewModels;

namespace MangaMan.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private void TabPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        e.Pointer.Capture(sender as IInputElement);
    }

    private void TabPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (e.InitialPressMouseButton != MouseButton.Middle || !ReferenceEquals(sender, e.Pointer.Captured))
            return;

        var vm = (DataContext as MainWindowViewModel)!;
        vm.CloseTab(((sender as Control)!.DataContext as ViewModelBase)!);
    }
}