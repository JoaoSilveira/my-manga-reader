using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using CommunityToolkit.Mvvm.Input;
using MangaMan.ViewModels;

namespace MangaMan.Views;

public partial class HomeView : UserControl
{
    public HomeView()
    {
        InitializeComponent();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e is { Key: Key.F, KeyModifiers: KeyModifiers.Control })
            FilterTextBox.Focus();
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