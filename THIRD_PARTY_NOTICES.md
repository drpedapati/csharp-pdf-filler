# Third-Party Notices

## Runtime dependency

- `PDFsharp 6.2.4`
  - License: MIT
  - Use in this repo: PDF parsing, AcroForm traversal, fill operations, save path, and related low-level document handling

## Vendored reference material

- `Apache PDFBox 3.0.6`
  - Location: `reference-src/pdfbox/`
  - License: Apache License 2.0
  - Use in this repo: architectural and behavioral reference material only

- `current-syncfusion-tool`
  - Location: `reference-src/current-syncfusion-tool/`
  - Use in this repo: archived comparison target for compatibility tests only
  - This is not a runtime dependency of the replacement CLI

## Fixtures and exploratory forms

The repo also contains:
- frozen compatibility fixtures
- known-good filled reference outputs
- exploratory public-source forms for regression hardening

Where possible, provenance and source URLs are documented alongside those assets in:
- `fixtures/README.md`
- `fixtures/exploratory-downloads/catalog.json`

These artifacts are for testing and validation, not runtime distribution dependencies.

## Privacy note

Project history and metadata were intentionally scrubbed of personal institutional identifiers before public release.
