# pdf-form-filler

`pdf-form-filler` is a Syncfusion-free .NET CLI for inspecting, schema-exporting, and filling true AcroForm PDFs.

The current focus is practical CLI compatibility for real-world payer and government-style AcroForm workflows:
- inspect whether a PDF is a supported AcroForm
- export a stable schema for detected fields
- fill supported fields from JSON
- optionally flatten the output for final distribution

XFA is rejected by default. Experimental XFA comparison work lives separately under [notes/xfa-experimental/README.md](notes/xfa-experimental/README.md).

## Status

- Runtime dependency on Syncfusion is removed.
- The replacement is validated against the frozen payer compatibility corpus and known-good reference fills.
- Live old-vs-new regression tests are available when `SYNCFUSION_LICENSE_KEY` is set for the archived reference implementation.
- `--flatten` is off by default so filled forms stay editable for human review.

## Install

### Build from source

```bash
dotnet build PdfFormFiller.slnx -c Release
dotnet publish src/PdfFormFiller.Cli/PdfFormFiller.Cli.csproj \
  -c Release \
  -r osx-arm64 \
  --self-contained false \
  -p:PublishSingleFile=true \
  -o ./artifacts/publish/osx-arm64

./artifacts/publish/osx-arm64/pdf-form-filler --help
```

### Local NuGet tool package

```bash
dotnet pack src/PdfFormFiller.Cli/PdfFormFiller.Cli.csproj -c Release -o ./artifacts/packages
dotnet tool install --tool-path ./.tools/pdf-form-filler \
  --add-source ./artifacts/packages \
  pdf-form-filler

./.tools/pdf-form-filler/pdf-form-filler --help
```

### Homebrew tap

The tap itself should live in a separate tap repository.
This repo includes release instructions and a formula template in:
- [docs/RELEASING.md](docs/RELEASING.md)
- [packaging/homebrew/README.md](packaging/homebrew/README.md)
- [packaging/homebrew/pdf-form-filler.rb.template](packaging/homebrew/pdf-form-filler.rb.template)

## Usage

```bash
pdf-form-filler inspect --pdf form.pdf --json
pdf-form-filler schema --pdf form.pdf
pdf-form-filler fill --pdf form.pdf --values values.json --out filled.pdf --json
pdf-form-filler fill --pdf form.pdf --values values.json --out final.pdf --flatten
```

Behavior notes:
- non-form PDFs are rejected
- XFA forms are rejected by default
- `--license-key` is still accepted for backward CLI compatibility, but it is a no-op in the new implementation
- `--flatten` is for final non-editable output, not the default review workflow

## Development

Core commands:

```bash
dotnet build PdfFormFiller.slnx
dotnet test PdfFormFiller.slnx
dotnet test PdfFormFiller.slnx -c Release --no-build
```

Live old-vs-new equivalence tests:
- set `SYNCFUSION_LICENSE_KEY`
- or create a repo-local `.env.local`
- then run `dotnet test PdfFormFiller.slnx --filter FullyQualifiedName~CliEquivalenceTests`

Public release guidance:
- [CONTRIBUTING.md](CONTRIBUTING.md)
- [SECURITY.md](SECURITY.md)
- [CHANGELOG.md](CHANGELOG.md)
- [docs/RELEASING.md](docs/RELEASING.md)

## Repository Layout

- [src/PdfFormFiller.Cli](src/PdfFormFiller.Cli)
  - production CLI implementation
- [tests/PdfFormFiller.Cli.Tests](tests/PdfFormFiller.Cli.Tests)
  - fixture-backed regression and equivalence tests
- [fixtures/README.md](fixtures/README.md)
  - frozen compatibility corpus, reference fills, and exploratory public forms
- [docs/COMPATIBILITY_CONTRACT.md](docs/COMPATIBILITY_CONTRACT.md)
  - exact behavioral contract for the replacement
- [reference-src/](reference-src)
  - vendored reference material only; not part of runtime build
- [notes/](notes)
  - experimental and historical notes, not part of the compatibility contract

## Scope

This repo is intentionally narrow:
- AcroForm inspection, schema extraction, fill, and flatten
- compatibility with the current payer-form workflow
- better handling of real-world AcroForm edge cases

This repo is not promising:
- full XFA support in production mode
- OCR
- signature authoring
- general PDF editing beyond the form workflow

## Sensitive Data

Do not open public issues with real patient, member, claims, or other regulated PDFs.
Use sanitized samples or public forms only.
