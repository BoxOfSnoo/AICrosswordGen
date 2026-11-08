using AICrosswordGen.Services;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;

namespace AICrosswordGen.Views;

public partial class MainWindow : Window
{
    private string[] words = DemoWords.List;

    private SolveResult? best;

    private AppSettings settings = SettingsStore.Load();

    private bool running;

    private List<string> across = new();

    private List<string> down = new();

    private int progressDone;

    private int progressTotal;

    private string? inProgressPath;

    private bool hasEdits;

    private string wordListName = "";

    private bool settingTitle;

    private DispatcherTimer? titleSaveTimer;

    private List<string?> acrossClues = new();

    private List<string?> downClues = new();

    public MainWindow()
    {
        InitializeComponent();

        var saved = this.settings.Theme;

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

        SetDefaultTitle();

        this.titleSaveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        this.titleSaveTimer.Tick += (_, _) =>
        {
            this.titleSaveTimer?.Stop();
            SaveInProgress();
        };

        ActualThemeVariantChanged += (_, _) => UpdateGridBackground();

        Loaded += OnLoaded;
    }

    private string DefaultTitle()
    {
        var name = Path.GetFileNameWithoutExtension(this.wordListName).Replace('_', ' ').Replace('-', ' ').Trim();

        if (name.Length == 0)
        {
            return DateTime.Now.ToString("yyyy-MM-dd");
        }

        name = name.ToLowerInvariant();
        name = char.ToUpperInvariant(name[0]) + name[1..];

        return $"{name} {DateTime.Now:yyyy-MM-dd}";
    }

    private string CurrentTitle()
    {
        return string.IsNullOrWhiteSpace(TitleBox.Text) ? DefaultTitle() : TitleBox.Text.Trim();
    }

    private void SetDefaultTitle()
    {
        this.settingTitle = true;

        try
        {
            TitleBox.Text = DefaultTitle();
        }
        finally
        {
            this.settingTitle = false;
        }
    }

    private void TitleBox_TextChanged(object? sender, TextChangedEventArgs e)
    {
        if (this.settingTitle)
        {
            return;
        }

        if (this.best != null && this.best.Grid.Cells.Count > 0)
        {
            this.hasEdits = true;
            this.titleSaveTimer?.Stop();
            this.titleSaveTimer?.Start();
        }
    }

    private async void OnLoaded(object? sender, EventArgs e)
    {
        RefreshRecentMenu();

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
                this.words = lines.ToArray();
                this.wordListName = "words.txt";

                SetDefaultTitle();

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

    private void EnsureClueLists()
    {
        if (this.acrossClues.Count != this.across.Count)
        {
            this.acrossClues = new List<string?>(new string?[this.across.Count]);
        }

        if (this.downClues.Count != this.down.Count)
        {
            this.downClues = new List<string?>(new string?[this.down.Count]);
        }
    }

    private Dictionary<string, string> ClueMap()
    {
        EnsureClueLists();

        return PuzzleClues.ClueMap(this.across, this.acrossClues, this.down, this.downClues);
    }

    private void RefreshClueLists()
    {
        EnsureClueLists();

        var acrossRows = new List<Control>();

        for (int i = 0; i < this.across.Count; i++)
        {
            acrossRows.Add(MakeClueRow(this.across[i], true, i, this.acrossClues[i]));
        }

        AcrossList.ItemsSource = acrossRows;

        var downRows = new List<Control>();

        for (int i = 0; i < this.down.Count; i++)
        {
            downRows.Add(MakeClueRow(this.down[i], false, i, this.downClues[i]));
        }

        DownList.ItemsSource = downRows;
    }

    private Control MakeClueRow(string entry, bool isAcross, int index, string? clue)
    {
        var word = PuzzleClues.WordOf(entry);

        var row = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto")
        };

        var label = new TextBlock
        {
            Text = string.IsNullOrWhiteSpace(clue) ? entry : $"{entry} \u2013 {clue}",
            TextWrapping = Avalonia.Media.TextWrapping.Wrap
        };

        var pencil = new Avalonia.Controls.Shapes.Path
        {
            Data = Avalonia.Media.Geometry.Parse("M3 17.25V21h3.75L17.81 9.94l-3.75-3.75L3 17.25z M20.71 7.04c.39-.39.39-1.02 0-1.41l-2.34-2.34a.996.996 0 0 0-1.41 0l-1.83 1.83 3.75 3.75 1.83-1.83z"),
            Fill = string.IsNullOrWhiteSpace(clue) ? Brushes.White : Brushes.LimeGreen,
            Stretch = Avalonia.Media.Stretch.Uniform,
            Width = 14,
            Height = 14
        };

        var editBtn = new Button
        {
            Content = pencil,
            Tag = $"{(isAcross ? "A" : "D")}|{index}",
            Margin = new Avalonia.Thickness(4, 0, 0, 0),
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            Padding = new Avalonia.Thickness(4)
        };

        editBtn.Click += OnEditClue;

        var btn = new Button
        {
            Content = "X",
            Tag = word,
            Margin = new Avalonia.Thickness(4, 0, 0, 0)
        };

        btn.Click += OnRemoveWord;

        Grid.SetColumn(label, 0);
        Grid.SetColumn(editBtn, 1);
        Grid.SetColumn(btn, 2);

        row.Children.Add(label);
        row.Children.Add(editBtn);
        row.Children.Add(btn);

        return row;
    }

    private async void OnEditClue(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not string tag)
        {
            return;
        }

        var parts = tag.Split('|');

        if (parts.Length != 2 || !int.TryParse(parts[1], out int index))
        {
            return;
        }

        bool isAcross = parts[0] == "A";
        var list = isAcross ? this.across : this.down;
        var clues = isAcross ? this.acrossClues : this.downClues;

        if (index < 0 || index >= list.Count)
        {
            return;
        }

        var entry = list[index];
        var word = PuzzleClues.WordOf(entry);
        var current = index < clues.Count ? clues[index] : null;

        var win = new ClueWindow(word, current);

        await win.ShowDialog(this);

        if (!win.Accepted)
        {
            return;
        }

        EnsureClueLists();
        clues[index] = string.IsNullOrWhiteSpace(win.ClueText) ? null : win.ClueText;
        this.hasEdits = true;

        bool saved = SaveInProgress();

        RefreshClueLists();

        if (saved)
        {
            SetStatus(string.IsNullOrWhiteSpace(win.ClueText) ? $"Cleared clue for {word}" : $"Clue saved for {word}");
        }
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

            foreach (var x in this.words)
            {
                if (!string.Equals(x, w, StringComparison.OrdinalIgnoreCase))
                {
                    keptWords.Add(x);
                }
            }

            this.words = keptWords.ToArray();

            EnsureClueLists();

            var keptAcross = new List<string>();
            var keptAcrossClues = new List<string?>();

            for (int i = 0; i < this.across.Count; i++)
            {
                if (!this.across[i].EndsWith(" " + w, StringComparison.OrdinalIgnoreCase))
                {
                    keptAcross.Add(this.across[i]);
                    keptAcrossClues.Add(this.acrossClues[i]);
                }
            }

            this.across = keptAcross;
            this.acrossClues = keptAcrossClues;

            var keptDown = new List<string>();
            var keptDownClues = new List<string?>();

            for (int i = 0; i < this.down.Count; i++)
            {
                if (!this.down[i].EndsWith(" " + w, StringComparison.OrdinalIgnoreCase))
                {
                    keptDown.Add(this.down[i]);
                    keptDownClues.Add(this.downClues[i]);
                }
            }

            this.down = keptDown;
            this.downClues = keptDownClues;

            if (this.inProgressPath != null)
            {
                InProgressStore.Delete(this.inProgressPath);
            }

            this.inProgressPath = null;
            this.hasEdits = false;
            this.titleSaveTimer?.Stop();

            RefreshClueLists();

            Board.Children.Clear();

            this.best = null;

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
        return PuzzleService.Generate(this.words, this.settings, ReportProgress);
    }

    private bool SaveInProgress()
    {
        if (this.best == null || this.best.Grid.Cells.Count == 0)
        {
            return false;
        }

        try
        {
            EnsureClueLists();

            if (this.inProgressPath == null)
            {
                this.inProgressPath = InProgressStore.CreatePath();
            }

            File.WriteAllText(this.inProgressPath, AcrossTextWriter.Text(this.best.Grid, title: CurrentTitle(), acrossClues: this.acrossClues, downClues: this.downClues));
            InProgressStore.TouchMru(this.inProgressPath);
            RefreshRecentMenu();

            return true;
        }
        catch (Exception ex)
        {
            SetStatus($"Save in progress failed: {ex.Message}");

            return false;
        }
    }

    private void DiscardInProgress()
    {
        this.titleSaveTimer?.Stop();

        if (this.inProgressPath != null)
        {
            InProgressStore.Delete(this.inProgressPath);
        }

        this.inProgressPath = null;
        this.hasEdits = false;
        RefreshRecentMenu();
    }

    private void RefreshRecentMenu()
    {
        if (RecentMenu == null)
        {
            return;
        }

        RecentMenu.Items.Clear();

        var mru = InProgressStore.LoadMru();

        if (mru.Count == 0)
        {
            RecentMenu.Items.Add(new MenuItem { Header = "No puzzles in progress", IsEnabled = false });
            return;
        }

        foreach (var p in mru)
        {
            var item = new MenuItem { Header = Path.GetFileName(p) };

            item.Click += (s, e) => LoadInProgress(p);

            RecentMenu.Items.Add(item);
        }
    }

    private async void OpenInProgress_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open Puzzle in Progress",
            AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType("Text") { Patterns = ["*.txt"] }]
        });

        if (files.Count == 0)
        {
            return;
        }

        LoadInProgress(files[0].Path.LocalPath);
    }

    private void LoadInProgress(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                SetStatus($"Not found: {path}");
                RefreshRecentMenu();
                return;
            }

            var fullPath = Path.GetFullPath(path);
            var workFolder = Path.GetFullPath(InProgressStore.Folder);

            if (!fullPath.StartsWith(workFolder, StringComparison.OrdinalIgnoreCase))
            {
                var copy = InProgressStore.CreatePath();

                File.Copy(path, copy);
                path = copy;
            }

            var (grid, acrossClues, downClues, title) = InProgressStore.Read(path);

            if (grid.Cells.Count == 0)
            {
                SetStatus("Puzzle in progress empty - not loaded");
                return;
            }

            var (_, across, down) = PuzzleClues.Get(grid);
            var used = new List<string>();

            foreach (var s in across)
            {
                used.Add(PuzzleClues.WordOf(s));
            }

            foreach (var s in down)
            {
                used.Add(PuzzleClues.WordOf(s));
            }

            foreach (var w in used)
            {
                grid.Dictionary.Add(w);
            }

            double density = grid.Area() > 0 ? (double)grid.Cells.Count / grid.Area() : 0;

            this.best = new SolveResult(grid, used, new List<string>(), density, grid.Area(), 0);
            this.inProgressPath = path;
            this.hasEdits = true;

            Render();

            this.settingTitle = true;

            try
            {
                TitleBox.Text = string.IsNullOrWhiteSpace(title) ? DefaultTitle() : title;
            }
            finally
            {
                this.settingTitle = false;
            }

            // Render clears the clues, so reapply the loaded ones
            this.acrossClues = acrossClues;
            this.downClues = downClues;
            EnsureClueLists();
            RefreshClueLists();

            InProgressStore.TouchMru(path);
            RefreshRecentMenu();
            SetStatus($"Resumed {Path.GetFileName(path)}");
        }
        catch (Exception ex)
        {
            SetStatus($"Load in progress failed: {ex.Message}");
        }
    }

    private void ReportProgress(int done, int total)
    {
        this.progressDone = done;
        this.progressTotal = total;

        Dispatcher.UIThread.Post(UpdateProgress);
    }

    private void UpdateProgress()
    {
        ProgressBar.Maximum = Math.Max(1, this.progressTotal);
        ProgressBar.Value = this.progressDone;
        ProgressText.Text = $"{this.progressDone}/{this.progressTotal}";
    }

    private async void Generate_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (this.running)
        {
            return;
        }

        var keptClues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (this.best != null && this.best.Grid.Cells.Count > 0)
        {
            keptClues = ClueMap();

            if (keptClues.Count > 0)
            {
                var confirm = new ConfirmWindow($"Current puzzle will be regenerated. Keep the existing clues for words used in the new puzzle? ({keptClues.Count} clue(s))", "Keep clues", "Cancel", "Discard clues");

                await confirm.ShowDialog(this);

                if (confirm.ThirdAccepted)
                {
                    keptClues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                }
                else if (!confirm.Accepted)
                {
                    return;
                }
            }
            else if (this.hasEdits)
            {
                var confirm = new ConfirmWindow("Current puzzle will be discarded and a new one will be generated. Continue?", "Discard & Generate", "Cancel");

                await confirm.ShowDialog(this);

                if (!confirm.Accepted)
                {
                    return;
                }
            }

            DiscardInProgress();
        }
        else if (this.inProgressPath != null)
        {
            DiscardInProgress();
        }

        // fresh seed each Generate so repeat clicks don't repeat same puzzle
        this.settings = this.settings with { Seed = (this.settings.Seed + 1) % 100000 };

        SettingsStore.Save(this.settings);

        var blocked = SettingsStore.LoadBlocklist();

        var kept = new List<string>();

        foreach (var w in this.words)
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

        this.words = words;

        this.running = true;

        SetStatus("Generating...");

        ProgressBar.IsVisible = true;
        ProgressText.IsVisible = true;
        ProgressBar.Maximum = Math.Max(1, this.settings.Iterations);
        ProgressBar.Value = 0;
        ProgressText.Text = $"0/{this.settings.Iterations}";

        try
        {
            this.best = await Task.Run(GeneratePuzzle);

            Render(keptClues);
            this.hasEdits = false;
            SaveInProgress();
        }
        finally
        {
            ProgressBar.IsVisible = false;
            ProgressText.IsVisible = false;
            this.running = false;
        }
    }

    private void Render(Dictionary<string, string>? keepClues = null)
    {
        if (this.best == null || this.best.Grid.Cells.Count == 0)
        {
            SetStatus("No puzzle - use Generate");

            return;
        }

        BoardOverlay.IsVisible = false;

        var (numbers, across, down) = PuzzleClues.Get(this.best.Grid);

        this.across = across;
        this.down = down;
        this.acrossClues = new List<string?>(new string?[across.Count]);
        this.downClues = new List<string?>(new string?[down.Count]);

        if (keepClues != null && keepClues.Count > 0)
        {
            PuzzleClues.ApplyClues(keepClues, across, this.acrossClues, down, this.downClues);
        }

        RefreshClueLists();

        var (minR, maxR, minC, maxC) = this.best.Grid.Bounds();

        int w = maxC - minC + 1;
        int h = maxR - minR + 1;

        Board.Children.Clear();

        Board.ColumnDefinitions = new ColumnDefinitions(string.Join(",", Enumerable.Repeat("28", w)));
        Board.RowDefinitions = new RowDefinitions(string.Join(",", Enumerable.Repeat("28", h)));

        for (int r = 0; r < h; r++)
        {
            for (int c = 0; c < w; c++)
            {
                bool white = this.best.Grid.Cells.ContainsKey((minR + r, minC + c));

                var cell = new Border
                {
                    Background = white ? Brushes.White : Brushes.Black,
                    BorderBrush = Brushes.Black,
                    BorderThickness = new Avalonia.Thickness(1)
                };

                int number = numbers[r, c];

                string letter = this.best.Grid.Cells.TryGetValue((minR + r, minC + c), out char ch) ? ch.ToString() : "";

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

        SetStatus($"Size: {w}x{h}, density: {this.best.Density:P1}, used: {this.best.Used.Count}, unused: {this.best.Unused.Count}");
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

        this.words = words;
        this.wordListName = files[0].Name;

        SetDefaultTitle();

        SetStatus($"Loaded {files[0].Name} ({this.words.Length} words)");
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
        if (this.best == null || this.best.Grid.Cells.Count == 0)
        {
            SetStatus("Generate a puzzle first");

            return;
        }

        var path = await PickSaveAsync(".puz");

        if (path == null)
        {
            return;
        }

        var txtPath = Path.ChangeExtension(path, ".txt");

        if (File.Exists(txtPath))
        {
            var overwrite = new ConfirmWindow($"Overwrite {txtPath}?");

            await overwrite.ShowDialog(this);

            if (!overwrite.Accepted)
            {
                SetStatus("Save cancelled");
                return;
            }
        }

        EnsureClueLists();

        try
        {
            PuzWriter.Write(this.best.Grid, path, title: CurrentTitle(), acrossClues: this.acrossClues, downClues: this.downClues);
            await File.WriteAllTextAsync(txtPath, AcrossTextWriter.Text(this.best.Grid, title: CurrentTitle(), acrossClues: this.acrossClues, downClues: this.downClues));
        }
        catch (Exception ex)
        {
            SetStatus($"Save failed: {ex.Message}");
            return;
        }

        var confirm = new ConfirmWindow("Mark current puzzle as completed?", "Yes", "No");

        await confirm.ShowDialog(this);

        if (confirm.Accepted)
        {
            if (this.inProgressPath != null)
            {
                InProgressStore.Delete(this.inProgressPath);
            }

            this.inProgressPath = null;
            this.hasEdits = false;
            RefreshRecentMenu();
            SetStatus($"Saved {path} and {txtPath} (completed)");
        }
        else
        {
            SaveInProgress();
            SetStatus($"Saved {path} and {txtPath}");
        }
    }

    private async void SavePdf_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (this.best == null || this.best.Grid.Cells.Count == 0)
        {
            SetStatus("Generate a puzzle first");

            return;
        }

        var path = await PickSaveAsync(".pdf");

        if (path == null)
        {
            return;
        }

        EnsureClueLists();

        try
        {
            PdfWriter.WriteBlank(this.best.Grid, path, title: CurrentTitle(), acrossClues: this.acrossClues, downClues: this.downClues);
        }
        catch (Exception ex)
        {
            SetStatus($"Save failed: {ex.Message}");
            return;
        }

        SetStatus($"Saved {path}");
    }

    private async void SaveText_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (this.best == null || this.best.Grid.Cells.Count == 0)
        {
            SetStatus("Generate a puzzle first");

            return;
        }

        var path = await PickSaveAsync(".txt");

        if (path == null)
        {
            return;
        }

        EnsureClueLists();

        try
        {
            await File.WriteAllTextAsync(path, AcrossTextWriter.Text(this.best.Grid, title: CurrentTitle(), acrossClues: this.acrossClues, downClues: this.downClues));
        }
        catch (Exception ex)
        {
            SetStatus($"Save failed: {ex.Message}");
            return;
        }

        SetStatus($"Saved {path}");
    }

    private void Quit_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        this.titleSaveTimer?.Stop();

        if (this.hasEdits && this.best != null && this.best.Grid.Cells.Count > 0)
        {
            SaveInProgress();
        }

        Close();
    }

    private void OnSettingsChanged(AppSettings s)
    {
        this.settings = s;

        SettingsStore.Save(this.settings);
    }

    private async void Options_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var win = new OptionsWindow(this.settings);

        win.SettingsChanged += OnSettingsChanged;

        await win.ShowDialog(this);

        if (win.Accepted)
        {
            this.settings = win.Settings;

            SettingsStore.Save(this.settings);
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

        UpdateGridBackground();

        DarkItem.Header = variant == Avalonia.Styling.ThemeVariant.Dark ? "✓ Dark" : "_Dark";
        LightItem.Header = variant == Avalonia.Styling.ThemeVariant.Light ? "✓ Light" : "_Light";
        SystemItem.Header = variant == Avalonia.Styling.ThemeVariant.Default ? "✓ System" : "_System";

        var name = variant == Avalonia.Styling.ThemeVariant.Dark ? "Dark" : variant == Avalonia.Styling.ThemeVariant.Light ? "Light" : "System";

        if (this.settings.Theme != name)
        {
            this.settings = this.settings with { Theme = name };

            SettingsStore.Save(this.settings);
        }
    }

    private void UpdateGridBackground()
    {
        GridBackground.Background = ActualThemeVariant == Avalonia.Styling.ThemeVariant.Dark ? Brushes.Gray : Brushes.White;
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
