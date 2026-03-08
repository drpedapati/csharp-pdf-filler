# Fixtures

This folder contains the concrete artifacts the replacement must use as its compatibility target.

## Layout

- `corpus-originals/roundtrip-fill-ok/`
  - original payer PDFs that the current Syncfusion tool can inspect and fill successfully
- `corpus-originals/unsupported/`
  - original payer PDFs that the current tool intentionally rejects
- `reference-fills/<slug>/`
  - per-form reference package for each currently passing form
- `exploratory-downloads/`
  - additional public-source legal and insurance PDFs used for edge-case hardening outside the frozen compatibility baseline
- `llm-harness-sanity/bcbsks-qwen9b-simple/`
  - a small end-to-end LLM mapping example on `qwen3.5:9b`
- `corpus-report.csv`
  - merged inspect + fill report
- `corpus-inspect-matrix.csv`
  - inspect-only report
- `corpus-smoke-fill.csv`
  - synthetic fill report
- `corpus-summary.json`
  - summary counts and slug lists

## Reference fill package contents

Each `reference-fills/<slug>/` folder contains:
- `original.pdf`
- `schema.json`
- `reference-values.json`
- `reference-filled.pdf`
- `fill-result.json`
- `source-metadata.json`

Use these to validate the replacement against the current behavior.

## Exploratory downloads

The `exploratory-downloads/` folder is intentionally separate from the compatibility corpus.
Use it to probe new edge cases without silently changing the frozen acceptance target in:
- `corpus-originals/`
- `reference-fills/`

Some exploratory forms are expected to surface intentional improvements over the old Syncfusion tool.
For example, real XFA government forms belong here first, not in the compatibility baseline.

## Important interpretation note

Byte-for-byte identical output PDFs are not required.
What matters is semantic compatibility:
- same inspect classification
- same field discovery shape
- same field names and kinds
- same fill result counts and skipped/unused behavior
- visually correct and viewer-stable output

## LLM harness sanity case

The `llm-harness-sanity` example is included because it exposed a real local-model integration issue:
- `qwen3.5:9b` generated correct JSON for a simple AcroForm mapping task
- but Ollama returned that JSON in `thinking` instead of `response`

That means future agent integration work should not confuse a harness bug with a PDF backend bug.
