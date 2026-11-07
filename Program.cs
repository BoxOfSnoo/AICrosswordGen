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

            // work-file round-trip: escaped clue and clue equal to its answer survive
            File.WriteAllText("test-inprogress.txt", AcrossTextWriter.Text(g, title: "Test", acrossClues: new string?[] { "<5 letters" }, downClues: new string?[] { "CAT" }));

            var (g2, ac, dc) = InProgressStore.Read("test-inprogress.txt");

            if (g2.Cells.Count != g.Cells.Count || !g2.Cells.All(kv => g2.Cells.TryGetValue(kv.Key, out char ch) && ch == kv.Value))
            {
                throw new Exception("In-progress grid mismatch");
            }

            if (ac.Count == 0 || ac[0] != "<5 letters" || dc.Count == 0 || dc[0] != "CAT")
            {
                throw new Exception("In-progress clues mismatch");
            }

            // missing clues round-trip as blank, never as the word itself
            File.WriteAllText("test-inprogress2.txt", AcrossTextWriter.Text(g, title: "Test", acrossClues: new string?[] { null }, downClues: new string?[] { null }));

            var (_, ac2, dc2) = InProgressStore.Read("test-inprogress2.txt");

            if (ac2.Count == 0 || ac2[0] != null || dc2.Count == 0 || dc2[0] != null)
            {
                throw new Exception("Blank clues mismatch");
            }

            // .puz missing clues are blank too (title, author, copyright, 2 clues, notes)
            PuzWriter.Write(g, "test-blank.puz", title: "Test");

            var puz = File.ReadAllBytes("test-blank.puz");
            int atPuz = 0x34 + 2 * 5 * 3;
            var puzStrings = new List<string>();

            for (int i = 0; i < 6; i++)
            {
                int end = Array.IndexOf(puz, (byte)0, atPuz);
                puzStrings.Add(System.Text.Encoding.ASCII.GetString(puz, atPuz, end - atPuz));
                atPuz = end + 1;
            }

            if (puzStrings[0] != "Test" || puzStrings.Skip(1).Any(s => s.Length != 0))
            {
                throw new Exception("Puz blank clues mismatch");
            }

            if (puz[0x30] != 1 || puz[0x31] != 0)
            {
                throw new Exception("Puz bitmask mismatch");
            }

            // PDF: clue-only lines (no answer) plus a 25% solution page
            PdfWriter.WriteBlank(g, "test-clue.pdf", title: "Test", acrossClues: new string?[] { "<5 letters" }, downClues: new string?[] { "feline" });
            var cluePdf = System.Text.Encoding.Latin1.GetString(File.ReadAllBytes("test-clue.pdf"));

            if (!cluePdf.Contains("1. <5 letters") || cluePdf.Contains("CRANE"))
            {
                throw new Exception("Pdf clue lines mismatch");
            }

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

            // small clue PDF: blank page + solution page only, with letters filled in
            int solPages = 0;
            int atSol = 0;

            while ((atSol = cluePdf.IndexOf(marker, atSol, StringComparison.Ordinal)) >= 0)
            {
                solPages++;
                atSol += marker.Length;
            }

            if (solPages != 2 || !cluePdf.Contains("Solution") || !cluePdf.Contains("(C)") || !cluePdf.Contains("(R)"))
            {
                throw new Exception("Solution page missing");
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
