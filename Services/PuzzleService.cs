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

    static bool CoversAny(Models.Placement p, HashSet<(int Row, int Col)> dots)
    {
        for (int i = 0; i < p.Word.Length; i++)
        {
            int r = p.Row + (p.Horizontal ? 0 : i);
            int c = p.Col + (p.Horizontal ? i : 0);

            if (dots.Contains((r, c)))
            {
                return true;
            }
        }

        return false;
    }

    // shortcut: pre-filter checks any neighbour letter; misses rare cross-only fills, drop filter when scans feel slow
    static bool CanBridge(string w, HashSet<char> neighbours)
    {
        for (int i = 0; i < w.Length; i++)
        {
            if (neighbours.Contains(w[i]))
            {
                return true;
            }
        }

        return false;
    }

    // empty cells inside bounds with filled neighbours on both sides (single dot)
    internal static List<(int Row, int Col)> FindIsolatedDots(Models.CrosswordGrid grid)
    {
        var dots = new List<(int Row, int Col)>();

        if (grid.IsEmpty)
        {
            return dots;
        }

        var (minR, maxR, minC, maxC) = grid.Bounds();

        for (int r = minR; r <= maxR; r++)
        {
            for (int c = minC; c <= maxC; c++)
            {
                if (grid.Cells.ContainsKey((r, c)))
                {
                    continue;
                }

                bool h = grid.Cells.ContainsKey((r, c - 1)) && grid.Cells.ContainsKey((r, c + 1));
                bool v = grid.Cells.ContainsKey((r - 1, c)) && grid.Cells.ContainsKey((r + 1, c));

                if (h || v)
                {
                    dots.Add((r, c));
                }
            }
        }

        return dots;
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
            var usedAll = new List<string>(best.Used);
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

                if (!usedAll.Contains(w))
                {
                    usedAll.Add(w);
                }

                best = best with
                {
                    Density = (double)best.Grid.Cells.Count / Math.Max(1, best.Grid.Area()),
                    Area = best.Grid.Area()
                };
            }

            // gap scan: isolated single dots flanked on both sides
            bool gapChanged = true;
            int gapPass = 0;

            while (gapChanged)
            {
                gapChanged = false;

                var dots = FindIsolatedDots(best.Grid);

                if (dots.Count == 0)
                {
                    break;
                }

                progress?.Invoke(fillers.Count + gapPass, fillers.Count + dots.Count);
                gapPass++;

                var dotSet = new HashSet<(int Row, int Col)>(dots);
                var neighbours = new HashSet<char>();

                foreach (var (r, c) in dots)
                {
                    foreach (var (nr, nc) in new[] { (r, c - 1), (r, c + 1), (r - 1, c), (r + 1, c) })
                    {
                        if (best.Grid.Cells.TryGetValue((nr, nc), out char ch))
                        {
                            neighbours.Add(ch);
                        }
                    }
                }

                Models.Placement? gapBest = null;
                string? gapWord = null;

                foreach (var w in fillers)
                {
                    if (usedSet.Contains(w) || !CanBridge(w, neighbours))
                    {
                        continue;
                    }

                    foreach (var c in best.Grid.Candidates(w))
                    {
                        if (c.BboxGrowth != 0 || !CoversAny(c, dotSet))
                        {
                            continue;
                        }

                        if (gapBest == null || c.Score > gapBest.Value.Score)
                        {
                            gapBest = c;
                            gapWord = w;
                        }
                    }
                }

                if (gapBest != null && gapWord != null)
                {
                    best.Grid.Place(gapBest.Value);
                    usedSet.Add(gapWord);

                    if (!usedAll.Contains(gapWord))
                    {
                        usedAll.Add(gapWord);
                    }

                    best = best with
                    {
                        Density = (double)best.Grid.Cells.Count / Math.Max(1, best.Grid.Area()),
                        Area = best.Grid.Area()
                    };
                    gapChanged = true;
                }
            }

            best = best with
            {
                Used = usedAll,
                Unused = best.Unused.Where(u => !usedSet.Contains(u)).ToList()
            };
        }

        return best;
    }
}
