namespace PdfFormFiller.Cli;

public sealed record FormFieldInfo(
    string Name,
    string Kind,
    string? ToolTip,
    bool ReadOnly,
    bool Required,
    object? CurrentValue,
    IReadOnlyList<string> Choices
);

public sealed record FormSchemaFieldInfo(
    string Name,
    string Kind,
    string? ToolTip,
    bool ReadOnly,
    bool Required,
    IReadOnlyList<string> Choices
);

public sealed record FormInspection(
    string PdfPath,
    string FormType,
    bool IsXfaForm,
    bool IsSupportedAcroForm,
    bool CanFillValues,
    int SupportedFillableFieldCount,
    string ValidationMessage,
    int FieldCount,
    IReadOnlyList<FormFieldInfo> Fields
);

public sealed record FormSchema(
    string PdfPath,
    string FormType,
    bool IsXfaForm,
    bool IsSupportedAcroForm,
    bool CanFillValues,
    int SupportedFillableFieldCount,
    string ValidationMessage,
    int FieldCount,
    IReadOnlyList<FormSchemaFieldInfo> Fields
);

public sealed record FillResult(
    string PdfPath,
    string OutputPath,
    string FormType,
    bool Flattened,
    int AppliedFields,
    IReadOnlyList<string> SkippedFields,
    IReadOnlyList<string> UnusedInputKeys
);
