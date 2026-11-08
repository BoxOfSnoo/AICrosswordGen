using Avalonia.Controls;

namespace AICrosswordGen.Views;

public partial class ConfirmWindow : Window
{
    public bool Accepted { get; private set; }

    public bool ThirdAccepted { get; private set; }

    public ConfirmWindow()
    {
        InitializeComponent();
    }

    public ConfirmWindow(string message)
    {
        InitializeComponent();
        Msg.Text = message;
    }

    public ConfirmWindow(string message, string okLabel, string cancelLabel)
    {
        InitializeComponent();
        Msg.Text = message;
        OkButton.Content = okLabel;
        CancelButton.Content = cancelLabel;
    }

    public ConfirmWindow(string message, string okLabel, string cancelLabel, string thirdLabel)
    {
        InitializeComponent();
        Msg.Text = message;
        OkButton.Content = okLabel;
        CancelButton.Content = cancelLabel;
        ThirdButton.Content = thirdLabel;
        ThirdButton.IsVisible = true;
    }

    private void Ok_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Accepted = true;
        Close();
    }

    private void Third_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        ThirdAccepted = true;
        Close();
    }

    private void Cancel_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Close();
    }
}
