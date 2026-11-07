using AICrosswordGen.Models;

namespace AICrosswordGen.Services;

public static class AcrossTextWriter
{
    static string Sanitize(string s)
    {
        s = s.Replace('\r', ' ').Replace('\n', ' ');

        return s.StartsWith("<") ? " " + s : s;
    }

    public static string Text(CrosswordGrid grid, string title = "AICrosswordGen", string author = "", string copyright = "", string notes = "",
        IReadOnlyList<string?>? acrossClues = null, IReadOnlyList<string?>? downClues = null)
    {
        var (numbers, across, down) = PuzzleClues.Get(grid);

        var (minR, maxR, minC, maxC) = grid.Bounds();

        int w = maxC - minC + 1;
        int h = maxR - minR + 1;

        var sb = new System.Text.StringBuilder();

        sb.AppendLine("<ACROSS PUZZLE>");
        sb.AppendLine("<TITLE>");
        sb.AppendLine(title);
        sb.AppendLine("<AUTHOR>");
        sb.AppendLine(author);
        sb.AppendLine("<COPYRIGHT>");
        sb.AppendLine(copyright);
        sb.AppendLine("<SIZE>");
        sb.AppendLine($"{w}x{h}");
        sb.AppendLine("<GRID>");
        sb.AppendLine(PuzWriter.TextOutput(grid));

        sb.AppendLine("<ACROSS>");

        for (int i = 0; i < across.Count; i++)
        {
            string? clue = acrossClues != null && i < acrossClues.Count ? acrossClues[i] : null;

            sb.AppendLine(string.IsNullOrWhiteSpace(clue) ? "" : Sanitize(clue));
        }

        sb.AppendLine("<DOWN>");

        for (int i = 0; i < down.Count; i++)
        {
            string? clue = downClues != null && i < downClues.Count ? downClues[i] : null;

            sb.AppendLine(string.IsNullOrWhiteSpace(clue) ? "" : Sanitize(clue));
        }

        if (!string.IsNullOrWhiteSpace(notes))
        {
            sb.AppendLine("<NOTEPAD>");
            sb.AppendLine(notes);
        }

        return sb.ToString();
    }
}
