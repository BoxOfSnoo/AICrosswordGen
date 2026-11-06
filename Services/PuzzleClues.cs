using AICrosswordGen.Models;
using System.Text;

namespace AICrosswordGen.Services;

public static class PuzzleClues
{
    public static (int[,] Numbers, List<string> Across, List<string> Down) Get(CrosswordGrid grid)
    {
        var (minR, maxR, minC, maxC) = grid.Bounds();
        int w = maxC - minC + 1, h = maxR - minR + 1;
        bool White(int r, int c) => grid.Cells.ContainsKey((minR + r, minC + c));
        var numbers = new int[h, w];
        var across = new List<string>();
        var down = new List<string>();
        int number = 0;
        for (int r = 0; r < h; r++)
            for (int c = 0; c < w; c++)
            {
                if (!White(r, c)) continue;
                bool startAcross = (c == 0 || !White(r, c - 1))
                                   && (c + 1 < w && White(r, c + 1));
                bool startDown = (r == 0 || !White(r - 1, c))
                                 && (r + 1 < h && White(r + 1, c));
                if (!startAcross && !startDown) continue;
                number++;
                numbers[r, c] = number;
                if (startAcross)
                {
                    var sb = new StringBuilder();
                    for (int cc = c; cc < w && White(r, cc); cc++)
                        sb.Append(grid.Cells[(minR + r, minC + cc)]);
                    across.Add($"{number}. {sb}");
                }
                if (startDown)
                {
                    var sb = new StringBuilder();
                    for (int rr = r; rr < h && White(rr, c); rr++)
                        sb.Append(grid.Cells[(minR + rr, minC + c)]);
                    down.Add($"{number}. {sb}");
                }
            }
        return (numbers, across, down);
    }
}
