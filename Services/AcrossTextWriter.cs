using AICrosswordGen.Models;

namespace AICrosswordGen.Services;

public static class AcrossTextWriter
{
    public static string Text(CrosswordGrid grid, string title = "AICrosswordGen", string author = "", string copyright = "", string notes = "")
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

        foreach (var s in across)
        {
            int dot = s.IndexOf(". ");

            sb.AppendLine(s[(dot + 2)..]);
        }

        sb.AppendLine("<DOWN>");

        foreach (var s in down)
        {
            int dot = s.IndexOf(". ");

            sb.AppendLine(s[(dot + 2)..]);
        }

        if (!string.IsNullOrWhiteSpace(notes))
        {
            sb.AppendLine("<NOTEPAD>");
            sb.AppendLine(notes);
        }

        return sb.ToString();
    }
}
