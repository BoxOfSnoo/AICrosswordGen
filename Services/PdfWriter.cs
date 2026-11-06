using AICrosswordGen.Models;
using System.Globalization;
using System.Text;

namespace AICrosswordGen.Services;

/// <summary>Minimal blank crossword PDF (no dependencies): grid + numbers + clue lists.</summary>
public static class PdfWriter
{
    public static void WriteBlank(CrosswordGrid grid, string path, string title = "AICrosswordGen")
    {
        if (grid.Cells.Count == 0) throw new InvalidOperationException("Empty grid");
        var (numbers, across, down) = PuzzleClues.Get(grid);
        var (minR, maxR, minC, maxC) = grid.Bounds();
        int w = maxC - minC + 1, h = maxR - minR + 1;
        double cell = Math.Min(400.0 / w, 400.0 / h);
        double ox = (612 - cell * w) / 2, oy = 710 - cell * h;

        var content = new StringBuilder();
        void Text(double x, double y, double size, string s)
        {
            s = s.Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)");
            content.Append($"BT /F1 {size.ToString(CultureInfo.InvariantCulture)} Tf {x.ToString(CultureInfo.InvariantCulture)} {y.ToString(CultureInfo.InvariantCulture)} Td ({s}) Tj ET\n");
        }

        Text(ox, 740, 14, title);
        Text(ox, 725, 9, "Blank crossword - letters omitted");

        for (int r = 0; r < h; r++)
            for (int c = 0; c < w; c++)
            {
                double x = ox + c * cell, y = oy + (h - 1 - r) * cell;
                bool white = grid.Cells.ContainsKey((minR + r, minC + c));
                if (!white)
                    content.Append($"q 0 g {x:F2} {y:F2} {cell:F2} {cell:F2} re f Q\n");
                else
                {
                    content.Append($"q 0 G 0.5 w {x:F2} {y:F2} {cell:F2} {cell:F2} re S Q\n");
                    if (numbers[r, c] > 0) Text(x + 1, y + cell - 7, 6, numbers[r, c].ToString(CultureInfo.InvariantCulture));
                }
            }

        double ty = oy - 20;
        double col2 = ox + cell * w / 2 + 20;
        Text(ox, ty, 12, "Across"); ty -= 12;
        int ai = 0;
        foreach (var s in across)
        {
            if (ty < 40) { Text(ox, ty, 7, $"... and {across.Count - ai} more"); break; }
            Text(ox, ty, 7, s); ty -= 9; ai++;
        }
        double dty = oy - 20;
        Text(col2, dty, 12, "Down"); dty -= 12;
        int di = 0;
        foreach (var s in down)
        {
            if (dty < 40) { Text(col2, dty, 7, $"... and {down.Count - di} more"); break; }
            Text(col2, dty, 7, s); dty -= 9; di++;
        }

        var objs = new List<string>
        {
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /Font << /F1 4 0 R >> >> /Contents 5 0 R >>",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>",
            $"<< /Length {Encoding.Latin1.GetByteCount(content.ToString())} >>\nstream\n{content}endstream"
        };
        var pdf = new StringBuilder("%PDF-1.4\n");
        var offsets = new List<int>();
        for (int i = 0; i < objs.Count; i++)
        {
            offsets.Add(pdf.Length);
            pdf.Append($"{i + 1} 0 obj\n{objs[i]}\nendobj\n");
        }
        int xref = pdf.Length;
        pdf.Append($"xref\n0 {objs.Count + 1}\n0000000000 65535 f \n");
        foreach (var off in offsets) pdf.Append($"{off:D10} 00000 n \n");
        pdf.Append($"trailer\n<< /Size {objs.Count + 1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n");
        File.WriteAllBytes(path, Encoding.Latin1.GetBytes(pdf.ToString()));
    }
}
