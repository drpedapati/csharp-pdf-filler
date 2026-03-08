# Compatibility Contract

The replacement must preserve the observable behavior of the current `pdf-form-filler` CLI.

## Binary identity

- Command name: `pdf-form-filler`
- Primary language/runtime: C# / .NET
- Target usage: CLI tool and local binary, not a library-only rewrite
- The replacement must not require Syncfusion or `SYNCFUSION_LICENSE_KEY`

## Commands and flags

Preserve these commands exactly:

```text
inspect --pdf <file> [--json] [--license-key <key>]
schema  --pdf <file> [--license-key <key>]
fill    --pdf <file> --values <file.json> --out <filled.pdf> [--flatten] [--json] [--license-key <key>]
```

Compatibility note:
- `--license-key` may remain accepted as a no-op for backward CLI compatibility, but the replacement must not require it.

## Help text shape

The current help text documents:
- `inspect`
- `schema`
- `fill`
- strict rejection of non-AcroForm PDFs
- strict rejection of XFA forms
- values JSON examples for text, checkbox, radio, and list fields

The help text does not need to be byte-identical, but it should preserve the same commands, flags, and semantics.

## Exit semantics

- `help` / `--help` / `-h`: exit `0`
- parse error or unknown option/command: exit `2`
- `inspect --json`:
  - exit `0` if `IsSupportedAcroForm == true`
  - exit `1` otherwise
- `schema`:
  - exit `0` on supported AcroForms
  - non-zero on unsupported PDFs
- `fill`:
  - exit `0` on successful fill
  - non-zero on input, validation, or fill failure

## Preflight behavior

The current tool checks:
- `--pdf` file must exist
- `--values` file must exist for `fill`
- missing files raise immediate errors before backend work starts

Preserve this behavior.

## Inspect JSON contract

The `inspect --json` output currently serializes a `FormInspection` record with these fields:

- `PdfPath`
- `FormType`
- `IsXfaForm`
- `IsSupportedAcroForm`
- `CanFillValues`
- `SupportedFillableFieldCount`
- `ValidationMessage`
- `FieldCount`
- `Fields`

Each field entry currently exposes:
- `Name`
- `Kind`
- `ToolTip`
- `ReadOnly`
- `Required`
- `CurrentValue`
- `Choices`

## Schema JSON contract

The `schema` output currently serializes a `FormSchema` record with these fields:
- `PdfPath`
- `FormType`
- `IsXfaForm`
- `IsSupportedAcroForm`
- `CanFillValues`
- `SupportedFillableFieldCount`
- `ValidationMessage`
- `FieldCount`
- `Fields`

Each schema field entry currently exposes:
- `Name`
- `Kind`
- `ToolTip`
- `ReadOnly`
- `Required`
- `Choices`

Important:
- `schema` intentionally omits `CurrentValue`
- preserve this omission

## Fill JSON contract

The `fill --json` output currently serializes a `FillResult` record with:
- `PdfPath`
- `OutputPath`
- `FormType`
- `Flattened`
- `AppliedFields`
- `SkippedFields`
- `UnusedInputKeys`

## Supported field kinds

Current supported fill kinds:
- `text`
- `checkbox`
- `combo`
- `list`
- `radio`

Current unsupported-but-detected kinds include:
- `signature`
- button-like fields
- other field types surfaced by the backend as class/type names

The replacement must preserve the current public kind names above.

## Validation rules to preserve

### Non-form PDF
Return unsupported with message:
- `PDF does not contain a form.`

### XFA form
Return unsupported with message:
- `PDF contains an XFA form. Only true AcroForm PDFs are supported.`

### AcroForm with zero usable fields
Return unsupported with message:
- `Form exists but contains no fields.`
- or, if fields exist but none are supported for fill:
- `PDF contains an AcroForm, but no supported fillable fields were found.`

### Supported AcroForm
Validation message:
- `PDF contains a supported AcroForm.`

## Fill rules to preserve

- Field matching is by field name from JSON keys
- unmatched keys stay in `UnusedInputKeys`
- unsupported or uncoercible field writes go to `SkippedFields`
- if no supported fillable fields exist, fail
- if no input keys match fillable form fields, fail with a message that names unused keys when possible
- `--flatten` must flatten the entire form output

## Behavior expectations on fixtures

The replacement must match the current corpus classification on the copied fixture set in `fixtures/`.

Current corpus baseline:
- `24` official payer PDFs
- `11` `roundtrip-fill-ok`
- `13` `unsupported`

Roundtrip-passing fixture slugs:
- `aetna-il-rx-prior-auth`
- `aetna-ma-imaging-prior-auth`
- `aetna-nh-rx-prior-auth`
- `anthem-va-pharmacy-prior-auth`
- `bcbsil-bcchp-uniform-pa`
- `bcbsks-prior-authorization-request`
- `bcbsnd-inpatient-fillable`
- `bcbsnd-outpatient-fillable`
- `bcbstx-rx-standard-pa`
- `molina-tx-standard-pa-form`
- `uhc-tx-commercial-prior-auth`

Unsupported fixture slugs:
- see `fixtures/corpus-summary.json`
- the replacement must not silently upgrade clearly static/non-form PDFs into “supported” unless it is truly correct and tested

## Acceptance bar

This is the minimum bar for “drop-in replacement”:

1. same command names and required flags
2. same JSON top-level shapes and field names
3. same exit code behavior for success vs unsupported vs parse errors
4. all copied current tests are either preserved or ported to the new backend
5. the `11` known-good fixtures still inspect/fill successfully
6. the `13` unsupported fixtures still fail cleanly and intentionally
7. no Syncfusion runtime dependency remains
