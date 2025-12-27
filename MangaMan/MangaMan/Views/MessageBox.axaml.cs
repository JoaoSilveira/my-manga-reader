using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Chrome;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace MangaMan.Views;

public enum MessageBoxButtons
{
    Ok,
    OkCancel,
    YesNo,
    YesNoCancel,
    ConfirmCancel,
}

public enum MessageBoxIcon
{
    None,
    Information,
    Warning,
    Error,
    Stop,
    Question,
}

public partial class MessageBox : Window
{
    public required string Message { get; init; }
    public MessageBoxButtons Buttons { get; init; } = MessageBoxButtons.Ok;
    public MessageBoxIcon IconType { get; init; } = MessageBoxIcon.None;

    public bool? Result { get; private set; } = null;

    public string PositiveText => Buttons switch
    {
        MessageBoxButtons.Ok or MessageBoxButtons.OkCancel => "Ok",
        MessageBoxButtons.YesNo or MessageBoxButtons.YesNoCancel => "Yes",
        MessageBoxButtons.ConfirmCancel => "Confirm",
        _ => throw new ArgumentOutOfRangeException()
    };

    public bool NegativeVisible => Buttons switch
    {
        MessageBoxButtons.Ok or MessageBoxButtons.OkCancel or MessageBoxButtons.ConfirmCancel => false,
        MessageBoxButtons.YesNo or MessageBoxButtons.YesNoCancel => true,
        _ => throw new ArgumentOutOfRangeException()
    };

    public bool CancelVisible => Buttons switch
    {
        MessageBoxButtons.YesNoCancel or MessageBoxButtons.OkCancel or MessageBoxButtons.ConfirmCancel => true,
        MessageBoxButtons.YesNo or MessageBoxButtons.Ok => false,
        _ => throw new ArgumentOutOfRangeException()
    };

    private MessageBox()
    {
        // InitializeComponent();
    }

    public static async Task<bool?> Show(Window? owner, string title, string message,
        MessageBoxButtons buttons = MessageBoxButtons.Ok, MessageBoxIcon icon = MessageBoxIcon.None)
    {
        if (Application.Current!.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
            return null;

        var self = new MessageBox()
        {
            Title = title,
            Message = message,
            Buttons = buttons,
            IconType = icon,
        };
        
        self.InitializeComponent();

        return await self.ShowDialog<bool?>(owner ?? desktop.MainWindow!);
    }

    private void DialogAction(object? sender, RoutedEventArgs e)
    {
        Result = (sender as Control)!.Tag as bool?;
        Close(Result);
    }
}