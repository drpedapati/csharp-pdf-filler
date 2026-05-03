# Changelog

All notable changes to this project should be recorded here.

## Unreleased

- Improved public-facing docs, quickstart coverage, and command guide.

## 0.2.1 - 2026-05-03

- Fixed Linux font resolution so AcroForm inspection and filling no longer crash when PDFsharp materializes text fields in containerized runtimes.
- Added cross-platform fallback font lookup for macOS, Linux DejaVu/Liberation fonts, and Windows fonts.

## 0.1.0 - 2026-03-09

- First public release of the Syncfusion-free `pdf-form-filler` CLI for true AcroForm workflows.
- Shipped `inspect`, `schema`, `fill`, and optional `--flatten` with stable JSON output contracts.
- Validated the replacement against the frozen payer compatibility corpus and known-good reference fill packages.
- Added broader public-form regression coverage across legal, insurance, benefits, and court-form edge cases.
- Kept XFA rejected by default in the production path while preserving a separate experimental comparison workflow.
- Published Homebrew installation through `drpedapati/tools`.
