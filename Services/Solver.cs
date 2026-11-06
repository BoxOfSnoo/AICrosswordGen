using AICrosswordGen.Models;

namespace AICrosswordGen.Services;

public sealed record SolveResult(
    CrosswordGrid Grid,
    List<string> Used,
    List<string> Unused,
    double Density,
    int Area,
    int Iterations);

public sealed class Solver
{
    private readonly List<string> _words;

    private readonly int _seed;

    public Solver(IEnumerable<string> input, int seed = 1)
    {
        _words = new List<string>();

        var seen = new HashSet<string>();

        foreach (var raw in input)
        {
            var w = raw.Trim().ToUpperInvariant();

            if (w.Length < 3)
            {
                continue;
            }

            if (!IsAllLetters(w))
            {
                continue;
            }

            if (seen.Add(w))
            {
                _words.Add(w);
            }
        }

        _seed = seed;
    }

    internal static bool IsAllLetters(string w)
    {
        for (int i = 0; i < w.Length; i++)
        {
            if (!char.IsLetter(w[i]))
            {
                return false;
            }
        }

        return true;
    }

    public SolveResult Solve(int iterations = 50, Action<int, int>? progress = null)
    {
        iterations = Math.Max(1, iterations);

        if (_words.Count == 0)
        {
            progress?.Invoke(1, 1);

            return new SolveResult(new CrosswordGrid(_words), new(), new(), 0, 0, 0);
        }

        SolveResult? best = null;
        var stagnant = 0;

        for (int it = 0; it < iterations; it++)
        {
            var cur = RunOnce(it);

            if (best == null || Better(cur, best))
            {
                best = cur;
                stagnant = 0;
            }
            else
            {
                stagnant++;
            }

            progress?.Invoke(it + 1, iterations);

            if (stagnant >= 4)
            {
                break;
            }
        }

        return best!;
    }

    static int SquareDiff(SolveResult r)
    {
        var (minR, maxR, minC, maxC) = r.Grid.Bounds();

        return Math.Abs((maxC - minC) - (maxR - minR));
    }

    static bool Better(SolveResult a, SolveResult b)
    {
        int minUsed = 15;

        bool aSmall = a.Used.Count < minUsed;
        bool bSmall = b.Used.Count < minUsed;

        if (aSmall != bSmall)
        {
            return bSmall;
        }

        int sa = SquareDiff(a);
        int sb = SquareDiff(b);

        bool aSq = sa <= 2;
        bool bSq = sb <= 2;

        if (aSq != bSq)
        {
            return aSq;
        }

        if (sa != sb)
        {
            return sa < sb;
        }

        if (Math.Abs(a.Density - b.Density) > 1e-9)
        {
            return a.Density > b.Density;
        }

        if (a.Used.Count != b.Used.Count)
        {
            return a.Used.Count > b.Used.Count;
        }

        return a.Area < b.Area;
    }

    SolveResult RunOnce(int iteration)
    {
        var rng = new Random(_seed + iteration * 7919);
        var grid = new CrosswordGrid(_words);

        var keys = new Dictionary<string, double>();

        foreach (var w in _words)
        {
            keys[w] = rng.NextDouble();
        }

        int CompareRemaining(string a, string b)
        {
            int byLength = b.Length.CompareTo(a.Length);

            if (byLength != 0)
            {
                return byLength;
            }

            return keys[a].CompareTo(keys[b]);
        }

        var remaining = new List<string>(_words);

        remaining.Sort(CompareRemaining);

        var used = new List<string>();
        var placed = new List<Placement>();

        var first = remaining[0];

        remaining.RemoveAt(0);

        // center longest word on origin, build outward around it
        var seed = new Placement(first, 0, -first.Length / 2, true, 0, first.Length, 0, 0);

        grid.Place(seed);

        placed.Add(seed);

        used.Add(first);

        // size cap: explore small squares to force dense core, leave many words unused
        int target = rng.Next(16, 25);

        (int W, int H) Dims(Placement p)
        {
            var (minR, maxR, minC, maxC) = grid.Bounds();

            int nMinR = Math.Min(minR, p.Row);
            int nMaxR = Math.Max(maxR, p.Row + (p.Horizontal ? 0 : p.Word.Length - 1));

            int nMinC = Math.Min(minC, p.Col);
            int nMaxC = Math.Max(maxC, p.Col + (p.Horizontal ? p.Word.Length - 1 : 0));

            return (nMaxC - nMinC + 1, nMaxR - nMinR + 1);
        }

        bool FitsTarget(Placement p)
        {
            var d = Dims(p);

            return d.W <= target && d.H <= target;
        }

        int CompareByAspectThenScore(Placement a, Placement b)
        {
            int d = a.Aspect.CompareTo(b.Aspect);

            if (d != 0)
            {
                return d;
            }

            return b.Score.CompareTo(a.Score);
        }

        int CompareByScoreDesc(Placement a, Placement b)
        {
            return b.Score.CompareTo(a.Score);
        }

        bool HasSharedLetter(string w, HashSet<char> letters)
        {
            for (int i = 0; i < w.Length; i++)
            {
                if (letters.Contains(w[i]))
                {
                    return true;
                }
            }

            return false;
        }

        List<Placement> FilterFits(List<Placement> cands)
        {
            var kept = new List<Placement>();

            foreach (var c in cands)
            {
                if (FitsTarget(c))
                {
                    kept.Add(c);
                }
            }

            return kept;
        }

        // dynamic best-fit: each step place remaining word with lowest aspect, then highest score
        while (remaining.Count > 0)
        {
            string? bestWord = null;
            Placement? bestCand = null;

            var letters = new HashSet<char>(grid.Cells.Values);

            foreach (var w in remaining)
            {
                if (!HasSharedLetter(w, letters))
                {
                    continue;
                }

                var cands = grid.Candidates(w);

                if (cands.Count == 0)
                {
                    continue;
                }

                cands = FilterFits(cands);

                if (cands.Count == 0)
                {
                    continue;
                }

                cands.Sort(CompareByAspectThenScore);

                if (bestCand == null
                    || cands[0].Aspect < bestCand.Value.Aspect
                    || (cands[0].Aspect == bestCand.Value.Aspect && cands[0].Score > bestCand.Value.Score))
                {
                    bestCand = cands[0];
                    bestWord = w;
                }
            }

            if (bestWord == null || bestCand == null)
            {
                break;
            }

            // tiny random jitter for diversity (within same aspect bucket)
            if (rng.Next(5) == 0)
            {
                var cands = FilterFits(grid.Candidates(bestWord));

                cands.Sort(CompareByScoreDesc);

                var sq = new List<Placement>();

                foreach (var c in cands)
                {
                    if (c.Aspect <= bestCand.Value.Aspect + 1)
                    {
                        sq.Add(c);
                    }
                }

                if (sq.Count > 0)
                {
                    bestCand = sq[rng.Next(Math.Min(3, sq.Count))];
                }
            }

            grid.Place(bestCand.Value);

            placed.Add(bestCand.Value);

            used.Add(bestWord);

            remaining.Remove(bestWord);
        }

        // density trim: drop words whose removal improves density (keep seed)
        for (int pass = 0; pass < 3; pass++)
        {
            bool changed = false;

            double curDensity = (double)grid.Cells.Count / Math.Max(1, grid.Area());

            foreach (var p in placed.ToList())
            {
                if (p.Word == placed[0].Word && p.Row == placed[0].Row && p.Col == placed[0].Col)
                {
                    continue;
                }

                var without = new List<Placement>();

                foreach (var q in placed)
                {
                    if (q.Word != p.Word || q != p)
                    {
                        without.Add(q);
                    }
                }

                var test = Rebuild(without);

                double d = test.Area() > 0 ? (double)test.Cells.Count / test.Area() : 0;

                if (d > curDensity + 0.001 && test.IsValid())
                {
                    placed.Remove(p);
                    grid = test;
                    used.Remove(p.Word);
                    curDensity = d;
                    changed = true;
                }
            }

            if (!changed)
            {
                break;
            }
        }

        int area = grid.Area();

        double density = area > 0 ? (double)grid.Cells.Count / area : 0;

        var unused = new List<string>(_words.Except(used, StringComparer.OrdinalIgnoreCase));

        return new SolveResult(grid, used, unused, density, area, _words.Count);
    }

    CrosswordGrid Rebuild(IEnumerable<Placement> ps)
    {
        var g = new CrosswordGrid(_words);

        foreach (var p in ps)
        {
            g.Place(p);
        }

        return g;
    }
}
