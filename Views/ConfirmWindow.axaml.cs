using Avalonia.Controls;

namespace AICrosswordGen.Views;

public partial class ConfirmWindow : Window
{
    public bool Accepted { get; private set; }

    public ConfirmWindow()
    {
        InitializeComponent();
    }

    public ConfirmWindow(string message)
    {
        InitializeComponent();
        Msg.Text = message;
    }

    private void Ok_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Accepted = true;
        Close();
    }

    private void Cancel_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Close();
    }
}
