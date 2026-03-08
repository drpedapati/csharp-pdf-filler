using System.Text.Json;
using Syncfusion.Pdf;
using Syncfusion.Pdf.Interactive;
using Syncfusion.Pdf.Parsing;

namespace PdfFormFiller.Cli;

public sealed class PdfFormService
{
    public FormInspection Inspect(string pdfPath)
    {
        string fullPath = NormalizePath(pdfPath);
        EnsureFileExists(fullPath);

        PdfLoadedDocument document = new(fullPath);
        try
        {
            PdfLoadedForm? form = document.Form;
            if (form is null)
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

            bool isXfa = IsXfaForm(form);
            List<FormFieldInfo> fields = LoadFields(form);
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
        finally
        {
            document.Close(true);
        }
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

    public FillResult Fill(string pdfPath, string valuesPath, string outputPath, bool flatten)
    {
        string fullPdfPath = NormalizePath(pdfPath);
        string fullValuesPath = NormalizePath(valuesPath);
        string fullOutputPath = NormalizePath(outputPath);

        EnsureFileExists(fullPdfPath);
        EnsureFileExists(fullValuesPath);

        PdfLoadedDocument document = new(fullPdfPath);
        try
        {
            PdfLoadedForm form = document.Form ?? throw new InvalidOperationException("PDF does not contain a form.");
            if (IsXfaForm(form))
            {
                throw new InvalidOperationException("XFA forms are not supported for fill. Provide a true AcroForm PDF.");
            }

            Dictionary<string, JsonElement> values = LoadValues(fullValuesPath);
            HashSet<string> unusedKeys = new(values.Keys, StringComparer.OrdinalIgnoreCase);
            List<string> skipped = [];
            int applied = 0;
            int supportedFillableFieldCount = 0;

            foreach (PdfField field in form.Fields)
            {
                if (IsSupportedFillableField(field))
                {
                    supportedFillableFieldCount++;
                }

                if (!values.TryGetValue(field.Name, out JsonElement value))
                {
                    continue;
                }

                unusedKeys.Remove(field.Name);

                if (!TryApplyValue(field, value))
                {
                    skipped.Add(field.Name);
                    continue;
                }

                applied++;
            }

            if (supportedFillableFieldCount == 0)
            {
                throw new InvalidOperationException("PDF contains an AcroForm, but no supported fillable fields were found.");
            }

            if (applied == 0)
            {
                string unusedSummary = unusedKeys.Count > 0
                    ? $" Unused input keys: {string.Join(", ", unusedKeys.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).Take(10))}."
                    : string.Empty;
                throw new InvalidOperationException($"No JSON keys matched fillable form fields.{unusedSummary}");
            }

            Directory.CreateDirectory(Path.GetDirectoryName(fullOutputPath)!);
            form.SetDefaultAppearance(false);
            if (flatten)
            {
                form.FlattenFields();
            }
            document.Save(fullOutputPath);

            return new FillResult(
                fullPdfPath,
                fullOutputPath,
                "acroform",
                flatten,
                applied,
                skipped,
                unusedKeys.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray()
            );
        }
        finally
        {
            document.Close(true);
        }
    }

    private static List<FormFieldInfo> LoadFields(PdfLoadedForm form)
    {
        List<FormFieldInfo> fields = [];
        foreach (PdfField field in form.Fields)
        {
            string kind = DetectKind(field);
            fields.Add(new FormFieldInfo(
                field.Name,
                kind,
                GetStringProperty(field, "ToolTip"),
                GetBoolProperty(field, "ReadOnly"),
                GetBoolProperty(field, "Required"),
                GetCurrentValue(field, kind),
                GetChoices(field)
            ));
        }
        return fields.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static string DetectKind(PdfField field) => field switch
    {
        PdfLoadedTextBoxField => "text",
        PdfLoadedCheckBoxField => "checkbox",
        PdfLoadedComboBoxField => "combo",
        PdfLoadedListBoxField => "list",
        PdfLoadedRadioButtonListField => "radio",
        PdfLoadedSignatureField => "signature",
        _ => field.GetType().Name,
    };

    private static object? GetCurrentValue(PdfField field, string kind) => kind switch
    {
        "text" => GetPropertyValue(field, "Text"),
        "checkbox" => GetPropertyValue(field, "Checked"),
        "combo" => GetPropertyValue(field, "SelectedValue"),
        "list" => GetPropertyValue(field, "SelectedValue"),
        "radio" => GetPropertyValue(field, "SelectedValue"),
        _ => null,
    };

    private static IReadOnlyList<string> GetChoices(PdfField field)
    {
        if (field is PdfLoadedRadioButtonListField radio)
        {
            List<string> radioChoices = [];
            foreach (object item in radio.Items)
            {
                string? value = GetStringProperty(item, "Value");
                string? optionValue = GetStringProperty(item, "OptionValue");
                if (!string.IsNullOrWhiteSpace(value))
                {
                    radioChoices.Add(value);
                }
                else if (!string.IsNullOrWhiteSpace(optionValue))
                {
                    radioChoices.Add(optionValue);
                }
            }

            if (radioChoices.Count > 0)
            {
                return radioChoices.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            }
        }

        object? valuesCollection = GetPropertyValue(field, "Values");
        List<string> choices = [];
        if (valuesCollection is System.Collections.IEnumerable enumerable)
        {
            foreach (object? item in enumerable)
            {
                if (item is null)
                {
                    continue;
                }

                string? value = GetStringProperty(item, "Value");
                string? text = GetStringProperty(item, "Text");
                if (!string.IsNullOrWhiteSpace(value))
                {
                    choices.Add(value);
                }
                else if (!string.IsNullOrWhiteSpace(text))
                {
                    choices.Add(text);
                }
            }
        }

        return choices.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static bool IsSupportedFillableField(FormFieldInfo field) =>
        IsSupportedFillableKind(field.Kind);

    private static bool IsSupportedFillableField(PdfField field) =>
        IsSupportedFillableKind(DetectKind(field));

    private static bool IsSupportedFillableKind(string kind) => kind is "text" or "checkbox" or "combo" or "list" or "radio";

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

    private static bool IsXfaForm(PdfLoadedForm form)
    {
        object? explicitFlag = GetPropertyValue(form, "IsXFAForm");
        if (explicitFlag is bool boolFlag)
        {
            return boolFlag;
        }

        object? loadedXfa = GetPropertyValue(form, "LoadedXfa");
        return loadedXfa is not null;
    }

    private static Dictionary<string, JsonElement> LoadValues(string valuesPath)
    {
        using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(valuesPath));
        if (doc.RootElement.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException("Values JSON must be an object keyed by field name.");
        }

        Dictionary<string, JsonElement> values = new(StringComparer.OrdinalIgnoreCase);
        foreach (JsonProperty property in doc.RootElement.EnumerateObject())
        {
            values[property.Name] = property.Value.Clone();
        }

        return values;
    }

    private static bool TryApplyValue(PdfField field, JsonElement value)
    {
        switch (field)
        {
            case PdfLoadedTextBoxField:
                return TrySetProperty(field, "Text", CoerceToString(value));
            case PdfLoadedCheckBoxField:
                return TrySetCheckboxValue(field, value);
            case PdfLoadedComboBoxField:
                return TrySetSelectedValue(field, value);
            case PdfLoadedListBoxField:
                return TrySetSelectedValue(field, value);
            case PdfLoadedRadioButtonListField:
                return TrySetRadioValue(field, value);
            default:
                return false;
        }
    }

    private static bool TrySetSelectedValue(PdfField field, JsonElement value)
    {
        var property = ReflectionPropertyHelper.FindProperty(field.GetType(), "SelectedValue");
        if (property is null || !property.CanWrite)
        {
            return false;
        }

        object? converted = property.PropertyType == typeof(string)
            ? CoerceToString(value)
            : property.PropertyType == typeof(string[])
                ? CoerceToStringArray(value)
                : null;

        if (converted is null)
        {
            return false;
        }

        property.SetValue(field, converted);
        return true;
    }

    private static bool TrySetRadioValue(PdfField field, JsonElement value)
    {
        if (field is not PdfLoadedRadioButtonListField radio)
        {
            return false;
        }

        string requestedValue = CoerceToString(value);
        if (string.IsNullOrWhiteSpace(requestedValue))
        {
            return false;
        }

        for (int i = 0; i < radio.Items.Count; i++)
        {
            object item = radio.Items[i];
            string itemValue = GetStringProperty(item, "Value");
            string optionValue = GetStringProperty(item, "OptionValue");
            if (StringComparer.OrdinalIgnoreCase.Equals(requestedValue, itemValue) ||
                StringComparer.OrdinalIgnoreCase.Equals(requestedValue, optionValue))
            {
                radio.SelectedIndex = i;
                return true;
            }
        }

        if (TryGetIndexedChoiceMatch(field, requestedValue, out int matchIndex) && matchIndex >= 0 && matchIndex < radio.Items.Count)
        {
            radio.SelectedIndex = matchIndex;
            return true;
        }

        return TrySetProperty(field, "SelectedValue", requestedValue);
    }

    private static bool TrySetCheckboxValue(PdfField field, JsonElement value)
    {
        if (!TryCoerceToBool(value, out bool parsed))
        {
            return false;
        }

        return TrySetProperty(field, "Checked", parsed);
    }

    private static bool TrySetProperty(PdfField field, string propertyName, object? value)
    {
        if (!ReflectionPropertyHelper.TrySetValue(field, propertyName, value))
        {
            return false;
        }

        return true;
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

    private static bool TryGetIndexedChoiceMatch(PdfField field, string requestedValue, out int matchIndex)
    {
        matchIndex = -1;
        object? valuesCollection = GetPropertyValue(field, "Values");
        if (valuesCollection is not System.Collections.IEnumerable enumerable)
        {
            return false;
        }

        int index = 0;
        foreach (object? item in enumerable)
        {
            string candidate = item?.ToString() ?? string.Empty;
            if (StringComparer.OrdinalIgnoreCase.Equals(candidate, requestedValue))
            {
                matchIndex = index;
                return true;
            }

            index++;
        }

        return false;
    }

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

    private static string GetStringProperty(object target, string propertyName) =>
        GetPropertyValue(target, propertyName)?.ToString() ?? string.Empty;

    private static bool GetBoolProperty(object target, string propertyName)
    {
        object? value = GetPropertyValue(target, propertyName);
        return value is bool result && result;
    }

    private static object? GetPropertyValue(object target, string propertyName) =>
        ReflectionPropertyHelper.GetValue(target, propertyName);

    private static string NormalizePath(string path) => Path.GetFullPath(path);

    private static void EnsureFileExists(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"File not found: {path}");
        }
    }
}
