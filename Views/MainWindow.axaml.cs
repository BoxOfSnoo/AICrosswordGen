using AICrosswordGen.Services;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;

namespace AICrosswordGen.Views;

public partial class MainWindow : Window
{
    private string[] _words = DemoWords.List;
    private SolveResult? _best;
    private AppSettings _settings = SettingsStore.Load();
    private bool _running;
    private List<string> _across = new();
    private List<string> _down = new();

    public MainWindow()
    {
        InitializeComponent();
        var saved = _settings.Theme;
        var variant = saved switch
        {
            "Dark" => Avalonia.Styling.ThemeVariant.Dark,
            "Light" => Avalonia.Styling.ThemeVariant.Light,
            _ => Avalonia.Styling.ThemeVariant.Default
        };
        SetTheme(variant);
        Loaded += async (_, _) =>
        {
            if (!File.Exists("words.txt")) return;
            try
            {
                var lines = (await File.ReadAllLinesAsync("words.txt")).Where(w => !string.IsNullOrWhiteSpace(w)).ToArray();
                if (lines.Length > 0) { _words = lines; SetStatus($"Loaded words.txt ({lines.Length} words)"); }
            }
            catch (Exception ex) { SetStatus($"words.txt error: {ex.Message}"); }
        };
    }

    private void SetStatus(string s) => StatusText.Text = s;

    private void RefreshClueLists()
    {
        AcrossList.ItemsSource = _across.Select(s => MakeClueRow(s)).ToList();
        DownList.ItemsSource = _down.Select(s => MakeClueRow(s)).ToList();
    }

    private Control MakeClueRow(string clue)
    {
        var word = clue.Contains(". ") ? clue[(clue.IndexOf(". ") + 2)..] : clue;
        var row = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto")
        };
        var label = new TextBlock
        {
            Text = clue
        };
        var btn = new Button
        {
            Content = "X",
            Margin = new Avalonia.Thickness(4, 0, 0, 0)
        };
        btn.Click += (_, _) => RemoveWord(word);
        Grid.SetColumn(label, 0);
        Grid.SetColumn(btn, 1);
        row.Children.Add(label);
        row.Children.Add(btn);
        return row;
    }

    private void RemoveWord(string word)
    {
        try
        {
            var w = word.Trim().ToUpperInvariant();

            if (w.Length == 0)
            {
                return;
            }

            SettingsStore.AddBlocklist(w);

            _words = _words.Where(x => !string.Equals(x, w, StringComparison.OrdinalIgnoreCase)).ToArray();
            _across = _across.Where(s => !s.EndsWith(" " + w, StringComparison.OrdinalIgnoreCase)).ToList();
            _down = _down.Where(s => !s.EndsWith(" " + w, StringComparison.OrdinalIgnoreCase)).ToList();
            RefreshClueLists();
            Board.Children.Clear();
            _best = null;
            BoardOverlay.IsVisible = true;
            SetStatus($"Removed {w} - generate again");
        }
        catch (Exception ex)
        {
            SetStatus($"Remove failed: {ex.Message}");
        }
    }

    private async void Generate_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_running) return;
        var blocked = SettingsStore.LoadBlocklist();
        var words = _words.Where(w => !blocked.Contains(w.Trim().ToUpperInvariant())).ToArray();
        if (words.Length == 0) { SetStatus("No words - load a word list first"); return; }
        _words = words;
        _running = true;
        SetStatus("Generating...");
        ProgressBar.IsVisible = true;
        ProgressText.IsVisible = true;
        ProgressBar.Maximum = Math.Max(1, _settings.Iterations);
        ProgressBar.Value = 0;
        ProgressText.Text = $"0/{_settings.Iterations}";
        try
        {
            await Task.Run(() => _best = PuzzleService.Generate(_words, _settings, (done, total) =>
            {
                Dispatcher.UIThread.Post(() =>
                {
                    ProgressBar.Maximum = Math.Max(1, total);
                    ProgressBar.Value = done;
                    ProgressText.Text = $"{done}/{total}";
                });
            }));
            Render();
        }
        finally
        {
            ProgressBar.IsVisible = false;
            ProgressText.IsVisible = false;
            _running = false;
        }
    }

    private void Render()
    {
        if (_best == null || _best.Grid.Cells.Count == 0) { SetStatus("No puzzle - use Generate"); return; }
        BoardOverlay.IsVisible = false;
        var (numbers, across, down) = PuzzleClues.Get(_best.Grid);
        _across = across;
        _down = down;
        RefreshClueLists();
        var (minR, maxR, minC, maxC) = _best.Grid.Bounds();
        int w = maxC - minC + 1, h = maxR - minR + 1;
        Board.Children.Clear();
        Board.ColumnDefinitions = new ColumnDefinitions(string.Join(",", Enumerable.Repeat("28", w)));
        Board.RowDefinitions = new RowDefinitions(string.Join(",", Enumerable.Repeat("28", h)));
        for (int r = 0; r < h; r++)
            for (int c = 0; c < w; c++)
            {
                bool white = _best.Grid.Cells.ContainsKey((minR + r, minC + c));
                var cell = new Border
                {
                    Background = white ? Brushes.White : Brushes.Black,
                    BorderBrush = Brushes.Black,
                    BorderThickness = new Avalonia.Thickness(2)
                };
                int number = numbers[r, c];
                string letter = _best.Grid.Cells.TryGetValue((minR + r, minC + c), out char ch) ? ch.ToString() : "";
                var panel = new Grid();
                if (number > 0)
                    panel.Children.Add(new TextBlock
                    {
                        Text = number.ToString(),
                        FontSize = 7,
                        Margin = new Avalonia.Thickness(1, 0, 0, 0)
                    });
                if (white)
                    panel.Children.Add(new TextBlock
                    {
                        Text = letter,
                        FontSize = 13
                    });
                cell.Child = panel;
                Grid.SetRow(cell, r);
                Grid.SetColumn(cell, c);
                Board.Children.Add(cell);
            }
        SetStatus($"Size: {w}x{h}, density: {_best.Density:P1}, used: {_best.Used.Count}, unused: {_best.Unused.Count}");
    }

    private async void LoadWordList_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Load Word List",
            AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType("Text") { Patterns = ["*.txt"] }]
        });
        if (files.Count == 0) return;
        var lines = await File.ReadAllLinesAsync(files[0].Path.LocalPath);
        var words = lines.Where(w => !string.IsNullOrWhiteSpace(w)).ToArray();
        if (words.Length == 0) { SetStatus("Word list empty - not loaded"); return; }
        if (words.Length > 2000) { SetStatus($"Too big ({words.Length} words, max 2000) - not loaded"); return; }
        _words = words;
        SetStatus($"Loaded {files[0].Name} ({_words.Length} words)");
    }

    private async Task<string?> PickSaveAsync(string ext)
    {
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save",
            SuggestedFileName = "crossword" + ext,
            FileTypeChoices = [new FilePickerFileType(ext.TrimStart('.')) { Patterns = ["*" + ext] }]
        });
        var path = file?.Path.LocalPath;
        if (path != null && File.Exists(path))
        {
            var dlg = new ConfirmWindow($"Overwrite {path}?");
            await dlg.ShowDialog(this);
            if (!dlg.Accepted) { SetStatus("Save cancelled"); return null; }
        }
        return path;
    }

    private async void SavePuz_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_best == null || _best.Grid.Cells.Count == 0) { SetStatus("Generate a puzzle first"); return; }
        var path = await PickSaveAsync(".puz");
        if (path == null) return;
        PuzWriter.Write(_best.Grid, path, title: "AICrosswordGen");
        SetStatus($"Saved {path}");
    }

    private async void SavePdf_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_best == null || _best.Grid.Cells.Count == 0) { SetStatus("Generate a puzzle first"); return; }
        var path = await PickSaveAsync(".pdf");
        if (path == null) return;
        PdfWriter.WriteBlank(_best.Grid, path, title: "AICrosswordGen");
        SetStatus($"Saved {path}");
    }

    private async void SaveText_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_best == null || _best.Grid.Cells.Count == 0) { SetStatus("Generate a puzzle first"); return; }
        var path = await PickSaveAsync(".txt");
        if (path == null) return;
        await File.WriteAllTextAsync(path, AcrossTextWriter.Text(_best.Grid, title: "AICrosswordGen"));
        SetStatus($"Saved {path}");
    }

    private void Quit_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => Close();

    private async void Options_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var win = new OptionsWindow(_settings);
        await win.ShowDialog(this);
        if (win.Accepted)
        {
            _settings = win.Settings;
            SettingsStore.Save(_settings);
        }
    }

    private async void About_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        await new AboutWindow().ShowDialog(this);
    }

    private void SetTheme(Avalonia.Styling.ThemeVariant variant)
    {
        if (Avalonia.Application.Current != null)
        {
            Avalonia.Application.Current.RequestedThemeVariant = variant;
        }
        DarkItem.Header = variant == Avalonia.Styling.ThemeVariant.Dark ? "✓ Dark" : "_Dark";
        LightItem.Header = variant == Avalonia.Styling.ThemeVariant.Light ? "✓ Light" : "_Light";
        SystemItem.Header = variant == Avalonia.Styling.ThemeVariant.Default ? "✓ System" : "_System";
        var name = variant == Avalonia.Styling.ThemeVariant.Dark ? "Dark" : variant == Avalonia.Styling.ThemeVariant.Light ? "Light" : "System";
        if (_settings.Theme != name)
        {
            _settings = _settings with { Theme = name };
            SettingsStore.Save(_settings);
        }
    }

    private void ThemeDark_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        SetTheme(Avalonia.Styling.ThemeVariant.Dark);
    }

    private void ThemeLight_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        SetTheme(Avalonia.Styling.ThemeVariant.Light);
    }

    private void ThemeSystem_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        SetTheme(Avalonia.Styling.ThemeVariant.Default);
    }
}
