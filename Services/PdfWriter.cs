using AICrosswordGen.Models;
using System.Globalization;
using System.Text;

namespace AICrosswordGen.Services;

/// <summary>Minimal blank crossword PDF (no dependencies): grid + numbers + clue lists.</summary>
public static class PdfWriter
{
    public static void WriteBlank(CrosswordGrid grid, string path, string title = "AICrosswordGen",
        IReadOnlyList<string?>? acrossClues = null, IReadOnlyList<string?>? downClues = null)
    {
        if (grid.Cells.Count == 0)
        {
            throw new InvalidOperationException("Empty grid");
        }

        var (numbers, across, down) = PuzzleClues.Get(grid);

        var displayAcross = new List<string>();

        for (int i = 0; i < across.Count; i++)
        {
            string? clue = acrossClues != null && i < acrossClues.Count ? acrossClues[i] : null;

            displayAcross.Add(string.IsNullOrWhiteSpace(clue) ? across[i] : $"{across[i]} \u2013 {clue!.Replace('\r', ' ').Replace('\n', ' ')}");
        }

        var displayDown = new List<string>();

        for (int i = 0; i < down.Count; i++)
        {
            string? clue = downClues != null && i < downClues.Count ? downClues[i] : null;

            displayDown.Add(string.IsNullOrWhiteSpace(clue) ? down[i] : $"{down[i]} \u2013 {clue!.Replace('\r', ' ').Replace('\n', ' ')}");
        }

        var (minR, maxR, minC, maxC) = grid.Bounds();

        int w = maxC - minC + 1;
        int h = maxR - minR + 1;

        double cell = Math.Min(400.0 / w, 400.0 / h);

        double ox = (612 - cell * w) / 2;
        double oy = 710 - cell * h;

        // One StringBuilder per page; cur points at the page being written.
        var pages = new List<StringBuilder> { new StringBuilder() };
        var cur = pages[0];

        void Text(double x, double y, double size, string s)
        {
            s = s.Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)");

            cur.Append($"BT /F1 {size.ToString(CultureInfo.InvariantCulture)} Tf {x.ToString(CultureInfo.InvariantCulture)} {y.ToString(CultureInfo.InvariantCulture)} Td ({s}) Tj ET\n");
        }

        void NewPage()
        {
            cur = new StringBuilder();

            pages.Add(cur);
        }

        Text(ox, 740, 14, title);
        Text(ox, 725, 9, "Blank crossword - letters omitted");

        for (int r = 0; r < h; r++)
        {
            for (int c = 0; c < w; c++)
            {
                double x = ox + c * cell;
                double y = oy + (h - 1 - r) * cell;

                bool white = grid.Cells.ContainsKey((minR + r, minC + c));

                if (!white)
                {
                    cur.Append($"q 0 g {x:F2} {y:F2} {cell:F2} {cell:F2} re f Q\n");
                }
                else
                {
                    cur.Append($"q 0 G 0.5 w {x:F2} {y:F2} {cell:F2} {cell:F2} re S Q\n");

                    if (numbers[r, c] > 0)
                    {
                        Text(x + 1, y + cell - 7, 6, numbers[r, c].ToString(CultureInfo.InvariantCulture));
                    }
                }
            }
        }

        // How many clue lines fit under the grid on page 1?
        int lines = 0;

        for (double y = oy - 20 - 12; y >= 40; y -= 9)
        {
            lines++;
        }

        bool fits = across.Count <= lines && down.Count <= lines;

        if (fits)
        {
            double ty = oy - 20;

            double col2 = ox + cell * w / 2 + 20;

            Text(ox, ty, 12, "Across");

            ty -= 12;

            foreach (var s in displayAcross)
            {
                Text(ox, ty, 7, s);

                ty -= 9;
            }

            double dty = oy - 20;

            Text(col2, dty, 12, "Down");

            dty -= 12;

            foreach (var s in displayDown)
            {
                Text(col2, dty, 7, s);

                dty -= 9;
            }
        }
        else
        {
            // Clues don't fit under the grid: put all of them on page 2+, paginated.
            NewPage();

            Text(ox, 740, 12, "Across");

            double ty = 728;

            foreach (var s in displayAcross)
            {
                if (ty < 40)
                {
                    NewPage();

                    Text(ox, 740, 12, "Across (continued)");

                    ty = 728;
                }

                Text(ox, ty, 7, s);

                ty -= 9;
            }

            ty -= 12;

            if (ty < 40)
            {
                NewPage();

                Text(ox, 740, 12, "Down");

                ty = 728;
            }
            else
            {
                Text(ox, ty, 12, "Down");

                ty -= 12;
            }

            foreach (var s in displayDown)
            {
                if (ty < 40)
                {
                    NewPage();

                    Text(ox, 740, 12, "Down (continued)");

                    ty = 728;
                }

                Text(ox, ty, 7, s);

                ty -= 9;
            }
        }

        int pageCount = pages.Count;

        var pageIds = new List<int>();
        var contentIds = new List<int>();

        for (int i = 0; i < pageCount; i++)
        {
            int pageId = 4 + 2 * i;

            pageIds.Add(pageId);
            contentIds.Add(pageId + 1);
        }

        var kidParts = new List<string>();

        foreach (var id in pageIds)
        {
            kidParts.Add($"{id} 0 R");
        }

        string kids = string.Join(" ", kidParts);

        var objs = new List<string>
        {
            "<< /Type /Catalog /Pages 2 0 R >>",
            $"<< /Type /Pages /Kids [{kids}] /Count {pageCount} >>",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>"
        };

        for (int i = 0; i < pageCount; i++)
        {
            objs.Add($"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /Font << /F1 3 0 R >> >> /Contents {contentIds[i]} 0 R >>");

            var body = pages[i].ToString();

            objs.Add($"<< /Length {Encoding.Latin1.GetByteCount(body)} >>\nstream\n{body}endstream");
        }

        var pdf = new StringBuilder("%PDF-1.4\n");
        var offsets = new List<int>();

        for (int i = 0; i < objs.Count; i++)
        {
            offsets.Add(pdf.Length);

            pdf.Append($"{i + 1} 0 obj\n{objs[i]}\nendobj\n");
        }

        int xref = pdf.Length;

        pdf.Append($"xref\n0 {objs.Count + 1}\n0000000000 65535 f \n");

        foreach (var off in offsets)
        {
            pdf.Append($"{off:D10} 00000 n \n");
        }

        pdf.Append($"trailer\n<< /Size {objs.Count + 1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n");

        File.WriteAllBytes(path, Encoding.Latin1.GetBytes(pdf.ToString()));
    }
}
