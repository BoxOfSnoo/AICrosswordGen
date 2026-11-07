using Avalonia.Controls;

namespace AICrosswordGen.Views;

public partial class ClueWindow : Window
{
    public string ClueText { get; private set; } = "";

    public bool Accepted { get; private set; }

    public ClueWindow()
    {
        InitializeComponent();
    }

    public ClueWindow(string word, string? clue)
    {
        InitializeComponent();
        WordLabel.Text = word;
        ClueBox.Text = clue ?? "";
    }

    private void Save_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        ClueText = (ClueBox.Text ?? "").Trim();
        Accepted = true;
        Close();
    }

    private void Cancel_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Close();
    }
}
