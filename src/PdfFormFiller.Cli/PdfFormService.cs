using System.Text.Json;
using System.Reflection;
using PdfSharp.Pdf;
using PdfSharp.Pdf.AcroForms;
using PdfSharp.Pdf.Advanced;
using PdfSharp.Pdf.IO;
using PdfSharp.Pdf.Security;

namespace PdfFormFiller.Cli;

public sealed class PdfFormService : IPdfFormService
{
    private const string PromotedImportedDocumentTag = "promoted-import-document";
    private const BindingFlags InstanceFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    public FormInspection Inspect(string pdfPath)
    {
        FontResolverBootstrapper.EnsureConfigured();

        string fullPath = NormalizePath(pdfPath);
        EnsureFileExists(fullPath);

        using PdfDocument document = PdfReader.Open(fullPath, PdfDocumentOpenMode.Import);
        PdfAcroForm? acroForm = TryGetAcroForm(document);
        if (acroForm is null)
        {
            return new FormInspection(
                fullPath,
                "none",
                false,
                false,
                false,
                0,
                "PDF does not contain a form.",
                0,
                []
            );
        }

        bool isXfa = IsXfaForm(acroForm);
        List<FormFieldInfo> fields = LoadFields(acroForm);
        if (fields.Count == 0)
        {
            return new FormInspection(
                fullPath,
                isXfa ? "xfa" : "acroform",
                isXfa,
                false,
                false,
                0,
                isXfa
                    ? "PDF contains an XFA form. Only true AcroForm PDFs are supported."
                    : "Form exists but contains no fields.",
                0,
                []
            );
        }

        int supportedFillableFieldCount = fields.Count(IsSupportedFillableField);
        bool canFillValues = !isXfa && supportedFillableFieldCount > 0;

        return new FormInspection(
            fullPath,
            isXfa ? "xfa" : "acroform",
            isXfa,
            canFillValues,
            canFillValues,
            supportedFillableFieldCount,
            BuildValidationMessage(isXfa, supportedFillableFieldCount),
            fields.Count,
            fields
        );
    }

    public FormSchema Schema(string pdfPath)
    {
        FormInspection inspection = Inspect(pdfPath);
        return new FormSchema(
            inspection.PdfPath,
            inspection.FormType,
            inspection.IsXfaForm,
            inspection.IsSupportedAcroForm,
            inspection.CanFillValues,
            inspection.SupportedFillableFieldCount,
            inspection.ValidationMessage,
            inspection.FieldCount,
            inspection.Fields.Select(field => new FormSchemaFieldInfo(
                field.Name,
                field.Kind,
                field.ToolTip,
                field.ReadOnly,
                field.Required,
                field.Choices
            )).ToArray()
        );
    }

    public FillResult Fill(string pdfPath, string valuesPath, string outputPath, bool flatten, bool experimentalXfa = false)
    {
        FontResolverBootstrapper.EnsureConfigured();

        string fullPdfPath = NormalizePath(pdfPath);
        string fullValuesPath = NormalizePath(valuesPath);
        string fullOutputPath = NormalizePath(outputPath);

        EnsureFileExists(fullPdfPath);
        EnsureFileExists(fullValuesPath);

        PdfDocument? document = null;
        try
        {
            document = OpenForModify(fullPdfPath);
            PdfAcroForm? acroForm = TryGetAcroForm(document);
            if (acroForm is null)
            {
                throw new InvalidOperationException("PDF does not contain a form.");
            }

            bool isXfa = IsXfaForm(acroForm);
            if (isXfa && !experimentalXfa)
            {
                throw new InvalidOperationException("XFA forms are not supported for fill. Provide a true AcroForm PDF.");
            }

            Dictionary<string, JsonElement> values = LoadValues(fullValuesPath);
            HashSet<string> unusedKeys = new(values.Keys, StringComparer.OrdinalIgnoreCase);
            List<string> skippedFields = [];
            int appliedFields = 0;
            int supportedFillableFieldCount = 0;

            foreach ((string fieldName, PdfAcroField field) in LoadFieldTargets(acroForm))
            {
                string kind = DetectKind(field);
                if (IsSupportedFillableKind(kind))
                {
                    supportedFillableFieldCount++;
                }

                if (!values.TryGetValue(fieldName, out JsonElement value))
                {
                    continue;
                }

                unusedKeys.Remove(fieldName);

                if (!TryApplyValue(field, value))
                {
                    skippedFields.Add(fieldName);
                    continue;
                }

                appliedFields++;
            }

            if (supportedFillableFieldCount == 0)
            {
                throw new InvalidOperationException("PDF contains an AcroForm, but no supported fillable fields were found.");
            }

            if (appliedFields == 0)
            {
                string unusedSummary = unusedKeys.Count > 0
                    ? $" Unused input keys: {string.Join(", ", unusedKeys.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).Take(10))}."
                    : string.Empty;
                throw new InvalidOperationException($"No JSON keys matched fillable form fields.{unusedSummary}");
            }

            acroForm.Elements.SetBoolean("/NeedAppearances", true);
            if (flatten)
            {
                document.Flatten();
                RemoveInteractiveForm(document);
            }

            string? outputDirectory = Path.GetDirectoryName(fullOutputPath);
            if (!string.IsNullOrWhiteSpace(outputDirectory))
            {
                Directory.CreateDirectory(outputDirectory);
            }

            bool preserveOriginalEncryption = HasOriginalEncryption(document);
            PrepareDocumentForSave(document, preserveOriginalEncryption);

            if (preserveOriginalEncryption)
            {
                RestoreOriginalEncryptionForWrite(document);
                SavePreservingOriginalEncryption(document, fullOutputPath);
            }
            else
            {
                document.Save(fullOutputPath);
            }

            return new FillResult(
                fullPdfPath,
                fullOutputPath,
                isXfa ? "xfa" : "acroform",
                flatten,
                appliedFields,
                skippedFields,
                unusedKeys.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray()
            );
        }
        finally
        {
            document?.Dispose();
        }
    }

    private static PdfAcroForm? TryGetAcroForm(PdfDocument document)
    {
        if (!document.Internals.Catalog.Elements.ContainsKey("/AcroForm"))
        {
            return null;
        }

        return document.AcroForm;
    }

    private static List<FormFieldInfo> LoadFields(PdfAcroForm form)
    {
        List<FormFieldInfo> fields = [];
        CollectFields(form.Fields, null, fields);
        return fields
            .Where(field => !string.IsNullOrWhiteSpace(field.Name))
            .OrderBy(field => field.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static IReadOnlyList<(string FieldName, PdfAcroField Field)> LoadFieldTargets(PdfAcroForm form)
    {
        List<(string FieldName, PdfAcroField Field)> targets = [];
        CollectFieldTargets(form.Fields, null, targets);
        return targets;
    }

    private static void CollectFields(
        PdfAcroField.PdfAcroFieldCollection fieldCollection,
        string? parentPath,
        List<FormFieldInfo> fields
    )
    {
        for (int i = 0; i < fieldCollection.Count; i++)
        {
            PdfAcroField field = fieldCollection[i];
            string fullName = BuildFullFieldName(field, parentPath);
            if (ShouldExposeField(field))
            {
                fields.Add(CreateFormFieldInfo(field, fullName));
                continue;
            }

            if (field.HasKids)
            {
                CollectFields(field.Fields, fullName, fields);
            }
        }
    }

    private static void CollectFieldTargets(
        PdfAcroField.PdfAcroFieldCollection fieldCollection,
        string? parentPath,
        List<(string FieldName, PdfAcroField Field)> targets
    )
    {
        for (int i = 0; i < fieldCollection.Count; i++)
        {
            PdfAcroField field = fieldCollection[i];
            string fullName = BuildFullFieldName(field, parentPath);
            if (ShouldExposeField(field))
            {
                targets.Add((fullName, field));
                continue;
            }

            if (field.HasKids)
            {
                CollectFieldTargets(field.Fields, fullName, targets);
            }
        }
    }

    private static bool ShouldExposeField(PdfAcroField field)
    {
        if (field is not PdfGenericField)
        {
            return true;
        }

        string fieldType = TryGetName(field.Elements, "/FT");
        return !field.HasKids || !string.IsNullOrWhiteSpace(fieldType);
    }

    private static FormFieldInfo CreateFormFieldInfo(PdfAcroField field, string fullName)
    {
        string kind = DetectKind(field);
        return new FormFieldInfo(
            fullName,
            kind,
            GetToolTip(field),
            field.ReadOnly,
            field.Flags.HasFlag(PdfAcroFieldFlags.Required),
            GetCurrentValue(field, kind),
            GetChoices(field)
        );
    }

    private static string BuildFullFieldName(PdfAcroField field, string? parentPath)
    {
        string partialName = TryGetString(field.Elements, "/T");
        if (!string.IsNullOrWhiteSpace(partialName))
        {
            return string.IsNullOrWhiteSpace(parentPath) ? partialName : $"{parentPath}.{partialName}";
        }

        string fieldName = field.Name;
        if (!string.IsNullOrWhiteSpace(fieldName))
        {
            return string.IsNullOrWhiteSpace(parentPath) || fieldName.Contains('.')
                ? fieldName
                : $"{parentPath}.{fieldName}";
        }

        return parentPath ?? string.Empty;
    }

    private static string DetectKind(PdfAcroField field) => field switch
    {
        PdfTextField => "text",
        PdfCheckBoxField => "checkbox",
        PdfComboBoxField => "combo",
        PdfListBoxField => "list",
        PdfRadioButtonField => "radio",
        PdfSignatureField => "signature",
        PdfPushButtonField => "PdfLoadedButtonField",
        _ => field.GetType().Name,
    };

    private static bool IsSupportedFillableKind(string kind) =>
        kind is "text" or "checkbox" or "combo" or "list" or "radio";

    private static object? GetCurrentValue(PdfAcroField field, string kind) => kind switch
    {
        "text" => ((PdfTextField)field).Text,
        "checkbox" => ((PdfCheckBoxField)field).Checked,
        "combo" => GetChoiceCurrentValue(field),
        "list" => GetFieldValue(field),
        "radio" => GetFieldValue(field),
        _ => null,
    };

    private static IReadOnlyList<string> GetChoices(PdfAcroField field)
    {
        if (field is PdfChoiceField)
        {
            return ReadOptionsForDisplay(TryGetArray(field.Elements, "/Opt"));
        }

        if (field is PdfRadioButtonField radio)
        {
            IReadOnlyList<string> options = ReadWidgetAppearanceNames(field);
            if (options.Count > 0)
            {
                return options;
            }

            options = ReadOptionsForDisplay(TryGetArray(field.Elements, "/Opt"));
            if (options.Count > 0)
            {
                return options;
            }

            return radio.GetAppearanceNames()
                .Select(NormalizeNameValue)
                .Where(value => !string.IsNullOrWhiteSpace(value) && !string.Equals(value, "Off", StringComparison.OrdinalIgnoreCase))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        return [];
    }

    private static IReadOnlyList<string> ReadOptions(PdfArray? optionsArray)
    {
        return ReadChoiceOptions(optionsArray)
            .Select(option => GetChoiceOptionValue(option, preserveWhitespace: false))
            .Where(option => !string.IsNullOrWhiteSpace(option))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static IReadOnlyList<string> ReadOptionsForDisplay(PdfArray? optionsArray)
    {
        return ReadChoiceOptions(optionsArray)
            .Select(option => GetChoiceOptionValue(option, preserveWhitespace: true))
            .Where(option => !string.IsNullOrWhiteSpace(option))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static object? GetChoiceCurrentValue(PdfAcroField field)
    {
        object? rawValue = TryGetItem(field.Elements, "/V");
        string? resolvedValue = TryResolvePdfItemString(rawValue, preserveWhitespace: true);
        if (resolvedValue is not null)
        {
            if (resolvedValue.Length == 0)
            {
                return null;
            }

            if (resolvedValue.StartsWith("/", StringComparison.Ordinal))
            {
                return NormalizeNameValue(resolvedValue);
            }

            string normalizedResolvedValue = NormalizeScalarValue(resolvedValue).Trim();
            if (normalizedResolvedValue.Length == 0)
            {
                return resolvedValue;
            }

            IReadOnlyList<ChoiceOption> choices = ReadChoiceOptions(TryGetArray(field.Elements, "/Opt"));
            bool exactChoiceMatch = choices.Any(choice => ChoiceValueMatches(choice, resolvedValue, normalizeCandidate: false));
            if (exactChoiceMatch)
            {
                return resolvedValue;
            }

            bool normalizedChoiceMatch = choices.Any(choice => ChoiceValueMatches(choice, normalizedResolvedValue, normalizeCandidate: true));
            if (normalizedChoiceMatch && char.IsWhiteSpace(resolvedValue[0]))
            {
                return normalizedResolvedValue;
            }

            return resolvedValue;
        }

        string fallbackValue = NormalizeScalarValue(((PdfComboBoxField)field).Value?.ToString());
        return string.IsNullOrWhiteSpace(fallbackValue) ? null : fallbackValue;
    }

    private static IReadOnlyList<string> ReadWidgetAppearanceNames(PdfAcroField field)
    {
        PdfArray? kids = TryGetArray(field.Elements, "/Kids");
        if (kids is null)
        {
            return ReadAppearanceStateNames(field);
        }

        List<string> names = [];
        for (int i = 0; i < kids.Elements.Count; i++)
        {
            PdfDictionary? widget = SafeGetDictionary(kids, i);
            PdfDictionary? appearance = widget?.Elements.GetDictionary("/AP");
            PdfDictionary? normalAppearance = appearance?.Elements.GetDictionary("/N");
            if (normalAppearance is null)
            {
                continue;
            }

            names.AddRange(ReadAppearanceStateNames(normalAppearance));
        }

        return names.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static IReadOnlyList<string> ReadAppearanceStateNames(PdfDictionary dictionary)
    {
        PdfDictionary? normalAppearance = dictionary;
        if (dictionary.Elements.ContainsKey("/AP"))
        {
            PdfDictionary? appearance = dictionary.Elements.GetDictionary("/AP");
            normalAppearance = appearance?.Elements.GetDictionary("/N");
        }

        if (normalAppearance is null)
        {
            return [];
        }

        return normalAppearance.Elements
            .Select(entry => NormalizeNameValue(entry.Key))
            .Where(value => !string.IsNullOrWhiteSpace(value) && !string.Equals(value, "Off", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static object? GetFieldValue(PdfAcroField field)
    {
        PdfArray? valueArray = TryGetArray(field.Elements, "/V");
        if (valueArray is not null)
        {
            List<string> values = [];
            for (int i = 0; i < valueArray.Elements.Count; i++)
            {
                string value = GetArrayStringOrName(valueArray, i);
                if (string.IsNullOrWhiteSpace(value))
                {
                    object? item = valueArray.Elements[i];
                    value = NormalizeScalarValue(item?.ToString());
                }

                if (!string.IsNullOrWhiteSpace(value))
                {
                    values.Add(value);
                }
            }

            return values.Count switch
            {
                0 => null,
                1 => values[0],
                _ => values.ToArray(),
            };
        }

        string nameValue = TryGetName(field.Elements, "/V");
        if (!string.IsNullOrWhiteSpace(nameValue))
        {
            return NormalizeNameValue(nameValue);
        }

        string stringValue = TryGetString(field.Elements, "/V");
        if (!string.IsNullOrWhiteSpace(stringValue))
        {
            string normalizedStringValue = NormalizeScalarValue(stringValue);
            return normalizedStringValue.StartsWith("/", StringComparison.Ordinal)
                ? NormalizeNameValue(normalizedStringValue)
                : normalizedStringValue;
        }

        string fallbackValue = NormalizeScalarValue(field.Value?.ToString());
        if (string.IsNullOrWhiteSpace(fallbackValue))
        {
            return null;
        }

        return fallbackValue.StartsWith("/", StringComparison.Ordinal)
            ? NormalizeNameValue(fallbackValue)
            : fallbackValue;
    }

    private static string GetToolTip(PdfAcroField field) => TryGetString(field.Elements, "/TU");

    private static bool IsSupportedFillableField(FormFieldInfo field) =>
        field.Kind is "text" or "checkbox" or "combo" or "list" or "radio";

    private static string BuildValidationMessage(bool isXfa, int supportedFillableFieldCount)
    {
        if (isXfa)
        {
            return "PDF contains an XFA form. Only true AcroForm PDFs are supported.";
        }

        if (supportedFillableFieldCount == 0)
        {
            return "PDF contains an AcroForm, but no supported fillable fields were found.";
        }

        return "PDF contains a supported AcroForm.";
    }

    private static bool IsXfaForm(PdfAcroForm form) =>
        form.Elements.ContainsKey("/XFA");

    private static string NormalizePath(string path) => Path.GetFullPath(path);

    private static void EnsureFileExists(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"PDF file not found: {path}");
        }
    }

    private static Dictionary<string, JsonElement> LoadValues(string valuesPath)
    {
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(valuesPath));
        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException("Values JSON must be an object keyed by field name.");
        }

        Dictionary<string, JsonElement> values = new(StringComparer.OrdinalIgnoreCase);
        foreach (JsonProperty property in document.RootElement.EnumerateObject())
        {
            values[property.Name] = property.Value.Clone();
        }

        return values;
    }

    private static bool TryApplyValue(PdfAcroField field, JsonElement value) => field switch
    {
        PdfTextField textField => TrySetTextValue(textField, value),
        PdfCheckBoxField checkBoxField => TrySetCheckboxValue(checkBoxField, value),
        PdfComboBoxField comboBoxField => TrySetComboValue(comboBoxField, value),
        PdfListBoxField listBoxField => TrySetListValue(listBoxField, value),
        PdfRadioButtonField radioButtonField => TrySetRadioValue(radioButtonField, value),
        _ => false,
    };

    private static bool TrySetTextValue(PdfTextField field, JsonElement value)
    {
        field.Elements.SetString("/V", CoerceToString(value));
        return true;
    }

    private static bool TrySetCheckboxValue(PdfCheckBoxField field, JsonElement value)
    {
        if (!TryCoerceToBool(value, out bool parsed))
        {
            return false;
        }

        IReadOnlyList<CheckboxStateTarget> targets = ReadCheckboxStateTargets(field);
        if (targets.Count == 0)
        {
            field.Checked = parsed;
            return true;
        }

        string fieldStateName = targets[0].StateName;
        field.Elements.SetName("/V", parsed ? fieldStateName : "Off");
        field.Elements.SetName("/AS", parsed ? fieldStateName : "Off");

        foreach (CheckboxStateTarget target in targets)
        {
            if (target.Widget is not null)
            {
                target.Widget.Elements.SetName("/AS", parsed ? target.StateName : "Off");
            }
        }

        return true;
    }

    private static bool TrySetComboValue(PdfComboBoxField field, JsonElement value)
    {
        string requestedValue = CoerceToString(value);
        if (string.IsNullOrWhiteSpace(requestedValue))
        {
            return false;
        }

        IReadOnlyList<ChoiceOption> choices = ReadChoiceOptions(TryGetArray(field.Elements, "/Opt"));
        ChoiceOption? matchedOption = FindChoiceOption(choices, requestedValue);
        if (matchedOption is not null)
        {
            field.SelectedIndex = matchedOption.RawIndex;
            // XFA shadow combos can render correctly but retain a placeholder /V unless we
            // also persist the matched export value explicitly.
            field.Elements.SetString("/V", matchedOption.StoredValue);
            return true;
        }

        if (field.Flags.HasFlag(PdfAcroFieldFlags.Edit))
        {
            field.Elements.SetString("/V", requestedValue);
            return true;
        }

        return false;
    }

    private static bool TrySetListValue(PdfListBoxField field, JsonElement value)
    {
        string requestedValue = CoerceToStringArray(value).FirstOrDefault() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(requestedValue))
        {
            return false;
        }

        IReadOnlyList<ChoiceOption> choices = ReadChoiceOptions(TryGetArray(field.Elements, "/Opt"));
        ChoiceOption? matchedOption = FindChoiceOption(choices, requestedValue);
        if (matchedOption is null)
        {
            return false;
        }

        field.SelectedIndex = matchedOption.RawIndex;
        field.Elements.SetString("/V", matchedOption.StoredValue);
        return true;
    }

    private static bool TrySetRadioValue(PdfRadioButtonField field, JsonElement value)
    {
        string requestedValue = CoerceToString(value);
        if (string.IsNullOrWhiteSpace(requestedValue))
        {
            return false;
        }

        IReadOnlyList<RadioStateTarget> targets = ReadRadioStateTargets(field);
        RadioStateTarget? selectedTarget = targets.FirstOrDefault(
            target => StringComparer.OrdinalIgnoreCase.Equals(target.StateName, requestedValue)
        );
        if (selectedTarget is null)
        {
            return false;
        }

        field.Elements.SetName("/V", selectedTarget.StateName);
        if (selectedTarget.Widget is null)
        {
            field.Elements.SetName("/AS", selectedTarget.StateName);
            return true;
        }

        foreach (RadioStateTarget target in targets)
        {
            target.Widget!.Elements.SetName(
                "/AS",
                ReferenceEquals(target.Widget, selectedTarget.Widget) ? target.StateName : "Off"
            );
        }

        return true;
    }

    private static ChoiceOption? FindChoiceOption(IReadOnlyList<ChoiceOption> choices, string requestedValue)
    {
        for (int i = 0; i < choices.Count; i++)
        {
            if (ChoiceValueMatches(choices[i], requestedValue, normalizeCandidate: false))
            {
                return choices[i];
            }
        }

        string normalizedRequestedValue = NormalizeScalarValue(requestedValue);
        for (int i = 0; i < choices.Count; i++)
        {
            if (ChoiceValueMatches(choices[i], normalizedRequestedValue, normalizeCandidate: true))
            {
                return choices[i];
            }
        }

        return null;
    }

    private static IReadOnlyList<ChoiceOption> ReadChoiceOptions(PdfArray? optionsArray)
    {
        if (optionsArray is null)
        {
            return [];
        }

        List<ChoiceOption> options = [];
        for (int i = 0; i < optionsArray.Elements.Count; i++)
        {
            PdfArray? optionPair = SafeGetArray(optionsArray, i);
            if (optionPair is not null)
            {
                string exportValue = optionPair.Elements.Count > 0
                    ? GetArrayStringOrName(optionPair, 0, preserveStringWhitespace: true)
                    : string.Empty;
                string displayValue = optionPair.Elements.Count > 1
                    ? GetArrayStringOrName(optionPair, 1, preserveStringWhitespace: true)
                    : string.Empty;
                string storedValue = !string.IsNullOrEmpty(exportValue) ? exportValue : displayValue;
                options.Add(new ChoiceOption(i, exportValue, displayValue, storedValue));
                continue;
            }

            string option = GetArrayStringOrName(optionsArray, i, preserveStringWhitespace: true);
            options.Add(new ChoiceOption(i, option, string.Empty, option));
        }

        return options;
    }

    private static bool ChoiceValueMatches(ChoiceOption option, string candidate, bool normalizeCandidate)
    {
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return false;
        }

        string compareValue = normalizeCandidate ? NormalizeScalarValue(candidate) : candidate;
        return ChoiceValueEquals(option.ExportValue, compareValue, normalizeCandidate)
            || ChoiceValueEquals(option.DisplayValue, compareValue, normalizeCandidate)
            || ChoiceValueEquals(GetChoiceOptionValue(option, preserveWhitespace: !normalizeCandidate), compareValue, normalizeCandidate);
    }

    private static bool ChoiceValueEquals(string value, string candidate, bool normalizeValue)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        string compareValue = normalizeValue ? NormalizeScalarValue(value) : value;
        return string.Equals(compareValue, candidate, StringComparison.OrdinalIgnoreCase);
    }

    private static string GetChoiceOptionValue(ChoiceOption option, bool preserveWhitespace)
    {
        string value = !string.IsNullOrEmpty(option.ExportValue) ? option.ExportValue : option.DisplayValue;
        return preserveWhitespace ? value : NormalizeScalarValue(value);
    }

    private static IReadOnlyList<RadioStateTarget> ReadRadioStateTargets(PdfRadioButtonField field)
    {
        List<RadioStateTarget> targets = [];
        PdfArray? kids = TryGetArray(field.Elements, "/Kids");
        if (kids is not null)
        {
            for (int i = 0; i < kids.Elements.Count; i++)
            {
                PdfDictionary? widget = SafeGetDictionary(kids, i);
                if (widget is null)
                {
                    continue;
                }

                foreach (string stateName in ReadAppearanceStateNames(widget))
                {
                    targets.Add(new RadioStateTarget(stateName, widget));
                }
            }

            if (targets.Count > 0)
            {
                return targets;
            }
        }

        foreach (string stateName in ReadAppearanceStateNames(field))
        {
            targets.Add(new RadioStateTarget(stateName, null));
        }

        return targets;
    }

    private static IReadOnlyList<CheckboxStateTarget> ReadCheckboxStateTargets(PdfCheckBoxField field)
    {
        List<CheckboxStateTarget> targets = [];
        PdfArray? kids = TryGetArray(field.Elements, "/Kids");
        if (kids is not null)
        {
            for (int i = 0; i < kids.Elements.Count; i++)
            {
                PdfDictionary? widget = SafeGetDictionary(kids, i);
                if (widget is null)
                {
                    continue;
                }

                foreach (string stateName in ReadAppearanceStateNames(widget))
                {
                    targets.Add(new CheckboxStateTarget(stateName, widget));
                }
            }

            if (targets.Count > 0)
            {
                return targets;
            }
        }

        foreach (string stateName in ReadAppearanceStateNames(field))
        {
            targets.Add(new CheckboxStateTarget(stateName, null));
        }

        return targets;
    }

    private static string CoerceToString(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.String => element.GetString() ?? string.Empty,
        JsonValueKind.Number => element.ToString(),
        JsonValueKind.True => "true",
        JsonValueKind.False => "false",
        JsonValueKind.Null => string.Empty,
        _ => element.ToString(),
    };

    private static string[] CoerceToStringArray(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.Array => element.EnumerateArray().Select(CoerceToString).ToArray(),
        _ => [CoerceToString(element)],
    };

    private static bool TryCoerceToBool(JsonElement element, out bool parsed)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.True:
                parsed = true;
                return true;
            case JsonValueKind.False:
                parsed = false;
                return true;
            case JsonValueKind.String:
                return TryParseBoolString(element.GetString(), out parsed);
            case JsonValueKind.Number:
                if (element.TryGetInt32(out int number) && (number == 0 || number == 1))
                {
                    parsed = number == 1;
                    return true;
                }
                break;
        }

        parsed = false;
        return false;
    }

    private static bool TryParseBoolString(string? value, out bool parsed)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            parsed = false;
            return false;
        }

        switch (value.Trim().ToLowerInvariant())
        {
            case "1":
            case "true":
            case "yes":
            case "on":
            case "checked":
                parsed = true;
                return true;
            case "0":
            case "false":
            case "no":
            case "off":
            case "unchecked":
                parsed = false;
                return true;
            default:
                parsed = false;
                return false;
        }
    }

    private static string NormalizeNameValue(string value)
    {
        string trimmed = value.Trim();
        if (trimmed.StartsWith('/'))
        {
            trimmed = trimmed[1..];
        }

        return NormalizeScalarValue(trimmed);
    }

    private static string NormalizeScalarValue(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        string trimmed = value.Trim();
        if (trimmed.Length >= 2 && trimmed[0] == '(' && trimmed[^1] == ')')
        {
            trimmed = trimmed[1..^1];
        }

        return trimmed;
    }

    private static PdfDocument OpenForModify(string pdfPath)
    {
        try
        {
            return PdfReader.Open(pdfPath, PdfDocumentOpenMode.Modify);
        }
        catch (PdfReaderException ex) when (ex.Message.Contains("owner password", StringComparison.OrdinalIgnoreCase))
        {
            PdfDocument document = PdfReader.Open(pdfPath, PdfDocumentOpenMode.Import);
            PromoteImportedDocumentForModification(document);
            return document;
        }
    }

    private static void PromoteImportedDocumentForModification(PdfDocument document)
    {
        typeof(PdfDocument)
            .GetField("_openMode", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .SetValue(document, PdfDocumentOpenMode.Modify);

        document.SecuritySettings.HasOwnerPermissions = true;
        document.Tag = PromotedImportedDocumentTag;

        // Materialize the page tree so save/flatten paths see the imported pages.
        _ = document.Pages.Count;
    }

    private static void PrepareDocumentForSave(PdfDocument document, bool preserveOriginalEncryption)
    {
        if (!string.Equals(document.Tag as string, PromotedImportedDocumentTag, StringComparison.Ordinal))
        {
            return;
        }

        PdfInternals internals = document.Internals;
        if (string.IsNullOrEmpty(internals.FirstDocumentID))
        {
            InvokeInstance(GetTrailer(document), "CreateNewDocumentIDs");
        }

        if (preserveOriginalEncryption)
        {
            internals.SecondDocumentID = internals.FirstDocumentID;
        }
        else if (string.IsNullOrEmpty(internals.SecondDocumentID))
        {
            InvokeInstance(GetTrailer(document), "CreateNewDocumentIDs");
        }
        else
        {
            internals.SecondDocumentID = Guid.NewGuid().ToString("N");
        }

        document.Info.ModificationDate = DateTime.Now;

        object irefTable = GetIrefTable(document);
        InvokeInstance(irefTable, "Compact");

        _ = document.Pages;

        InvokeInstance(irefTable, "Renumber");
    }

    private static bool HasOriginalEncryption(PdfDocument document)
    {
        return GetElements(GetTrailer(document)).ContainsKey("/Encrypt");
    }

    private static void RestoreOriginalEncryptionForWrite(PdfDocument document)
    {
        PdfStandardSecurityHandler securityHandler = document.SecurityHandler;
        InvokeInstance(securityHandler, "PrepareForReading");

        PasswordValidity validity = (PasswordValidity)InvokeInstance(securityHandler, "ValidatePassword", string.Empty)!;
        if (validity == PasswordValidity.Invalid)
        {
            throw new InvalidOperationException("Unable to restore the original encryption state for save.");
        }

        document.SecuritySettings.HasOwnerPermissions = true;
    }

    private static void SavePreservingOriginalEncryption(PdfDocument document, string outputPath)
    {
        using FileStream stream = File.Create(outputPath);
        PdfStandardSecurityHandler securityHandler = document.SecurityHandler;
        object writer = CreatePdfWriter(stream, document, securityHandler);

        try
        {
            WriteDocumentPreservingOriginalEncryption(document, securityHandler, writer);
        }
        finally
        {
            InvokeInstance(writer, "Close", false);
        }
    }

    private static object CreatePdfWriter(Stream stream, PdfDocument document, PdfStandardSecurityHandler securityHandler)
    {
        Type writerType = typeof(PdfReader).Assembly.GetType("PdfSharp.Pdf.IO.PdfWriter")
            ?? throw new InvalidOperationException("Unable to locate PDFsharp writer type.");
        ConstructorInfo ctor = writerType.GetConstructor(
            InstanceFlags,
            binder: null,
            [typeof(Stream), typeof(PdfDocument), typeof(PdfStandardSecurityHandler)],
            modifiers: null)
            ?? throw new InvalidOperationException("Unable to locate PDFsharp writer constructor.");

        return ctor.Invoke([stream, document, securityHandler]);
    }

    private static void WriteDocumentPreservingOriginalEncryption(
        PdfDocument document,
        PdfStandardSecurityHandler securityHandler,
        object writer)
    {
        ReplaceCrossReferenceStreamTrailer(document);

        object trailer = GetTrailer(document);
        object irefTable = GetIrefTable(document);
        if (securityHandler.Reference is null)
        {
            InvokeInstance(irefTable, "Add", securityHandler);
        }

        GetElements(trailer)["/Encrypt"] = securityHandler.Reference;
        InvokeInstance(document, "PrepareForSave");
        PreserveMetadataEncryptionBehavior(document, securityHandler);

        InvokeInstance(writer, "WriteFileHeader", document);

        Array references = (Array)GetProperty(irefTable, "AllReferences")!;
        foreach (object reference in references)
        {
            SetProperty(reference, "Position", GetProperty(writer, "Position"));

            object pdfObject = GetProperty(reference, "Value")
                ?? throw new InvalidOperationException("Encountered a PDF reference without an object.");

            InvokeInstance(securityHandler, "EnterObject", GetProperty(pdfObject, "ObjectID"));
            InvokeInstance(pdfObject, "WriteObject", writer);
        }

        InvokeInstance(securityHandler, "LeaveObject");

        object startXRef = GetProperty(writer, "Position")
            ?? throw new InvalidOperationException("Unable to read PDF writer position.");

        InvokeInstance(irefTable, "WriteObject", writer);
        InvokeInstance(writer, "WriteRaw", "trailer\n");
        GetElements(trailer).SetInteger("/Size", references.Length + 1);
        InvokeInstance(trailer, "WriteObject", writer);
        InvokeInstance(writer, "WriteEof", document, startXRef);
    }

    private static void PreserveMetadataEncryptionBehavior(PdfDocument document, PdfStandardSecurityHandler securityHandler)
    {
        bool encryptMetadata = (securityHandler.Elements["/EncryptMetadata"] as PdfBoolean)?.Value ?? true;
        if (encryptMetadata)
        {
            return;
        }

        PdfDictionary? metadata = document.Internals.Catalog.Elements.GetDictionary("/Metadata");
        if (metadata is null)
        {
            return;
        }

        securityHandler.SetIdentityCryptFilter(metadata);
    }

    private static void RemoveInteractiveForm(PdfDocument document)
    {
        PdfCatalog catalog = document.Internals.Catalog;
        catalog.Elements.Remove("/AcroForm");

        FieldInfo? acroFormField = typeof(PdfCatalog).GetField("_acroForm", InstanceFlags);
        acroFormField?.SetValue(catalog, null);
    }

    private static void ReplaceCrossReferenceStreamTrailer(PdfDocument document)
    {
        object trailer = GetTrailer(document);
        if (!string.Equals(trailer.GetType().Name, "PdfCrossReferenceStream", StringComparison.Ordinal))
        {
            return;
        }

        Type trailerType = typeof(PdfReader).Assembly.GetType("PdfSharp.Pdf.Advanced.PdfTrailer")
            ?? throw new InvalidOperationException("Unable to locate PDFsharp trailer type.");
        object replacementTrailer = Activator.CreateInstance(trailerType, [trailer])
            ?? throw new InvalidOperationException("Unable to convert cross-reference stream trailer.");
        SetProperty(document, "Trailer", replacementTrailer);
    }

    private static object GetTrailer(PdfDocument document) =>
        typeof(PdfDocument)
            .GetProperty("Trailer", InstanceFlags)!
            .GetValue(document)!;

    private static PdfDictionary.DictionaryElements GetElements(object pdfObject) =>
        (PdfDictionary.DictionaryElements)pdfObject.GetType()
            .GetProperty("Elements", InstanceFlags)!
            .GetValue(pdfObject)!;

    private static object GetIrefTable(PdfDocument document) =>
        typeof(PdfDocument)
            .GetProperty("IrefTable", InstanceFlags)!
            .GetValue(document)!;

    private static object? GetProperty(object target, string propertyName) =>
        target.GetType()
            .GetProperty(propertyName, InstanceFlags)
            ?.GetValue(target);

    private static void SetProperty(object target, string propertyName, object? value) =>
        target.GetType()
            .GetProperty(propertyName, InstanceFlags)!
            .SetValue(target, value);

    private static object? InvokeInstance(object target, string methodName, params object?[] args)
    {
        MethodInfo method = target.GetType()
            .GetMethods(InstanceFlags)
            .Single(candidate => candidate.Name == methodName && ParametersMatch(candidate.GetParameters(), args));

        return method.Invoke(target, args);
    }

    private static bool ParametersMatch(ParameterInfo[] parameters, object?[] args)
    {
        if (parameters.Length != args.Length)
        {
            return false;
        }

        for (int i = 0; i < parameters.Length; i++)
        {
            object? argument = args[i];
            if (argument is null)
            {
                if (parameters[i].ParameterType.IsValueType && Nullable.GetUnderlyingType(parameters[i].ParameterType) is null)
                {
                    return false;
                }

                continue;
            }

            if (!parameters[i].ParameterType.IsInstanceOfType(argument))
            {
                return false;
            }
        }

        return true;
    }

    private static PdfDictionary? TryGetDictionary(PdfDictionary.DictionaryElements elements, string key)
    {
        try
        {
            return elements.GetDictionary(key);
        }
        catch (InvalidCastException)
        {
            return null;
        }
    }

    private static PdfItem? TryGetItem(PdfDictionary.DictionaryElements elements, string key)
    {
        try
        {
            return elements.ContainsKey(key) ? elements[key] : null;
        }
        catch (KeyNotFoundException)
        {
            return null;
        }
    }

    private static string TryGetString(PdfDictionary.DictionaryElements elements, string key)
    {
        try
        {
            return elements.TryGetString(key, out string? value) && value is not null
                ? value
                : string.Empty;
        }
        catch (InvalidCastException)
        {
        }

        return TryResolvePdfItemString(TryGetItem(elements, key), preserveWhitespace: false) ?? string.Empty;
    }

    private static string TryGetName(PdfDictionary.DictionaryElements elements, string key)
    {
        try
        {
            return elements.GetName(key);
        }
        catch (InvalidCastException)
        {
        }

        string? value = TryResolvePdfItemString(TryGetItem(elements, key), preserveWhitespace: true);
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return value.StartsWith("/", StringComparison.Ordinal) ? value : $"/{value}";
    }

    private static PdfArray? TryGetArray(PdfDictionary.DictionaryElements elements, string key)
    {
        try
        {
            return elements.GetArray(key);
        }
        catch (InvalidCastException)
        {
            return null;
        }
    }

    private static PdfArray? SafeGetArray(PdfArray array, int index)
    {
        try
        {
            return array.Elements.GetArray(index);
        }
        catch (InvalidCastException)
        {
            return null;
        }
    }

    private static PdfDictionary? SafeGetDictionary(PdfArray array, int index)
    {
        try
        {
            return array.Elements.GetDictionary(index);
        }
        catch (InvalidCastException)
        {
            return null;
        }
    }

    private sealed record ChoiceOption(int RawIndex, string ExportValue, string DisplayValue, string StoredValue);

    private sealed record RadioStateTarget(string StateName, PdfDictionary? Widget);

    private sealed record CheckboxStateTarget(string StateName, PdfDictionary? Widget);

    private static string GetArrayStringOrName(PdfArray array, int index, bool preserveStringWhitespace = false)
    {
        try
        {
            string stringValue = array.Elements.GetString(index);
            if (!string.IsNullOrEmpty(stringValue))
            {
                return preserveStringWhitespace ? stringValue : NormalizeScalarValue(stringValue);
            }
        }
        catch (InvalidCastException)
        {
        }

        try
        {
            string nameValue = array.Elements.GetName(index);
            if (!string.IsNullOrWhiteSpace(nameValue))
            {
                return NormalizeNameValue(nameValue);
            }
        }
        catch (InvalidCastException)
        {
        }

        string? resolvedValue = TryResolvePdfItemString(array.Elements[index], preserveStringWhitespace);
        if (resolvedValue is not null)
        {
            return resolvedValue;
        }

        string fallbackValue = array.Elements[index]?.ToString() ?? string.Empty;
        return preserveStringWhitespace ? fallbackValue : NormalizeScalarValue(fallbackValue);
    }

    private static string? TryResolvePdfItemString(object? item, bool preserveWhitespace)
    {
        if (item is null)
        {
            return null;
        }

        PdfReference.Dereference(ref item);
        if (item is null)
        {
            return null;
        }

        if (item is string rawString)
        {
            return preserveWhitespace ? rawString : NormalizeScalarValue(rawString);
        }

        object? valueProperty = GetProperty(item, "Value");
        if (valueProperty is string stringValue)
        {
            return preserveWhitespace ? stringValue : NormalizeScalarValue(stringValue);
        }

        if (valueProperty is not null)
        {
            string scalarValue = valueProperty.ToString() ?? string.Empty;
            return preserveWhitespace ? scalarValue : NormalizeScalarValue(scalarValue);
        }

        string fallbackValue = item.ToString() ?? string.Empty;
        return preserveWhitespace ? fallbackValue : NormalizeScalarValue(fallbackValue);
    }
}
