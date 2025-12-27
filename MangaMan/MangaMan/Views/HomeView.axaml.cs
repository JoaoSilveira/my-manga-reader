using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
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
        if (!e.GetCurrentPoint(null).Properties.IsLeftButtonPressed || e.ClickCount != 2)
            return;

        var vm = ((sender as Control)!.DataContext as ArchiveViewModel)!;
        vm.OpenArchiveCommand.Execute(null);
    }

    protected override async void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        await (DataContext as HomeViewModel)!.Initialize();
    }
}