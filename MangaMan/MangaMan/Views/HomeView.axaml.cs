using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using MangaMan.ViewModels;

namespace MangaMan.Views;

public partial class HomeView : UserControl
{
    public HomeView()
    {
        InitializeComponent();
    }

    private void ArchivePointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(null).Properties.IsLeftButtonPressed && e.ClickCount == 2)
        {
            ((sender as Control)!.DataContext as ArchiveViewModel).OpenArchiveCommand.Execute(null);
        }
    }
}