using Avalonia.Controls;
using System.Diagnostics;

namespace AICrosswordGen.Views;

public partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();
    }

    private void Ok_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Close();
    }

    private void RepoLink_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo("https://github.com/BoxOfSnoo/AICrosswordGen") { UseShellExecute = true });
        }
        catch
        {
        }
    }
}
