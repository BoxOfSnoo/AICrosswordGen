# AI Policy

This project was built with help from AI code-generation tools (AI assistants).

## Human review is required

AI contributions are welcome, but they only count if a human has:

1. Read the change.
2. Checked it does what it should.
3. Run the build (and, for solver changes, a Generate).

No AI change is merged, published or released on AI's word alone.

## What AI may do

- Suggest code, fixes and tests.
- Explain existing code.
- Generate draft documentation like this file.

## What AI may not do

- Claim its own output is correct without a build/test.
- Invent puzzle words (filler spelling) or fake densities.
- Bypass the Options/seed/blocklist checks to "make it faster" by dropping validity rules (no 2-letter runs, every 3+ run must be a known word).

## How to submit an AI-assisted change

- Say in the commit/PR that AI was used.
- Include the commands you ran (`dotnet build`, `dotnet run -- --test-pdf`, a Generate).
- Keep the diff small. AI output that cannot be read in one sitting will be sent back.
