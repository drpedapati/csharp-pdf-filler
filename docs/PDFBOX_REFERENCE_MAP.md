# PDFBox Reference Map

Official source release copied into:
- `reference-src/pdfbox/pdfbox-3.0.6/`

Use PDFBox as the primary conceptual reference for AcroForm parsing, field modeling, appearance generation, widget handling, and flattening.

## Core AcroForm classes

Primary files to study:

- `reference-src/pdfbox/pdfbox-3.0.6/pdfbox/src/main/java/org/apache/pdfbox/pdmodel/interactive/form/PDAcroForm.java`
- `reference-src/pdfbox/pdfbox-3.0.6/pdfbox/src/main/java/org/apache/pdfbox/pdmodel/interactive/form/PDField.java`
- `reference-src/pdfbox/pdfbox-3.0.6/pdfbox/src/main/java/org/apache/pdfbox/pdmodel/interactive/form/PDFieldTree.java`
- `reference-src/pdfbox/pdfbox-3.0.6/pdfbox/src/main/java/org/apache/pdfbox/pdmodel/interactive/form/PDTerminalField.java`
- `reference-src/pdfbox/pdfbox-3.0.6/pdfbox/src/main/java/org/apache/pdfbox/pdmodel/interactive/form/PDNonTerminalField.java`

## Concrete field types

- `PDTextField.java`
- `PDCheckBox.java`
- `PDRadioButton.java`
- `PDComboBox.java`
- `PDListBox.java`
- `PDButton.java`
- `PDVariableText.java`
- `PDSignatureField.java`

All live under:
- `reference-src/pdfbox/pdfbox-3.0.6/pdfbox/src/main/java/org/apache/pdfbox/pdmodel/interactive/form/`

## Appearance generation

Critical file:
- `reference-src/pdfbox/pdfbox-3.0.6/pdfbox/src/main/java/org/apache/pdfbox/pdmodel/interactive/form/AppearanceGeneratorHelper.java`

This is one of the most important references in the whole tree because it shows how a mature library thinks about:
- widget-level appearance updates
- default appearance strings
- text layout and rotation handling
- bounding boxes
- appearance regeneration pitfalls

## Widget and annotation handling

Study:
- `reference-src/pdfbox/pdfbox-3.0.6/pdfbox/src/main/java/org/apache/pdfbox/pdmodel/interactive/annotation/PDAnnotationWidget.java`

The replacement must treat widgets as first-class objects, not just field-name containers.

## NeedAppearances and flattening

Study in `PDAcroForm.java`:
- `getNeedAppearances()`
- `setNeedAppearances(Boolean value)`
- `flatten()`
- `flatten(List<PDField> fields, boolean refreshAppearances)`
- `refreshAppearances(List<PDField> fields)`

Also inspect flattening tests:
- `reference-src/pdfbox/pdfbox-3.0.6/pdfbox/src/test/java/org/apache/pdfbox/pdmodel/interactive/form/PDAcroFormFlattenTest.java`
- `reference-src/pdfbox/pdfbox-3.0.6/pdfbox/src/test/java/org/apache/pdfbox/pdmodel/interactive/form/PDAcroFormGenerateAppearancesTest.java`

## Example programs

Very useful examples:
- `examples/src/main/java/org/apache/pdfbox/examples/interactive/form/PrintFields.java`
- `examples/src/main/java/org/apache/pdfbox/examples/interactive/form/SetField.java`
- `examples/src/main/java/org/apache/pdfbox/examples/interactive/form/CreateSimpleForm.java`
- `examples/src/main/java/org/apache/pdfbox/examples/interactive/form/CreateCheckBox.java`
- `examples/src/main/java/org/apache/pdfbox/examples/interactive/form/CreateRadioButtons.java`
- `examples/src/main/java/org/apache/pdfbox/examples/interactive/form/CreateMultiWidgetsForm.java`

## What to imitate from PDFBox

Imitate conceptually:
- clear distinction between AcroForm root, field tree, terminal vs non-terminal fields, and widgets
- explicit handling of appearance streams
- conservative flattening behavior
- field-tree traversal instead of assuming a flat field list
- class-per-field-type design

Do not imitate blindly:
- Java-specific type structure when it hurts C# ergonomics
- internal complexity unrelated to the CLI surface we need
- features outside current scope such as signatures unless required for compatibility exposure

## Practical guidance

For the replacement, PDFBox should be treated as:
- architectural template
- source of edge-case ideas
- source of test ideas
- source of naming/behavior patterns for widget and appearance handling

The replacement does not need to mirror PDFBox line-for-line.
It needs to match the current `pdf-form-filler` behavior while being robust on the copied payer corpus.
