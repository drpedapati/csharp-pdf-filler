# Drop-In Replacement Prompt

Build a new Syncfusion-free implementation of `pdf-form-filler` in C#/.NET inside this workspace.

## Objective

Create a new `pdf-form-filler` binary that is operationally indistinguishable from the current Syncfusion-backed one for the scope we actually use:
- inspect whether a PDF is a true usable AcroForm
- export schema for detected fields
- fill supported AcroForm fields from JSON values
- optionally flatten the resulting form
- reject non-form PDFs, XFA forms, and empty/broken pseudo-forms cleanly

The new implementation must not depend on Syncfusion at runtime.

Use the copied Apache PDFBox source as the primary reference architecture and the copied current C# source snapshot plus fixtures as the compatibility target.

## Non-goals

Do not try to recreate all of Syncfusion or all of PDFBox.
Do not chase signatures, full PDF rendering, OCR, or general-purpose document manipulation unless directly required by the current CLI compatibility surface.

This project is an AcroForm-focused replacement.

## Ground truth references in this workspace

Current tool snapshot:
- `reference-src/current-syncfusion-tool/PdfFormFiller.Cli/`
- `reference-src/current-syncfusion-tool/PdfFormFiller.Cli.Tests/`

PDFBox reference source:
- `reference-src/pdfbox/pdfbox-3.0.6/`

Compatibility docs:
- `docs/COMPATIBILITY_CONTRACT.md`
- `docs/PDFBOX_REFERENCE_MAP.md`
- `fixtures/README.md`

Fixture corpus:
- `fixtures/corpus-originals/`
- `fixtures/reference-fills/`
- `fixtures/corpus-report.csv`
- `fixtures/corpus-summary.json`

LLM sanity case:
- `fixtures/llm-harness-sanity/bcbsks-qwen9b-simple/`

## Required deliverable

A working C#/.NET CLI named `pdf-form-filler` that preserves the observable behavior documented in `docs/COMPATIBILITY_CONTRACT.md` and passes against the copied fixture set.

## High-level implementation strategy

### 1. Preserve the public CLI first

Mirror the current command surface immediately:
- `inspect`
- `schema`
- `fill`
- same required flags
- same JSON top-level output shapes
- same exit code conventions

You may keep `--license-key` as a deprecated/no-op compatibility flag.

### 2. Build an internal AcroForm object model

Before writing fill logic, build an internal representation for:
- document-level form state
- AcroForm root
- field tree
- terminal vs non-terminal fields
- widgets
- field kinds (`text`, `checkbox`, `combo`, `list`, `radio`, `signature`, unknown)
- current value and available choices

Do not implement the CLI directly against raw COS dictionaries everywhere. Wrap the PDF objects in your own model so the replacement remains debuggable and testable.

### 3. Use PDFBox as the design template

Use PDFBox source to guide:
- field tree traversal
- widget handling
- appearance stream strategy
- flattening strategy
- NeedAppearances behavior
- radio and checkbox export value handling

But reimplement in idiomatic C#.

### 4. Match the existing compatibility scope before expanding it

The order of priorities is:
1. match current inspect behavior
2. match current schema behavior
3. match current fill behavior on the `11` passing reference forms
4. preserve intentional rejection of the `13` unsupported forms
5. only after parity, consider expanding support

### 5. Build from the fixtures, not vibes

For each roundtrip-passing fixture in `fixtures/reference-fills/<slug>/`:
- compare `schema.json`
- compare `reference-values.json`
- run your fill implementation
- verify no skipped or unused keys
- visually compare your output to `reference-filled.pdf`

For unsupported fixtures:
- verify your `inspect` result still lands in the same unsupported category with a defensible message

## Technical requirements

### Supported field operations

Implement robust support for:
- text fields
- checkbox fields
- radio button groups
- combo boxes
- list boxes

Expose signature fields in inspect/schema, but do not claim fill support unless you truly implement it.

### Widget-aware fill

The implementation must handle the fact that field values and widget appearances are not the same thing.
A correct solution must account for:
- field dictionaries
- widget annotations
- appearance state for buttons
- appearance regeneration or explicit appearance construction for text/choice fields

### XFA handling

The replacement must explicitly detect XFA and reject it for fill.
Do not silently treat XFA as AcroForm.

### Flattening

Implement flattening conservatively:
- preserve visible output
- remove interactive field behavior in flattened output
- do not claim flatten success unless the output is visually stable in common viewers

### NeedAppearances

Treat `NeedAppearances` as an interoperability problem, not a magic switch.
You should understand how PDFBox handles it and decide whether the replacement should:
- generate appearances directly
- set or clear `NeedAppearances`
- or both

The final behavior should prefer reliable viewer output over minimal implementation effort.

## Suggested implementation phases

### Phase 1: CLI skeleton and object inspection

Build:
- project structure
- command parsing
- inspect output shape
- schema output shape
- file existence preflight

Acceptance:
- `inspect` and `schema` run without Syncfusion
- supported vs unsupported classification starts to resemble current behavior

### Phase 2: AcroForm parser and field tree

Build:
- parse AcroForm root
- detect XFA
- enumerate fields and widgets
- compute fully-qualified field names
- infer field kind and choices

Acceptance:
- `schema` on roundtrip fixtures produces the same field names and broadly the same field kinds

### Phase 3: Field writing

Build:
- text value writing
- checkbox on/off handling
- radio export value selection
- combo/list selection
- skipped/unused accounting

Acceptance:
- `fill` works on the simplest roundtrip forms first:
  - `bcbsks-prior-authorization-request`
  - `aetna-nh-rx-prior-auth`
  - `bcbsil-bcchp-uniform-pa`

### Phase 4: Appearance generation and flattening

Build:
- text/choice appearance regeneration
- button appearance state handling
- flatten support

Acceptance:
- viewer-stable output on the reference fills
- flattened outputs behave correctly

### Phase 5: Corpus hardening

Run against the full copied corpus.
Fix edge cases until the replacement reaches parity or better.

## Acceptance criteria

You are done only when all of the following are true:

1. The tool builds as a normal C#/.NET CLI without Syncfusion.
2. The command name is still `pdf-form-filler`.
3. `inspect`, `schema`, and `fill` preserve the public behavior in `docs/COMPATIBILITY_CONTRACT.md`.
4. The copied tests are preserved or ported and pass.
5. The `11` known-good reference forms still roundtrip successfully.
6. The `13` unsupported forms are still rejected intentionally and cleanly.
7. The replacement does not depend on hidden machine-local state or proprietary license keys.

## Practical file-by-file reading order

Read these first:
- `docs/COMPATIBILITY_CONTRACT.md`
- `fixtures/README.md`
- `reference-src/current-syncfusion-tool/PdfFormFiller.Cli/CommandLineParser.cs`
- `reference-src/current-syncfusion-tool/PdfFormFiller.Cli/App.cs`
- `reference-src/current-syncfusion-tool/PdfFormFiller.Cli/Models.cs`
- `reference-src/current-syncfusion-tool/PdfFormFiller.Cli/PdfFormService.cs`
- `reference-src/current-syncfusion-tool/PdfFormFiller.Cli.Tests/`

Then read these PDFBox files:
- `PDAcroForm.java`
- `PDField.java`
- `PDFieldTree.java`
- `PDTerminalField.java`
- `PDNonTerminalField.java`
- `PDTextField.java`
- `PDCheckBox.java`
- `PDRadioButton.java`
- `PDComboBox.java`
- `PDListBox.java`
- `AppearanceGeneratorHelper.java`
- example files under `examples/interactive/form/`

## Key design warning

Do not optimize for elegance at the expense of compatibility.
This replacement must be judged by real PDFs, real field trees, and actual viewer output, not by how minimal the code looks.

## Stretch goal

After parity, add a backend-independent internal test harness so future engines can be compared against the same fixture set without rewriting the CLI tests.
