using AICrosswordGen.Models;
using AICrosswordGen.Services;
using Avalonia;

namespace AICrosswordGen;

public static class Program
{
    public static void Main(string[] args)
    {
        if (args.Contains("--test-pdf"))
        {
            // self-check: trim/filler validity and filler filter
            var g = new CrosswordGrid(new[] { "CRANE", "CAT" });
            g.Place(new Placement("CRANE", 0, 0, true, 0, 5, 0, 0));
            g.Place(new Placement("CAT", 0, 0, false, 1, 2, 0, 0));
            if (!g.IsValid()) throw new Exception("IsValid failed");
            var fillers = new[] { "AAA", "GOLD" };
            var ok = fillers.Where(w => w.Distinct().Count() > 1 && w.Any(c => "AEIOU".Contains(c))).ToArray();
            if (ok.Contains("AAA") || !ok.Contains("GOLD")) throw new Exception("Filler filter failed");
            var primary = PuzzleService.FindResource("google-10000-english-no-swears.txt");
            if (primary == null) throw new Exception("Google filler missing");
            var best = PuzzleService.Generate(DemoWords.List, new AppSettings(Iterations: 5));
            if (!best.Grid.IsValid()) throw new Exception("Generated invalid");
            PuzWriter.Write(best.Grid, "test.puz", title: "Test");
            PdfWriter.WriteBlank(best.Grid, "test.pdf", title: "Test");
            var (numbers, across, down) = PuzzleClues.Get(best.Grid);
            File.WriteAllText("test.txt", PuzWriter.TextOutput(best.Grid) + "\n\nAcross\n" + string.Join("\n", across));
            Console.WriteLine($"test ok density {best.Density:P1}");
            return;
        }
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }
    public static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder.Configure<App>().UsePlatformDetect().LogToTrace();
    }
}
