# Experimental XFA Runs

This folder holds the reproducible workflow for best-effort XFA comparison runs.

The production CLI still rejects XFA by default.
The experimental path is opt-in with:

```bash
dotnet run --project src/PdfFormFiller.Cli -- \
  fill --pdf <xfa.pdf> --values <values.json> --out <filled.pdf> --experimental-xfa
```

Use the runner below to:
- generate deterministic sample values for XFA fixtures
  - the generator uses field-name/tooltip heuristics so tiny date/hour/state fields get plausible short values
- fill each fixture with the old Syncfusion CLI and the new CLI in experimental mode
- save both PDFs side by side
- capture JSON summaries
- render every page to PNG and build page-by-page old/new compare images for visual review

## Run

```bash
python3 notes/xfa-experimental/run_experiment.py
```

Optional flags:

```bash
python3 notes/xfa-experimental/run_experiment.py --ids irs-w4-2026 va-21-526ez
python3 notes/xfa-experimental/run_experiment.py --limit 3
python3 notes/xfa-experimental/run_experiment.py --output-root notes/generated/xfa-experimental/manual-check
```

## Output

Each run is written under `notes/generated/xfa-experimental/<timestamp>/`.

Per fixture:
- `input.pdf`
- `generated-values.json`
- `old-filled.pdf`
- `new-filled-experimental-xfa.pdf`
- `old-fill-result.json`
- `new-fill-result.json`
- `old-inspect-old-output.json`
- `old-inspect-new-output.json`
- `new-inspect-old-output.json`
- `new-inspect-new-output.json`
- `old-page-1.png`, `old-page-2.png`, ...
- `new-page-1.png`, `new-page-2.png`, ...
- `compare-page-1.png`, `compare-page-2.png`, ...
- `comparison.json`

Run-level files:
- `summary.json`

## Interpretation

This workflow is intentionally exploratory.
It does not change the main compatibility contract.

Use it to answer practical questions such as:
- does best-effort XFA filling complete without crashing?
- do old and new outputs look similar enough for human workflows?
- are there XFA families where the new output is obviously worse or obviously acceptable?
