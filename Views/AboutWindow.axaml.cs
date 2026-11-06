using Avalonia.Controls;

namespace AICrosswordGen.Views;

public partial class AboutWindow : Window
{
    public AboutWindow() => InitializeComponent();
    private void Ok_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => Close();
}
