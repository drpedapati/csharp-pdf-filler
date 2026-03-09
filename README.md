# pdf-form-filler

`pdf-form-filler` is a Syncfusion-free CLI for inspecting, schema-exporting, filling, and optionally flattening true AcroForm PDFs.

This project is intentionally narrow. It is built for real AcroForm workflows where you want JSON in, a filled PDF out, and a predictable command-line surface you can automate.

What it does well:
- inspect whether a PDF is actually a supported AcroForm
- export a stable schema for the detected fields
- fill text, checkbox, combo, list, and radio fields from JSON
- keep output editable by default so a human can review it
- flatten only when you are ready to lock the form down

What it does not promise:
- full XFA support in the normal product path
- OCR
- signature authoring
- general PDF editing outside the form workflow

## Install

### Homebrew

```bash
brew tap drpedapati/tools
brew install pdf-form-filler
```

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

### Local .NET tool package

```bash
dotnet pack src/PdfFormFiller.Cli/PdfFormFiller.Cli.csproj -c Release -o ./artifacts/packages
dotnet tool install --tool-path ./.tools/pdf-form-filler \
  --add-source ./artifacts/packages \
  pdf-form-filler

./.tools/pdf-form-filler/pdf-form-filler --help
```

## Quick Start

If you cloned this repo, you can try the tool right now against a public court form that is already checked in here.

Inspect the sample form:

```bash
pdf-form-filler inspect \
  --pdf fixtures/exploratory-downloads/legal/ao399-waiver-of-service.pdf \
  --json
```

Expected shape:

```json
{
  "FormType": "acroform",
  "IsXfaForm": false,
  "IsSupportedAcroForm": true,
  "CanFillValues": true,
  "SupportedFillableFieldCount": 13,
  "ValidationMessage": "PDF contains a supported AcroForm."
}
```

Fill it with the included sample values:

```bash
pdf-form-filler fill \
  --pdf fixtures/exploratory-downloads/legal/ao399-waiver-of-service.pdf \
  --values docs/examples/ao399-values.json \
  --out /tmp/ao399-filled.pdf \
  --json
```

Expected shape:

```json
{
  "FormType": "acroform",
  "Flattened": false,
  "AppliedFields": 9,
  "SkippedFields": [],
  "UnusedInputKeys": []
}
```

Check the filled output:

```bash
pdf-form-filler inspect --pdf /tmp/ao399-filled.pdf --json
```

There is a fuller walkthrough, with exact sample JSON and an XFA rejection example, in [docs/QUICKSTART.md](docs/QUICKSTART.md).

## Common Commands

```bash
pdf-form-filler inspect --pdf form.pdf --json
pdf-form-filler schema --pdf form.pdf
pdf-form-filler fill --pdf form.pdf --values values.json --out filled.pdf --json
pdf-form-filler fill --pdf form.pdf --values values.json --out final.pdf --flatten
```

Values JSON usually looks like this:

```json
{
  "FieldName": "text value",
  "CheckboxField": true,
  "RadioField": "OptionValue",
  "ListField": ["A", "B"]
}
```

Two practical notes:
- For combo and list fields, use the exact values shown in `schema` output under `Choices`.
- `--flatten` is off by default. Leave the PDF editable while a human is reviewing it.

## Behavior You Should Expect

- non-form PDFs are rejected
- XFA forms are rejected by default
- `--license-key` is accepted for backward CLI compatibility, but it is a no-op in the new implementation
- `--experimental-xfa` exists only for comparison and visual-review workflows; it is not the normal production path

If you want the user-facing command guide, see [docs/CLI.md](docs/CLI.md).
If you want the strict compatibility surface, see [docs/COMPATIBILITY_CONTRACT.md](docs/COMPATIBILITY_CONTRACT.md).

## Status

- Runtime dependency on Syncfusion is gone.
- The replacement is validated against the frozen payer compatibility corpus and known-good reference fills.
- Live old-vs-new regression tests are available when `SYNCFUSION_LICENSE_KEY` is set for the archived reference implementation.
- Homebrew install is live through `drpedapati/tools`.

## Development

Core commands:

```bash
dotnet build PdfFormFiller.slnx
dotnet test PdfFormFiller.slnx
dotnet test PdfFormFiller.slnx -c Release --no-build
```

Optional live equivalence coverage:

```bash
dotnet test PdfFormFiller.slnx --filter FullyQualifiedName~CliEquivalenceTests
```

That path requires `SYNCFUSION_LICENSE_KEY`, either in the environment or in a repo-local `.env.local`.

## Repository Layout

- [src/PdfFormFiller.Cli](src/PdfFormFiller.Cli)
  production CLI implementation
- [tests/PdfFormFiller.Cli.Tests](tests/PdfFormFiller.Cli.Tests)
  fixture-backed regression and equivalence tests
- [fixtures/README.md](fixtures/README.md)
  frozen corpus, reference fills, and exploratory public forms
- [docs/README.md](docs/README.md)
  quickstart, CLI guide, release guide, and compatibility references
- [reference-src/](reference-src)
  vendored reference material only, not runtime code
- [notes/](notes)
  experimental and historical notes outside the production contract

## Sensitive Data

Do not open public issues with real patient, member, claims, or other regulated PDFs.
Use sanitized samples or public forms only.
