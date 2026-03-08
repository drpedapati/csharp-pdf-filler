# Implementation Guide

## Recommended first engineering move

Start by creating a new clean C# project in this folder that preserves the current CLI surface without any backend implementation detail.

Then port the current tests one by one and replace backend assumptions with fixture-backed assertions.

## Suggested internal modules

- `Cli/`
  - parsing
  - help text
  - error handling
- `Pdf/LowLevel/`
  - COS objects, dictionaries, arrays, names, streams
- `Pdf/AcroForm/`
  - form root
  - field tree
  - widgets
  - field types
  - value encoding/decoding
- `Pdf/Appearance/`
  - text appearance generation
  - checkbox/radio appearance state
  - NeedAppearances strategy
- `Pdf/Flatten/`
  - flattening pipeline
- `Fixtures/Tests/`
  - corpus-driven regression tests

## Most important engineering risks

1. appearance streams
2. radio/checkbox export values
3. terminal/non-terminal field traversal
4. widget/page association
5. flattening without visual regressions
6. falsely accepting pseudo-forms as supported

## Strong recommendation

Use the smallest passing form first:
- `bcbsks-prior-authorization-request`

Then move to a checkbox-heavier form:
- `uhc-tx-commercial-prior-auth`

Then move to the full passing corpus.
