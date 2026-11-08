namespace AICrosswordGen.Services;

// Filler words: Resources/google-10000-english-no-swears.txt based on
// https://github.com/first20hours/google-10000-english (MIT), see README Credits.
public sealed record AppSettings(int Iterations = 30, int Seed = 42, bool UseFiller = true, string Theme = "System");

public static class PuzzleService
{
    public static string? FindResource(params string[] names)
    {
        foreach (var n in names)
        {
            var candidates = new[]
            {
                Path.Combine(AppContext.BaseDirectory, "Resources", n),
                Path.Combine(AppContext.BaseDirectory, n),
                Path.Combine("Resources", n),
                n
            };

            foreach (var p in candidates)
            {
                if (File.Exists(p))
                {
                    return p;
                }
            }
        }

        return null;
    }

    static bool HasVowel(string w)
    {
        for (int i = 0; i < w.Length; i++)
        {
            if ("AEIOU".Contains(w[i]))
            {
                return true;
            }
        }

        return false;
    }

    static bool HasVariedLetters(string w)
    {
        var seen = new HashSet<char>();

        for (int i = 0; i < w.Length; i++)
        {
            seen.Add(w[i]);
        }

        return seen.Count > 1;
    }

    internal static int CompareByLength(string a, string b)
    {
        return b.Length.CompareTo(a.Length);
    }

    static int CompareByScoreDesc(Models.Placement a, Models.Placement b)
    {
        return b.Score.CompareTo(a.Score);
    }

    public static SolveResult Generate(IEnumerable<string> words, AppSettings settings, Action<int, int>? progress = null)
    {
        var solver = new Solver(words, settings.Seed);

        var best = solver.Solve(settings.Iterations, progress);

        var primary = FindResource("google-10000-english-no-swears.txt");
        var fallback = FindResource("words_alpha.txt");

        if (settings.UseFiller && (primary != null || fallback != null))
        {
            var fillers = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var path in new[] { primary, fallback })
            {
                if (path == null)
                {
                    continue;
                }

                foreach (var line in File.ReadLines(path))
                {
                    var w = line.Trim().ToUpperInvariant();

                    if (w.Length < 3 || w.Length > 7)
                    {
                        continue;
                    }

                    if (!Solver.IsAllLetters(w))
                    {
                        continue;
                    }

                    if (!HasVariedLetters(w))
                    {
                        continue;
                    }

                    if (!HasVowel(w))
                    {
                        continue;
                    }

                    if (seen.Add(w))
                    {
                        fillers.Add(w);
                    }
                }
            }

            fillers.Sort(CompareByLength);

            foreach (var w in fillers)
            {
                best.Grid.Dictionary.Add(w);
            }

            var usedSet = new HashSet<string>(best.Used, StringComparer.OrdinalIgnoreCase);
            var fi = 0;

            foreach (var w in fillers)
            {
                fi++;

                if (fi % 200 == 0)
                {
                    progress?.Invoke(fi, fillers.Count);
                }

                if (usedSet.Contains(w))
                {
                    continue;
                }

                var cands = new List<Models.Placement>();

                foreach (var c in best.Grid.Candidates(w))
                {
                    if (c.BboxGrowth == 0)
                    {
                        cands.Add(c);
                    }
                }

                if (cands.Count == 0)
                {
                    continue;
                }

                cands.Sort(CompareByScoreDesc);

                best.Grid.Place(cands[0]);

                usedSet.Add(w);

                best = best with
                {
                    Density = (double)best.Grid.Cells.Count / Math.Max(1, best.Grid.Area()),
                    Area = best.Grid.Area()
                };
            }
        }

        return best;
    }
}
