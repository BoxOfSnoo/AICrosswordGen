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

    private int _progressDone;

    private int _progressTotal;

    public MainWindow()
    {
        InitializeComponent();

        var saved = _settings.Theme;

        Avalonia.Styling.ThemeVariant variant;

        if (saved == "Dark")
        {
            variant = Avalonia.Styling.ThemeVariant.Dark;
        }
        else
        {
            if (saved == "Light")
            {
                variant = Avalonia.Styling.ThemeVariant.Light;
            }
            else
            {
                variant = Avalonia.Styling.ThemeVariant.Default;
            }
        }

        SetTheme(variant);

        Loaded += OnLoaded;
    }

    private async void OnLoaded(object? sender, EventArgs e)
    {
        if (!File.Exists("words.txt"))
        {
            return;
        }

        try
        {
            var raw = await File.ReadAllLinesAsync("words.txt");

            var lines = new List<string>();

            foreach (var w in raw)
            {
                if (!string.IsNullOrWhiteSpace(w))
                {
                    lines.Add(w);
                }
            }

            if (lines.Count > 0)
            {
                _words = lines.ToArray();

                SetStatus($"Loaded words.txt ({lines.Count} words)");
            }
        }
        catch (Exception ex)
        {
            SetStatus($"words.txt error: {ex.Message}");
        }
    }

    private void SetStatus(string s)
    {
        StatusText.Text = s;
    }

    private void RefreshClueLists()
    {
        var acrossRows = new List<Control>();

        foreach (var s in _across)
        {
            acrossRows.Add(MakeClueRow(s));
        }

        AcrossList.ItemsSource = acrossRows;

        var downRows = new List<Control>();

        foreach (var s in _down)
        {
            downRows.Add(MakeClueRow(s));
        }

        DownList.ItemsSource = downRows;
    }

    private Control MakeClueRow(string clue)
    {
        var word = clue;

        if (clue.Contains(". "))
        {
            word = clue[(clue.IndexOf(". ") + 2)..];
        }

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
            Tag = word,
            Margin = new Avalonia.Thickness(4, 0, 0, 0)
        };

        btn.Click += OnRemoveWord;

        Grid.SetColumn(label, 0);
        Grid.SetColumn(btn, 1);

        row.Children.Add(label);
        row.Children.Add(btn);

        return row;
    }

    private void OnRemoveWord(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string word)
        {
            RemoveWord(word);
        }
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

            var keptWords = new List<string>();

            foreach (var x in _words)
            {
                if (!string.Equals(x, w, StringComparison.OrdinalIgnoreCase))
                {
                    keptWords.Add(x);
                }
            }

            _words = keptWords.ToArray();

            var keptAcross = new List<string>();

            foreach (var s in _across)
            {
                if (!s.EndsWith(" " + w, StringComparison.OrdinalIgnoreCase))
                {
                    keptAcross.Add(s);
                }
            }

            _across = keptAcross;

            var keptDown = new List<string>();

            foreach (var s in _down)
            {
                if (!s.EndsWith(" " + w, StringComparison.OrdinalIgnoreCase))
                {
                    keptDown.Add(s);
                }
            }

            _down = keptDown;

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

    private SolveResult GeneratePuzzle()
    {
        return PuzzleService.Generate(_words, _settings, ReportProgress);
    }

    private void ReportProgress(int done, int total)
    {
        _progressDone = done;
        _progressTotal = total;

        Dispatcher.UIThread.Post(UpdateProgress);
    }

    private void UpdateProgress()
    {
        ProgressBar.Maximum = Math.Max(1, _progressTotal);
        ProgressBar.Value = _progressDone;
        ProgressText.Text = $"{_progressDone}/{_progressTotal}";
    }

    private async void Generate_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_running)
        {
            return;
        }

        // fresh seed each Generate so repeat clicks don't repeat same puzzle
        _settings = _settings with { Seed = (_settings.Seed + 1) % 100000 };

        SettingsStore.Save(_settings);

        var blocked = SettingsStore.LoadBlocklist();

        var kept = new List<string>();

        foreach (var w in _words)
        {
            if (!blocked.Contains(w.Trim().ToUpperInvariant()))
            {
                kept.Add(w);
            }
        }

        var words = kept.ToArray();

        if (words.Length == 0)
        {
            SetStatus("No words - load a word list first");

            return;
        }

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
            _best = await Task.Run(GeneratePuzzle);

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
        if (_best == null || _best.Grid.Cells.Count == 0)
        {
            SetStatus("No puzzle - use Generate");

            return;
        }

        BoardOverlay.IsVisible = false;

        var (numbers, across, down) = PuzzleClues.Get(_best.Grid);

        _across = across;
        _down = down;

        RefreshClueLists();

        var (minR, maxR, minC, maxC) = _best.Grid.Bounds();

        int w = maxC - minC + 1;
        int h = maxR - minR + 1;

        Board.Children.Clear();

        Board.ColumnDefinitions = new ColumnDefinitions(string.Join(",", Enumerable.Repeat("28", w)));
        Board.RowDefinitions = new RowDefinitions(string.Join(",", Enumerable.Repeat("28", h)));

        for (int r = 0; r < h; r++)
        {
            for (int c = 0; c < w; c++)
            {
                bool white = _best.Grid.Cells.ContainsKey((minR + r, minC + c));

                var cell = new Border
                {
                    Background = white ? Brushes.White : Brushes.Black,
                    BorderBrush = Brushes.Black,
                    BorderThickness = new Avalonia.Thickness(1)
                };

                int number = numbers[r, c];

                string letter = _best.Grid.Cells.TryGetValue((minR + r, minC + c), out char ch) ? ch.ToString() : "";

                var panel = new Grid();

                if (number > 0)
                {
                    panel.Children.Add(new TextBlock
                    {
                        Text = number.ToString(),
                        FontSize = 7,
                        Foreground = Brushes.Black,
                        Margin = new Avalonia.Thickness(1, 0, 0, 0)
                    });
                }

                if (white)
                {
                    panel.Children.Add(new TextBlock
                    {
                        Text = letter,
                        FontSize = 13,
                        Foreground = Brushes.Black,
                        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                        VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
                    });
                }

                cell.Child = panel;

                Grid.SetRow(cell, r);
                Grid.SetColumn(cell, c);

                Board.Children.Add(cell);
            }
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

        if (files.Count == 0)
        {
            return;
        }

        var lines = await File.ReadAllLinesAsync(files[0].Path.LocalPath);

        var wordsList = new List<string>();

        foreach (var w in lines)
        {
            if (!string.IsNullOrWhiteSpace(w))
            {
                wordsList.Add(w);
            }
        }

        var words = wordsList.ToArray();

        if (words.Length == 0)
        {
            SetStatus("Word list empty - not loaded");

            return;
        }

        if (words.Length > 2000)
        {
            SetStatus($"Too big ({words.Length} words, max 2000) - not loaded");

            return;
        }

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

            if (!dlg.Accepted)
            {
                SetStatus("Save cancelled");

                return null;
            }
        }

        return path;
    }

    private async void SavePuz_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_best == null || _best.Grid.Cells.Count == 0)
        {
            SetStatus("Generate a puzzle first");

            return;
        }

        var path = await PickSaveAsync(".puz");

        if (path == null)
        {
            return;
        }

        PuzWriter.Write(_best.Grid, path, title: "AICrosswordGen");

        SetStatus($"Saved {path}");
    }

    private async void SavePdf_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_best == null || _best.Grid.Cells.Count == 0)
        {
            SetStatus("Generate a puzzle first");

            return;
        }

        var path = await PickSaveAsync(".pdf");

        if (path == null)
        {
            return;
        }

        PdfWriter.WriteBlank(_best.Grid, path, title: "AICrosswordGen");

        SetStatus($"Saved {path}");
    }

    private async void SaveText_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_best == null || _best.Grid.Cells.Count == 0)
        {
            SetStatus("Generate a puzzle first");

            return;
        }

        var path = await PickSaveAsync(".txt");

        if (path == null)
        {
            return;
        }

        await File.WriteAllTextAsync(path, AcrossTextWriter.Text(_best.Grid, title: "AICrosswordGen"));

        SetStatus($"Saved {path}");
    }

    private void Quit_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Close();
    }

    private void OnSettingsChanged(AppSettings s)
    {
        _settings = s;

        SettingsStore.Save(_settings);
    }

    private async void Options_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var win = new OptionsWindow(_settings);

        win.SettingsChanged += OnSettingsChanged;

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
