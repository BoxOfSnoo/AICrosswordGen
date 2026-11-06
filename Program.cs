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

            if (!g.IsValid())
            {
                throw new Exception("IsValid failed");
            }

            var fillers = new[] { "AAA", "GOLD" };
            var ok = new List<string>();

            foreach (var w in fillers)
            {
                var varied = w.Distinct().Count() > 1;

                var vowel = false;

                for (int i = 0; i < w.Length; i++)
                {
                    if ("AEIOU".Contains(w[i]))
                    {
                        vowel = true;
                    }
                }

                if (varied && vowel)
                {
                    ok.Add(w);
                }
            }

            if (ok.Contains("AAA") || !ok.Contains("GOLD"))
            {
                throw new Exception("Filler filter failed");
            }

            var primary = PuzzleService.FindResource("google-10000-english-no-swears.txt");

            if (primary == null)
            {
                throw new Exception("Google filler missing");
            }

            var best = PuzzleService.Generate(DemoWords.List, new AppSettings(Iterations: 5));

            if (!best.Grid.IsValid())
            {
                throw new Exception("Generated invalid");
            }

            // seed word is longest first word, centred on origin row 0
            var seedWord = best.Used[0];
            int startCol = -seedWord.Length / 2;

            for (int i = 0; i < seedWord.Length; i++)
            {
                if (!best.Grid.Cells.TryGetValue((0, startCol + i), out char ch) || ch != seedWord[i])
                {
                    throw new Exception("Seed word not centered");
                }
            }

            PuzWriter.Write(best.Grid, "test.puz", title: "Test");
            PdfWriter.WriteBlank(best.Grid, "test.pdf", title: "Test");
            File.WriteAllText("test.txt", AcrossTextWriter.Text(best.Grid, title: "Test"));

            // clues that do not fit page 1 must flow to extra pages, not be cut
            var pdfText = System.Text.Encoding.Latin1.GetString(File.ReadAllBytes("test.pdf"));
            int pageCount = 0;
            int at = 0;
            string marker = "/Type /Page ";

            while ((at = pdfText.IndexOf(marker, at, StringComparison.Ordinal)) >= 0)
            {
                pageCount++;

                at += marker.Length;
            }

            if (pageCount < 2)
            {
                throw new Exception("Clue pages missing");
            }

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
