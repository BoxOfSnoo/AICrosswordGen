# AICrosswordGen
Dense newspaper-style crossword generator. C# /.NET 10, Avalonia GUI (cross-platform).

## Run
```
dotnet run
```
GUI: Load Word List, Generate, Save PUZ / Blank PDF / Text. Options sets Iterations, Seed, Filler.

## Test (headless)
```
dotnet run -- --test-pdf
```
Writes test.puz/test.pdf/test.txt and exits.

## Publish (single-file)
```
.\build.ps1                      # win-x64 Release (single-file)
.\build.ps1 -Runtime linux-x64   # Linux
.\build.ps1 -Runtime osx-x64
```
Output in bin/Release/net10.0/<rid>/publish/AICrosswordGen.exe

Words: default demo list; if `words.txt` exists it loads instead. Filler uses `Resources/google-10000-english-no-swears.txt` (3-7 letters) first, merged with `Resources/words_alpha.txt` fallback when present.
