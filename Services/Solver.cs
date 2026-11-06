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
        _words = input
            .Select(w => w.Trim().ToUpperInvariant())
            .Where(w => w.Length >= 3 && w.All(char.IsLetter))
            .Distinct()
            .ToList();
        _seed = seed;
    }

    public SolveResult Solve(int iterations = 50, Action<int, int>? progress = null)
    {
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
        bool aSmall = a.Used.Count < minUsed, bSmall = b.Used.Count < minUsed;
        if (aSmall != bSmall) return bSmall;
        int sa = SquareDiff(a), sb = SquareDiff(b);
        bool aSq = sa <= 2, bSq = sb <= 2;
        if (aSq != bSq) return aSq;
        if (sa != sb) return sa < sb;
        if (Math.Abs(a.Density - b.Density) > 1e-9) return a.Density > b.Density;
        if (a.Used.Count != b.Used.Count) return a.Used.Count > b.Used.Count;
        return a.Area < b.Area;
    }

    SolveResult RunOnce(int iteration)
    {
        var _rng = new Random(_seed + iteration * 7919);
        var grid = new CrosswordGrid(_words);
        var remaining = _words.OrderByDescending(w => w.Length).ThenBy(_ => _rng.NextDouble()).ToList();
        var used = new List<string>();
        var placed = new List<Placement>();

        var first = remaining[0];
        remaining.RemoveAt(0);
        var seed = new Placement(first, 0, 0, true, 0, first.Length, 0, 0);
        grid.Place(seed);
        placed.Add(seed);
        used.Add(first);

        // size cap: explore small squares to force dense core, leave many words unused
        int target = _rng.Next(16, 25);
        (int W, int H) Dims(Placement p)
        {
            var (minR, maxR, minC, maxC) = grid.Bounds();
            int nMinR = Math.Min(minR, p.Row), nMaxR = Math.Max(maxR, p.Row + (p.Horizontal ? 0 : p.Word.Length - 1));
            int nMinC = Math.Min(minC, p.Col), nMaxC = Math.Max(maxC, p.Col + (p.Horizontal ? p.Word.Length - 1 : 0));
            return (nMaxC - nMinC + 1, nMaxR - nMinR + 1);
        }

        // dynamic best-fit: each step place remaining word with lowest aspect, then highest score
        while (remaining.Count > 0)
        {
            string? bestWord = null;
            Placement? bestCand = null;
            var letters = new HashSet<char>(grid.Cells.Values);
            foreach (var w in remaining)
            {
                if (!w.Any(c => letters.Contains(c))) continue;
                var cands = grid.Candidates(w);
                if (cands.Count == 0) continue;
                cands = cands.Where(c => { var d = Dims(c); return d.W <= target && d.H <= target; }).ToList();
                if (cands.Count == 0) continue;
                cands.Sort((a, b) =>
                {
                    int d = a.Aspect.CompareTo(b.Aspect);
                    return d != 0 ? d : b.Score.CompareTo(a.Score);
                });
                if (bestCand == null
                    || cands[0].Aspect < bestCand.Value.Aspect
                    || (cands[0].Aspect == bestCand.Value.Aspect && cands[0].Score > bestCand.Value.Score))
                {
                    bestCand = cands[0];
                    bestWord = w;
                }
            }
            if (bestWord == null || bestCand == null) break;
            // tiny random jitter for diversity (within same aspect bucket)
            if (_rng.Next(5) == 0)
            {
                var cands = grid.Candidates(bestWord)
                    .Where(c => { var d = Dims(c); return d.W <= target && d.H <= target; })
                    .ToList();
                cands.Sort((a, b) => b.Score.CompareTo(a.Score));
                var sq = cands.Where(c => c.Aspect <= bestCand.Value.Aspect + 1).ToList();
                if (sq.Count > 0) bestCand = sq[_rng.Next(Math.Min(3, sq.Count))];
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
                if (p.Word == placed[0].Word && p.Row == 0 && p.Col == 0) continue;
                var test = Rebuild(placed.Where(q => q.Word != p.Word || q != p));
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
            if (!changed) break;
        }

        int area = grid.Area();
        double density = area > 0 ? (double)grid.Cells.Count / area : 0;
        var unused = _words.Except(used, StringComparer.OrdinalIgnoreCase).ToList();
        return new SolveResult(grid, used, unused, density, area, _words.Count);
    }

    CrosswordGrid Rebuild(IEnumerable<Placement> ps)
    {
        var g = new CrosswordGrid(_words);
        foreach (var p in ps) g.Place(p);
        return g;
    }
}
