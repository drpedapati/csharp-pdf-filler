# pdf-form-filler

`pdf-form-filler` is a Syncfusion-free .NET CLI for inspecting and filling true AcroForm PDFs.

Commands:

```text
inspect --pdf <file> [--json]
schema  --pdf <file>
fill    --pdf <file> --values <file.json> --out <filled.pdf> [--flatten] [--json]
```

Notes:
- XFA is rejected by default.
- `--flatten` is off by default so output stays editable for human review.
- `--license-key` is accepted for backward compatibility but is not required by the new implementation.
