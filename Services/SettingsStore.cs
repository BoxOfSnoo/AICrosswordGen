namespace AICrosswordGen.Services;

public static class SettingsStore
{
    public static string Folder
    {
        get
        {
            bool unix = OperatingSystem.IsLinux() || OperatingSystem.IsMacOS();

            if (unix)
            {
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config", "aicrosswordgen");
            }

            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AICrosswordGen");
        }
    }

    public static string OptionsFile
    {
        get
        {
            return Path.Combine(Folder, "options.json");
        }
    }

    public static string BlocklistFile
    {
        get
        {
            return Path.Combine(Folder, "blocklist.txt");
        }
    }

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(OptionsFile))
            {
                var json = File.ReadAllText(OptionsFile);

                var s = System.Text.Json.JsonSerializer.Deserialize<AppSettings>(json);

                if (s != null)
                {
                    return s;
                }
            }
        }
        catch
        {
        }

        return new AppSettings();
    }

    public static void Save(AppSettings s)
    {
        Directory.CreateDirectory(Folder);

        File.WriteAllText(OptionsFile, System.Text.Json.JsonSerializer.Serialize(s));
    }

    public static HashSet<string> LoadBlocklist()
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (!File.Exists(BlocklistFile))
        {
            return set;
        }

        foreach (var line in File.ReadAllLines(BlocklistFile))
        {
            var w = line.Trim().ToUpperInvariant();

            if (w.Length > 0)
            {
                set.Add(w);
            }
        }

        return set;
    }

    public static void AddBlocklist(string word)
    {
        Directory.CreateDirectory(Folder);

        var set = LoadBlocklist();

        if (set.Add(word.Trim().ToUpperInvariant()))
        {
            File.AppendAllText(BlocklistFile, word.Trim().ToUpperInvariant() + "\n");
        }
    }
}
