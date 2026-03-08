# csharp-pdf-filler bootstrap workspace

This folder is a handoff package for building a Syncfusion-free C#/.NET replacement for `pdf-form-filler`.

Goal:
- build a new `pdf-form-filler` binary in C#/.NET
- remove the Syncfusion dependency entirely
- preserve the current CLI surface and behavior closely enough that it is a drop-in replacement for the current tool
- use Apache PDFBox source as the primary reference architecture

Contents:
- `reference-src/pdfbox/pdfbox-3.0.6/`: official Apache PDFBox source release
- `reference-src/current-syncfusion-tool/`: snapshot of the current Syncfusion-based implementation and tests
- `fixtures/`: original payer PDFs, known-good reference outputs, corpus reports, and an LLM harness sanity case
- `docs/`: replacement brief, compatibility contract, PDFBox reference map, and detailed prompt
- `notes/`: supporting notes and implementation guidance

Start here:
- `docs/DROP_IN_REPLACEMENT_PROMPT.md`
- `docs/COMPATIBILITY_CONTRACT.md`
- `docs/PDFBOX_REFERENCE_MAP.md`
- `fixtures/README.md`
