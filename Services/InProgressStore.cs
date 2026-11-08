using AICrosswordGen.Models;

namespace AICrosswordGen.Services;

public static class InProgressStore
{
    public static string Folder => Path.Combine(SettingsStore.Folder, "inprogress");

    public static string MruFile => Path.Combine(SettingsStore.Folder, "inprogress-mru.txt");

    public static string CreatePath()
    {
        Directory.CreateDirectory(Folder);

        return Path.Combine(Folder, $"puzzle-in-progress-{DateTime.Now:yyyyMMdd-HHmmss}.txt");
    }

    public static List<string> LoadMru()
    {
        var list = new List<string>();

        if (!File.Exists(MruFile))
        {
            return list;
        }

        foreach (var line in File.ReadAllLines(MruFile))
        {
            var p = line.Trim();

            if (p.Length > 0 && File.Exists(p) && !list.Contains(p))
            {
                list.Add(p);
            }
        }

        return list.Take(5).ToList();
    }

    public static void TouchMru(string path)
    {
        var list = LoadMru();

        list.RemoveAll(p => string.Equals(p, path, StringComparison.OrdinalIgnoreCase));
        list.Insert(0, path);

        Directory.CreateDirectory(SettingsStore.Folder);
        File.WriteAllText(MruFile, string.Join("\n", list.Take(5)) + "\n");
    }

    public static void RemoveFromMru(string path)
    {
        var list = LoadMru();

        list.RemoveAll(p => string.Equals(p, path, StringComparison.OrdinalIgnoreCase));

        Directory.CreateDirectory(SettingsStore.Folder);
        File.WriteAllText(MruFile, list.Count == 0 ? "" : string.Join("\n", list) + "\n");
    }

    public static void Delete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
        }

        RemoveFromMru(path);
    }

    public static (CrosswordGrid Grid, List<string?> AcrossClues, List<string?> DownClues, string Title) Read(string path)
    {
        var lines = File.ReadAllLines(path);

        string title = "";
        var gridLines = new List<string>();
        var acrossLines = new List<string>();
        var downLines = new List<string>();

        string section = "";

        foreach (var raw in lines)
        {
            var line = raw.TrimEnd('\r');

            if (line.StartsWith("<"))
            {
                section = line.Trim().ToUpperInvariant();
                continue;
            }

            if (section == "<TITLE>" && title.Length == 0)
            {
                title = line.Trim();
            }
            else if (section is "<GRID>" or "<CROSSWORD>")
            {
                if (line.Trim().Length > 0)
                {
                    gridLines.Add(line.Trim());
                }
            }
            else if (section == "<ACROSS>")
            {
                acrossLines.Add(line);
            }
            else if (section == "<DOWN>")
            {
                downLines.Add(line);
            }
        }

        var grid = new CrosswordGrid(Array.Empty<string>());
        grid.LoadGrid(gridLines.ToArray());

        var (_, across, down) = PuzzleClues.Get(grid);

        var acrossClues = new List<string?>();
        var downClues = new List<string?>();

        for (int i = 0; i < across.Count; i++)
        {
            string clue = i < acrossLines.Count ? acrossLines[i].Trim() : "";

            acrossClues.Add(clue.Length == 0 ? null : clue);
        }

        for (int i = 0; i < down.Count; i++)
        {
            string clue = i < downLines.Count ? downLines[i].Trim() : "";

            downClues.Add(clue.Length == 0 ? null : clue);
        }

        return (grid, acrossClues, downClues, title);
    }
}
