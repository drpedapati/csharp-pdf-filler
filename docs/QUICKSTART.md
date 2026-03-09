# Quickstart

This is the fastest way to see `pdf-form-filler` working on a real public form.

The example below uses:

- PDF: `fixtures/exploratory-downloads/legal/ao399-waiver-of-service.pdf`
- values file: `docs/examples/ao399-values.json`

That form is public, simple, and already part of the repo's exploratory corpus.

## 1. Inspect the PDF

```bash
pdf-form-filler inspect \
  --pdf fixtures/exploratory-downloads/legal/ao399-waiver-of-service.pdf \
  --json
```

Representative output:

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

That tells you the form is supported and fillable.

## 2. Export the schema

```bash
pdf-form-filler schema \
  --pdf fixtures/exploratory-downloads/legal/ao399-waiver-of-service.pdf
```

Why this matters:

- field names come from the schema
- combo and list values should come from `Choices`
- `schema` omits `CurrentValue`, so it is safer to treat as the write contract

## 3. Review the sample values file

The repo includes a working example at [docs/examples/ao399-values.json](examples/ao399-values.json).

```json
{
  "Plaintiff": "Jordan Example",
  "Defendant": "Riley Example",
  "Civil action number": "1:26-cv-00042",
  "Plantiff Attorney": "Alex Rivera",
  "Telephone number": "555-0100",
  "E-mail address": "alex@example.org",
  "Address": "123 Main St, Cincinnati, OH 45202",
  "Waiver": "I waive service of summons.",
  "Date_Today": "03/09/2026"
}
```

## 4. Fill the form

```bash
pdf-form-filler fill \
  --pdf fixtures/exploratory-downloads/legal/ao399-waiver-of-service.pdf \
  --values docs/examples/ao399-values.json \
  --out /tmp/ao399-filled.pdf \
  --json
```

Representative output:

```json
{
  "FormType": "acroform",
  "Flattened": false,
  "AppliedFields": 9,
  "SkippedFields": [],
  "UnusedInputKeys": []
}
```

The important parts:

- `AppliedFields` tells you how many values were written
- `SkippedFields` tells you which fields could not be written
- `UnusedInputKeys` tells you which JSON keys did not match any writable field

## 5. Verify the filled PDF

```bash
pdf-form-filler inspect --pdf /tmp/ao399-filled.pdf --json
```

Representative excerpt:

```json
[
  {
    "Name": "Civil action number",
    "CurrentValue": "1:26-cv-00042"
  },
  {
    "Name": "Defendant",
    "CurrentValue": "Riley Example"
  },
  {
    "Name": "E-mail address",
    "CurrentValue": "alex@example.org"
  },
  {
    "Name": "Plaintiff",
    "CurrentValue": "Jordan Example"
  },
  {
    "Name": "Telephone number",
    "CurrentValue": "555-0100"
  }
]
```

## 6. Flatten only when you are done

Default behavior keeps the output editable. That is usually what you want for AI-assisted form filling, because a human often needs to review and revise it.

Use `--flatten` only for the final locked copy:

```bash
pdf-form-filler fill \
  --pdf fixtures/exploratory-downloads/legal/ao399-waiver-of-service.pdf \
  --values docs/examples/ao399-values.json \
  --out /tmp/ao399-final.pdf \
  --flatten
```

## What unsupported looks like

If the PDF is XFA, the tool rejects it by default:

```bash
pdf-form-filler inspect \
  --pdf fixtures/exploratory-downloads/insurance/irs-w4-2026.pdf \
  --json
```

Representative output:

```json
{
  "FormType": "xfa",
  "IsXfaForm": true,
  "IsSupportedAcroForm": false,
  "CanFillValues": false,
  "SupportedFillableFieldCount": 48,
  "ValidationMessage": "PDF contains an XFA form. Only true AcroForm PDFs are supported."
}
```

That is expected. Production support is for true AcroForms.

## Next Docs

- [CLI.md](CLI.md) for command-by-command behavior
- [COMPATIBILITY_CONTRACT.md](COMPATIBILITY_CONTRACT.md) for the strict replacement surface
- [../fixtures/README.md](../fixtures/README.md) for corpus details
