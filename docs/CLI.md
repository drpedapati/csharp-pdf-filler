# CLI Guide

This guide is the user-facing command reference for `pdf-form-filler`.

If you want the strict replacement contract, use [COMPATIBILITY_CONTRACT.md](COMPATIBILITY_CONTRACT.md).

## Help

```bash
pdf-form-filler --help
```

Current help shape:

```text
inspect --pdf <file> [--json] [--license-key <key>]
schema  --pdf <file> [--license-key <key>]
fill    --pdf <file> --values <file.json> --out <filled.pdf> [--flatten] [--experimental-xfa] [--json] [--license-key <key>]
```

## `inspect`

Use `inspect` to answer one question first: "Is this PDF a real supported AcroForm?"

```bash
pdf-form-filler inspect --pdf form.pdf --json
```

What it returns:

- overall form type
- whether the file is XFA
- whether the form is supported for fill
- how many supported writable fields were found
- the field list with current values

Typical uses:

- preflight a form before you send it to an LLM
- decide whether to reject an upload
- examine current values in an already-filled PDF

## `schema`

Use `schema` when you want the write contract for a form.

```bash
pdf-form-filler schema --pdf form.pdf
```

What it returns:

- the same top-level support metadata as `inspect`
- field names
- field kinds
- read-only/required flags
- valid choices for combo and list fields

What it does not return:

- `CurrentValue`

That omission is intentional. `schema` is for writing, not round-tripping current state.

## `fill`

Use `fill` to write values from JSON into a supported AcroForm.

```bash
pdf-form-filler fill \
  --pdf form.pdf \
  --values values.json \
  --out filled.pdf \
  --json
```

The JSON response tells you:

- how many fields were applied
- which fields were skipped
- which input keys were unused
- whether the output was flattened

### Values JSON

Typical shape:

```json
{
  "TextField": "hello",
  "CheckboxField": true,
  "RadioField": "Approved",
  "ListField": ["A", "B"]
}
```

Field matching rules:

- keys match by exact field name
- text fields take strings
- checkboxes take booleans
- radio fields take the option value string
- combo and list fields should use exact values from `schema`

Practical advice:

- always inspect or schema the form first
- for combo and list widgets, copy values directly from `Choices`
- if you see unexpected `UnusedInputKeys`, your JSON keys do not match the form

### `--flatten`

`--flatten` is opt-in:

```bash
pdf-form-filler fill \
  --pdf form.pdf \
  --values values.json \
  --out final.pdf \
  --flatten
```

Use it for the final non-editable copy, not the working draft.

### `--experimental-xfa`

This flag exists for comparison and visual-review workflows only.

```bash
pdf-form-filler fill \
  --pdf xfa-form.pdf \
  --values values.json \
  --out trial.pdf \
  --experimental-xfa
```

Treat it as experimental:

- it is not the normal production contract
- it exists to compare behavior on real XFA forms
- do not assume viewer-stable output the way you would for normal AcroForms

## Exit Behavior

The short version:

- `--help`: exit `0`
- parse errors: non-zero
- supported `inspect --json`: exit `0`
- unsupported `inspect --json`: exit `1`
- successful `fill`: exit `0`
- failed `fill`: non-zero

## Common Failure Cases

### Not a form

Validation message:

```text
PDF does not contain a form.
```

### XFA instead of AcroForm

Validation message:

```text
PDF contains an XFA form. Only true AcroForm PDFs are supported.
```

### No usable writable fields

Validation message is one of:

```text
Form exists but contains no fields.
```

or

```text
PDF contains an AcroForm, but no supported fillable fields were found.
```

## Backward Compatibility Notes

- `--license-key` is still accepted, but it does nothing
- the product target is the existing payer-form CLI workflow
- XFA stays rejected by default in the production path
