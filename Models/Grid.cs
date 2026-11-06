namespace AICrosswordGen.Models;

public readonly record struct Placement(
    string Word,
    int Row,
    int Col,
    bool Horizontal,
    int Crossings,
    int NewCells,
    int BboxGrowth,
    int UnlistedPenalty,
    int Aspect = 0,
    int Adj = 0,
    int CenterDist = 0)
{
    public int Score
    {
        get
        {
            return Crossings * 15 + Adj * 10 - NewCells * 3 - BboxGrowth * 8 - UnlistedPenalty * 6 - Aspect * 30 - CenterDist * 2 + (BboxGrowth == 0 ? 15 : 0);
        }
    }
}

public sealed class CrosswordGrid
{
    public Dictionary<(int Row, int Col), char> Cells { get; } = new();

    public HashSet<string> Dictionary { get; }

    private int _minR = int.MaxValue;

    private int _maxR = int.MinValue;

    private int _minC = int.MaxValue;

    private int _maxC = int.MinValue;

    public CrosswordGrid(IEnumerable<string> words)
    {
        Dictionary = new HashSet<string>(words, StringComparer.OrdinalIgnoreCase);
    }

    public bool IsEmpty
    {
        get
        {
            return Cells.Count == 0;
        }
    }

    public (int MinRow, int MaxRow, int MinCol, int MaxCol) Bounds()
    {
        if (Cells.Count == 0)
        {
            return (0, -1, 0, -1);
        }

        return (_minR, _maxR, _minC, _maxC);
    }

    public bool IsValid()
    {
        if (Cells.Count == 0)
        {
            return true;
        }

        var (minR, maxR, minC, maxC) = Bounds();

        bool White(int r, int c)
        {
            return Cells.ContainsKey((r, c));
        }

        bool Check(string s)
        {
            if (s.Length == 2)
            {
                return false;
            }

            if (s.Length >= 3 && !Dictionary.Contains(s))
            {
                return false;
            }

            return true;
        }

        for (int r = minR; r <= maxR; r++)
        {
            var sb = new System.Text.StringBuilder();

            for (int c = minC; c <= maxC + 1; c++)
            {
                if (c <= maxC && White(r, c))
                {
                    sb.Append(Cells[(r, c)]);
                }
                else
                {
                    if (sb.Length > 1 && !Check(sb.ToString()))
                    {
                        return false;
                    }

                    sb.Clear();
                }
            }
        }

        for (int c = minC; c <= maxC; c++)
        {
            var sb = new System.Text.StringBuilder();

            for (int r = minR; r <= maxR + 1; r++)
            {
                if (r <= maxR && White(r, c))
                {
                    sb.Append(Cells[(r, c)]);
                }
                else
                {
                    if (sb.Length > 1 && !Check(sb.ToString()))
                    {
                        return false;
                    }

                    sb.Clear();
                }
            }
        }

        return true;
    }

    public int Area()
    {
        if (Cells.Count == 0)
        {
            return 0;
        }

        var (minR, maxR, minC, maxC) = Bounds();

        return (maxR - minR + 1) * (maxC - minC + 1);
    }

    public void Place(Placement p)
    {
        for (int i = 0; i < p.Word.Length; i++)
        {
            int r = p.Row + (p.Horizontal ? 0 : i);
            int c = p.Col + (p.Horizontal ? i : 0);

            Cells[(r, c)] = p.Word[i];

            _minR = Math.Min(_minR, r);
            _maxR = Math.Max(_maxR, r);
            _minC = Math.Min(_minC, c);
            _maxC = Math.Max(_maxC, c);
        }
    }

    public List<Placement> Candidates(string word)
    {
        var list = new List<Placement>();

        if (Cells.Count == 0)
        {
            return list;
        }

        var seen = new HashSet<(int, int, bool)>();

        foreach (var kv in Cells.ToList())
        {
            int r = kv.Key.Row;
            int c = kv.Key.Col;
            char ch = kv.Value;

            for (int i = 0; i < word.Length; i++)
            {
                if (word[i] != ch)
                {
                    continue;
                }

                // horizontal through this cell
                var p1 = TryMake(word, r, c - i, horizontal: true);

                if (p1 != null && seen.Add((p1.Value.Row, p1.Value.Col, true)))
                {
                    list.Add(p1.Value);
                }

                // vertical
                var p2 = TryMake(word, r - i, c, horizontal: false);

                if (p2 != null && seen.Add((p2.Value.Row, p2.Value.Col, false)))
                {
                    list.Add(p2.Value);
                }
            }
        }

        return list;
    }

    Placement? TryMake(string word, int row, int col, bool horizontal)
    {
        int crossings = 0;

        for (int i = 0; i < word.Length; i++)
        {
            int r = row + (horizontal ? 0 : i);
            int c = col + (horizontal ? i : 0);

            if (Cells.TryGetValue((r, c), out char ch))
            {
                if (ch != word[i])
                {
                    return null;
                }

                crossings++;
            }
        }

        if (crossings == 0)
        {
            return null;
        }

        int newCells = word.Length - crossings;

        // run checks: no 2-letter runs; every >=3 run must be a listed word
        int unlisted = 0;
        int invalid = 0;

        // helper reads hypothetical cell
        char? Hyp(int r, int c)
        {
            // inside new word?
            int dr = r - row;
            int dc = c - col;

            if (horizontal && dr == 0 && dc >= 0 && dc < word.Length)
            {
                return word[dc];
            }

            if (!horizontal && dc == 0 && dr >= 0 && dr < word.Length)
            {
                return word[dr];
            }

            if (Cells.TryGetValue((r, c), out char ch2))
            {
                return ch2;
            }

            return null;
        }

        var affectedRows = new HashSet<int>();
        var affectedCols = new HashSet<int>();

        for (int i = 0; i < word.Length; i++)
        {
            int r = row + (horizontal ? 0 : i);
            int c = col + (horizontal ? i : 0);

            affectedRows.Add(r);
            affectedCols.Add(c);
        }

        void CheckRun(int s, int e, string run)
        {
            if (run.Length == 2)
            {
                invalid++;
            }
            else
            {
                if (run.Length >= 3 && !Dictionary.Contains(run))
                {
                    unlisted++;
                }
            }
        }

        string BuildRowRun(int r, int s, int e)
        {
            var chars = new char[e - s + 1];

            for (int i = 0; i < chars.Length; i++)
            {
                chars[i] = Hyp(r, s + i)!.Value;
            }

            return new string(chars);
        }

        string BuildColRun(int c, int s, int e)
        {
            var chars = new char[e - s + 1];

            for (int i = 0; i < chars.Length; i++)
            {
                chars[i] = Hyp(s + i, c)!.Value;
            }

            return new string(chars);
        }

        foreach (int r in affectedRows)
        {
            int s = col;
            int e = col;

            while (Hyp(r, s - 1) != null)
            {
                s--;
            }

            while (Hyp(r, e + 1) != null)
            {
                e++;
            }

            bool overlaps = false;

            for (int i = 0; i < word.Length; i++)
            {
                int wr = row + (horizontal ? 0 : i);
                int wc = col + (horizontal ? i : 0);

                if (wr == r && wc >= s && wc <= e)
                {
                    overlaps = true;

                    break;
                }
            }

            if (overlaps)
            {
                CheckRun(s, e, BuildRowRun(r, s, e));
            }
        }

        foreach (int c in affectedCols)
        {
            int s = row;
            int e = row;

            while (Hyp(s - 1, c) != null)
            {
                s--;
            }

            while (Hyp(e + 1, c) != null)
            {
                e++;
            }

            bool overlaps = false;

            for (int i = 0; i < word.Length; i++)
            {
                int wr = row + (horizontal ? 0 : i);
                int wc = col + (horizontal ? i : 0);

                if (wc == c && wr >= s && wr <= e)
                {
                    overlaps = true;

                    break;
                }
            }

            if (overlaps)
            {
                CheckRun(s, e, BuildColRun(c, s, e));
            }
        }

        if (invalid > 0 || unlisted > 0)
        {
            return null;
        }

        // adjacent touches: existing letters side-by-side with this word (dense fill signal)
        int adj = 0;

        for (int i = 0; i < word.Length; i++)
        {
            int r = row + (horizontal ? 0 : i);
            int c = col + (horizontal ? i : 0);

            if (horizontal)
            {
                if (Cells.ContainsKey((r - 1, c)))
                {
                    adj++;
                }

                if (Cells.ContainsKey((r + 1, c)))
                {
                    adj++;
                }
            }
            else
            {
                if (Cells.ContainsKey((r, c - 1)))
                {
                    adj++;
                }

                if (Cells.ContainsKey((r, c + 1)))
                {
                    adj++;
                }
            }
        }

        int growth = NewArea(row, col, horizontal, word.Length, out int aspect);

        // distance from origin (seed word center) so builds stay clustered around it
        int midR = row + (horizontal ? 0 : word.Length / 2);
        int midC = col + (horizontal ? word.Length / 2 : 0);
        int centerDist = Math.Abs(midR) + Math.Abs(midC);

        return new Placement(word, row, col, horizontal, crossings, newCells, growth, unlisted, aspect, adj, centerDist);
    }

    int NewArea(int row, int col, bool horizontal, int len, out int aspect)
    {
        if (Cells.Count == 0)
        {
            aspect = Math.Abs(1 - len);

            return len;
        }

        var (minR, maxR, minC, maxC) = Bounds();

        int oldArea = (maxR - minR + 1) * (maxC - minC + 1);

        int nMinR = Math.Min(minR, row);
        int nMaxR = Math.Max(maxR, row + (horizontal ? 0 : len - 1));

        int nMinC = Math.Min(minC, col);
        int nMaxC = Math.Max(maxC, col + (horizontal ? len - 1 : 0));

        int w = nMaxC - nMinC + 1;
        int h = nMaxR - nMinR + 1;

        aspect = Math.Abs(w - h);

        return w * h - oldArea;
    }
}
