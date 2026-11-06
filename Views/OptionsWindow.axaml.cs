using AICrosswordGen.Services;
using Avalonia.Controls;

namespace AICrosswordGen.Views;

public partial class OptionsWindow : Window
{
    public AppSettings Settings { get; private set; } = new();
    public bool Accepted { get; private set; }

    public OptionsWindow() : this(new AppSettings()) { }

    public OptionsWindow(AppSettings current)
    {
        InitializeComponent();
        IterationsBox.Value = current.Iterations;
        SeedBox.Value = current.Seed;
        FillerBox.IsChecked = current.UseFiller;
    }

    private void Ok_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Settings = new AppSettings(
            Iterations: (int)(IterationsBox.Value ?? 30),
            Seed: (int)(SeedBox.Value ?? 42),
            UseFiller: FillerBox.IsChecked ?? true);
        Accepted = true;
        Close();
    }

    private void Cancel_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => Close();
}
