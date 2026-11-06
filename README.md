# AICrosswordGen

Turns a plain word list into a dense, square, newspaper-style crossword puzzle you can view, edit and export.

It works on Windows, Linux and macOS. It is built with C# / .NET 10 and Avalonia (a free, cross-platform GUI framework).

## What it does

1. You give it words, for example one word per line in a `.txt` file.
2. It searches for a small square layout (16–24 cells per side) where words cross each other.
3. Every 3+ letter run in the grid is a real word from your list — no nonsense words.
4. It shows the puzzle in the centre, Across clues on the left, Down clues on the right.
5. It can save the puzzle as `.puz` (Across Lite format), a blank PDF with clues, or plain text.

See [AI_POLICY.md](AI_POLICY.md) for how AI tools may be used on this project.

## Quick start

```bash
dotnet run
```

1. Press **File > Load Word List** and pick a `.txt` file (one word per line).
   If you skip this, a built-in demo word list is used.
   If a `words.txt` file sits next to the exe at start-up, it is loaded for you.
2. Press **File > Generate**. A progress bar shows `iteration / total` in the bottom status bar.
3. Read the result in the centre panel. Below the list you see size, density and how many words were used/unused.

## Menus

**File**
- **Load Word List** — open a `.txt` file.
- **Generate** — builds a new puzzle.
- **Save PUZ** — Across Lite binary format (".puz").
- **Save Blank PDF** — printable grid with blank cells, numbers and clue lists.
- **Save Text** — grid text plus Across/Down lists.
- **Quit**

**Help**
- **Options** — see below.
- **About** — version info.

## Options (Help > Options)

- **Iterations** — how many layout attempts the solver tries. More = denser puzzles, slower.
  Default 30. Keep it low for a quick try, higher for a better puzzle.
- **Seed** — random number. Generate adds 1 to the seed each time, so note the seed shown before you press Generate if you want to repeat that puzzle.
- **Use filler words** — when on, short words (3–7 letters) from the word lists in `Resources/` can fill empty spaces inside the square without growing it. This raises density a lot.

## Start-up files and `Resources/`

- `Resources/google-10000-english-no-swears.txt` — common words, used first as filler.
- `Resources/words_alpha.txt` — large dictionary, used as filler fallback.
- `daniel.txt`, `ezekiel.txt`, `words.txt` and other root `*.txt` files are local input files. They are ignored by git (see `.gitignore`).

## Test (no GUI)

```bash
dotnet run -- --test-pdf
```

Runs a tiny self-check (valid grid + filler filter), then writes `test.puz`, `test.pdf` and `test.txt` and exits.

## Build a single-file exe

Windows:

```powershell
.\build.ps1
```

Other platforms:

```powershell
.\build.ps1 -Runtime linux-x64
.\build.ps1 -Runtime osx-x64
.\build.ps1 -Runtime osx-arm64
```

Output lands in `bin/Release/net10.0/<rid>/publish/` — one self-contained exe, no extra DLLs to copy.

## Credits & licences

- **Across Lite `.puz` file format** — documented at [code.google.com/archive/p/puz/wikis/FileFormat.wiki](https://code.google.com/archive/p/puz/wikis/FileFormat.wiki). Used by `Services/PuzWriter.cs` for the `.puz` export.
- **Across Lite text format** — documented at [www.litsoft.com/across/docs/AcrossTextFormat.pdf](https://www.litsoft.com/across/docs/AcrossTextFormat.pdf). Used by `Services/AcrossTextWriter.cs` for ".txt (Across Lite format)".
- **google-10000-english word list** — [github.com/first20hours/google-10000-english](https://github.com/first20hours/google-10000-english) (MIT, see that repo for licence text). Wrapped as `Resources/google-10000-english-no-swears.txt` and used as filler words.

## Project layout

- `Views/` — windows: main window, Options, About, Confirm.
- `Models/` — grid data (`Grid.cs`): cells, bounds, validity check, candidate placements.
- `Services/` — solver, puzzle service + filler, `.puz` writer, PDF writer, clue numbering, settings/blocklist storage.
- `Program.cs`, `App.axaml` — start-up.
