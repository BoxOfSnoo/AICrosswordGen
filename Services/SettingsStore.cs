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
    public static string OptionsFile => Path.Combine(Folder, "options.json");
    public static string BlocklistFile => Path.Combine(Folder, "blocklist.txt");

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
        if (!File.Exists(BlocklistFile))
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        return new HashSet<string>(File.ReadAllLines(BlocklistFile).Select(x => x.Trim().ToUpperInvariant()).Where(x => x.Length > 0), StringComparer.OrdinalIgnoreCase);
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
