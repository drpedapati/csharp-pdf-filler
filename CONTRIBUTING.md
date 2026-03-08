# Contributing

## Scope

This project is a CLI replacement for AcroForm inspection and fill workflows.
Keep contributions focused on:
- CLI compatibility
- AcroForm correctness
- fixture-backed regression coverage
- release and packaging reliability

## Development Setup

```bash
dotnet build PdfFormFiller.slnx
dotnet test PdfFormFiller.slnx
```

Live old-vs-new equivalence coverage is optional and requires `SYNCFUSION_LICENSE_KEY`.
You can provide it either through the environment or a repo-local `.env.local`.

## Pull Request Guidelines

- Add or update tests for behavior changes.
- Prefer public or sanitized PDFs only.
- Do not attach real patient, member, claims, or other regulated documents to issues or PRs.
- Put new public-form edge cases in `fixtures/exploratory-downloads/` first.
- Treat `reference-src/` as vendored reference material, not application code.
- Keep `--flatten` behavior conservative and viewer-stable.

## Fixture Guidance

- The frozen compatibility target lives under `fixtures/corpus-originals/` and `fixtures/reference-fills/`.
- Exploratory public-source forms live under `fixtures/exploratory-downloads/`.
- If a new form exposes an intentional improvement over the old implementation, document that explicitly instead of silently broadening the compatibility contract.

## Before Opening a PR

Run:

```bash
dotnet test PdfFormFiller.slnx
dotnet build PdfFormFiller.slnx -c Release
dotnet test PdfFormFiller.slnx -c Release --no-build
```

If you changed release packaging or Homebrew assets, also review [docs/RELEASING.md](docs/RELEASING.md).
