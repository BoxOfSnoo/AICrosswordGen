using AICrosswordGen.Models;

namespace AICrosswordGen.Services;

/// <summary>Writes Across Lite .puz (spec: https://code.google.com/archive/p/puz/wikis/FileFormat.wiki).</summary>
public static class PuzWriter
{
    public static void Write(CrosswordGrid grid, string path,
        string title = "", string author = "", string copyright = "", string notes = "")
    {
        if (grid.Cells.Count == 0)
        {
            throw new InvalidOperationException("Empty grid");
        }

        var (minR, maxR, minC, maxC) = grid.Bounds();

        int w = maxC - minC + 1;
        int h = maxR - minR + 1;

        if (w > 255 || h > 255)
        {
            throw new InvalidOperationException($"Grid {w}x{h} too big for .puz (max 255)");
        }

        bool White(int r, int c)
        {
            return grid.Cells.ContainsKey((minR + r, minC + c));
        }

        var solution = new byte[w * h];
        var player = new byte[w * h];

        for (int r = 0; r < h; r++)
        {
            for (int c = 0; c < w; c++)
            {
                if (White(r, c))
                {
                    solution[r * w + c] = (byte)grid.Cells[(minR + r, minC + c)];
                    player[r * w + c] = (byte)'-';
                }
                else
                {
                    solution[r * w + c] = (byte)'.';
                    player[r * w + c] = (byte)'.';
                }
            }
        }

        // Number cells, extract clue words in number order (Across before Down)
        var (_, acrossFormatted, downFormatted) = PuzzleClues.Get(grid);

        // interleaved by number: rebuild ordered list from formatted entries
        var clues = new List<(int num, int order, string word)>();

        foreach (var s in acrossFormatted)
        {
            int dot = s.IndexOf(". ");

            string word = s[(dot + 2)..];

            int num = int.Parse(s[..dot]);

            clues.Add((num, 0, word));
        }

        foreach (var s in downFormatted)
        {
            int dot = s.IndexOf(". ");

            string word = s[(dot + 2)..];

            int num = int.Parse(s[..dot]);

            clues.Add((num, 1, word));
        }

        int CompareClues((int num, int order, string word) a, (int num, int order, string word) b)
        {
            int byNum = a.num.CompareTo(b.num);

            if (byNum != 0)
            {
                return byNum;
            }

            return a.order.CompareTo(b.order);
        }

        clues.Sort(CompareClues);

        var orderedClues = new List<string>();

        foreach (var clue in clues)
        {
            orderedClues.Add(clue.word);
        }

        byte[] Ascii(string s)
        {
            return System.Text.Encoding.ASCII.GetBytes(s);
        }

        ushort CksumBytes(byte[] data, int offset, int len, ushort init)
        {
            ushort c = init;

            for (int i = 0; i < len; i++)
            {
                if ((c & 0x0001) != 0)
                {
                    c = (ushort)((c >> 1) + 0x8000);
                }
                else
                {
                    c = (ushort)(c >> 1);
                }

                c = unchecked((ushort)(c + data[offset + i]));
            }

            return c;
        }

        ushort Cksum(string s, ushort init)
        {
            var b = System.Text.Encoding.ASCII.GetBytes(s);

            return CksumBytes(b, 0, b.Length, init);
        }

        ushort CksumZ(string s, ushort init)
        {
            return Cksum(s + "\0", init);
        }

        var header = new byte[0x34];

        ushort nClues = (ushort)orderedClues.Count;

        header[0x2C] = (byte)w;
        header[0x2D] = (byte)h;
        header[0x2E] = (byte)(nClues & 0xFF);
        header[0x2F] = (byte)((nClues >> 8) & 0xFF);

        // rest zero: bitmask, scrambled tag, reserved
        System.Text.Encoding.ASCII.GetBytes("ACROSS&DOWN\0").CopyTo(header, 0x02);
        System.Text.Encoding.ASCII.GetBytes("1.4\0").CopyTo(header, 0x18);

        ushort cCib = CksumBytes(header, 0x2C, 8, 0);
        ushort cSol = CksumBytes(solution, 0, solution.Length, 0);
        ushort cGrid = CksumBytes(player, 0, player.Length, 0);

        ushort cPart = 0;

        if (title.Length > 0)
        {
            cPart = CksumZ(title, cPart);
        }

        if (author.Length > 0)
        {
            cPart = CksumZ(author, cPart);
        }

        if (copyright.Length > 0)
        {
            cPart = CksumZ(copyright, cPart);
        }

        foreach (var clue in orderedClues)
        {
            cPart = Cksum(clue, cPart);
        }

        if (notes.Length > 0)
        {
            cPart = CksumZ(notes, cPart);
        }

        // Masked checksums XOR "ICHEATED"
        byte[] magic = "ICHEATED"u8.ToArray();

        (ushort ck, int idx)[] masked =
        [
            (cCib, 0), (cSol, 1), (cGrid, 2), (cPart, 3)
        ];

        foreach (var (ck, idx) in masked)
        {
            header[0x10 + idx] = (byte)(magic[idx] ^ (ck & 0xFF));
            header[0x14 + idx] = (byte)(magic[4 + idx] ^ ((ck >> 8) & 0xFF));
        }

        header[0x0E] = (byte)(cCib & 0xFF);
        header[0x0F] = (byte)((cCib >> 8) & 0xFF);

        // Overall checksum
        ushort overall = cCib;

        overall = CksumBytes(solution, 0, solution.Length, overall);
        overall = CksumBytes(player, 0, player.Length, overall);

        if (title.Length > 0)
        {
            overall = CksumZ(title, overall);
        }

        if (author.Length > 0)
        {
            overall = CksumZ(author, overall);
        }

        if (copyright.Length > 0)
        {
            overall = CksumZ(copyright, overall);
        }

        foreach (var clue in orderedClues)
        {
            overall = Cksum(clue, overall);
        }

        if (notes.Length > 0)
        {
            overall = CksumZ(notes, overall);
        }

        header[0x00] = (byte)(overall & 0xFF);
        header[0x01] = (byte)((overall >> 8) & 0xFF);

        using var fs = File.Create(path);

        using var bw = new BinaryWriter(fs);

        bw.Write(header);
        bw.Write(solution);
        bw.Write(player);

        void Zs(string s)
        {
            bw.Write(Ascii(s));
            bw.Write((byte)0);
        }

        Zs(title);
        Zs(author);
        Zs(copyright);

        foreach (var clue in orderedClues)
        {
            Zs(clue);
        }

        Zs(notes);
    }

    public static string TextOutput(CrosswordGrid grid)
    {
        var (minR, maxR, minC, maxC) = grid.Bounds();

        int w = maxC - minC + 1;
        int h = maxR - minR + 1;

        var lines = new List<string>();

        for (int r = minR; r <= maxR; r++)
        {
            var sb = new System.Text.StringBuilder();

            for (int c = minC; c <= maxC; c++)
            {
                if (grid.Cells.TryGetValue((r, c), out char ch))
                {
                    sb.Append(ch);
                }
                else
                {
                    sb.Append('.');
                }
            }

            lines.Add(sb.ToString());
        }

        return string.Join("\n", lines);
    }
}
