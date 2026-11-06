using AICrosswordGen.Services;
using Avalonia.Controls;

namespace AICrosswordGen.Views;

public partial class OptionsWindow : Window
{
    public AppSettings Settings { get; private set; } = new();

    public bool Accepted { get; private set; }

    public event Action<AppSettings>? SettingsChanged;

    public OptionsWindow() : this(new AppSettings()) { }

    public OptionsWindow(AppSettings current)
    {
        Settings = current;

        InitializeComponent();

        IterationsBox.Value = current.Iterations;
        SeedBox.Value = current.Seed;
        FillerBox.IsChecked = current.UseFiller;

        // apply instantly on change (after initial load so we don't fire spuriously)
        IterationsBox.ValueChanged += OnNumericChanged;
        SeedBox.ValueChanged += OnNumericChanged;
        FillerBox.IsCheckedChanged += OnFillerChanged;
    }

    private void OnNumericChanged(object? sender, NumericUpDownValueChangedEventArgs e)
    {
        ApplyLive();
    }

    private void OnFillerChanged(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        ApplyLive();
    }

    private void ApplyLive()
    {
        Settings = Settings with
        {
            Iterations = (int)(IterationsBox.Value ?? Settings.Iterations),
            Seed = (int)(SeedBox.Value ?? Settings.Seed),
            UseFiller = FillerBox.IsChecked ?? Settings.UseFiller
        };

        Accepted = true;

        SettingsChanged?.Invoke(Settings);
    }

    private void Ok_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        ApplyLive();

        Close();
    }
}
