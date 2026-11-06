namespace AICrosswordGen.Services;

// Filler words: Resources/google-10000-english-no-swears.txt based on
// https://github.com/first20hours/google-10000-english (MIT), see README Credits.
public sealed record AppSettings(int Iterations = 30, int Seed = 42, bool UseFiller = true);

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

                foreach (var w in File.ReadLines(path).Select(x => x.Trim().ToUpperInvariant()))
                {
                    if (w.Length < 3 || w.Length > 7)
                    {
                        continue;
                    }

                    if (!w.All(char.IsLetter))
                    {
                        continue;
                    }

                    if (w.Distinct().Count() <= 1)
                    {
                        continue;
                    }

                    if (!w.Any(c => "AEIOU".Contains(c)))
                    {
                        continue;
                    }

                    if (seen.Add(w))
                    {
                        fillers.Add(w);
                    }
                }
            }

            fillers = fillers.OrderBy(w => w.Length).ToList();

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

                var cands = best.Grid.Candidates(w).Where(c => c.BboxGrowth == 0).ToList();

                if (cands.Count == 0)
                {
                    continue;
                }

                cands.Sort((a, b) => b.Score.CompareTo(a.Score));
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
