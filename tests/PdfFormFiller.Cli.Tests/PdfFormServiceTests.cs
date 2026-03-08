using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using PdfSharp.Pdf.Security;

namespace PdfFormFiller.Cli.Tests;

public sealed class PdfFormServiceTests
{
    private readonly PdfFormService _service = new();

    [Fact]
    public void Inspect_UnsupportedCorpusPdf_ReturnsStructuredUnsupportedResult()
    {
        string pdfPath = GetWorkspacePath("fixtures", "corpus-originals", "unsupported", "bcbsri-cymbalta-pa-guide.pdf");

        FormInspection inspection = _service.Inspect(pdfPath);

        Assert.Equal(Path.GetFullPath(pdfPath), inspection.PdfPath);
        Assert.Equal("none", inspection.FormType);
        Assert.False(inspection.IsXfaForm);
        Assert.False(inspection.IsSupportedAcroForm);
        Assert.False(inspection.CanFillValues);
        Assert.Equal(0, inspection.SupportedFillableFieldCount);
        Assert.Equal("PDF does not contain a form.", inspection.ValidationMessage);
        Assert.Equal(0, inspection.FieldCount);
        Assert.Empty(inspection.Fields);
    }

    [Fact]
    public void Schema_KnownGoodFixture_MatchesReferenceCounts_AndOmitsCurrentValues()
    {
        string pdfPath = GetWorkspacePath(
            "fixtures",
            "reference-fills",
            "bcbsks-prior-authorization-request",
            "original.pdf"
        );

        FormSchema schema = _service.Schema(pdfPath);
        string json = JsonSerializer.Serialize(schema);

        Assert.True(schema.IsSupportedAcroForm);
        Assert.False(schema.IsXfaForm);
        Assert.Equal("acroform", schema.FormType);
        Assert.Equal(32, schema.FieldCount);
        Assert.Equal(31, schema.SupportedFillableFieldCount);
        Assert.Equal("PDF contains a supported AcroForm.", schema.ValidationMessage);
        Assert.Contains(schema.Fields, field => field.Name == "CLEAR DATA");
        Assert.Contains(schema.Fields, field => field.Name == "Sec1_provider_npi" && field.Kind == "text");
        Assert.DoesNotContain("CurrentValue", json);
    }

    [Fact]
    public void Inspect_CorpusClassificationMatchesBaseline()
    {
        string supportedDirectory = GetWorkspacePath("fixtures", "corpus-originals", "roundtrip-fill-ok");
        string unsupportedDirectory = GetWorkspacePath("fixtures", "corpus-originals", "unsupported");

        string[] supportedPdfs = Directory.GetFiles(supportedDirectory, "*.pdf");
        string[] unsupportedPdfs = Directory.GetFiles(unsupportedDirectory, "*.pdf");

        Assert.Equal(11, supportedPdfs.Length);
        Assert.Equal(13, unsupportedPdfs.Length);

        foreach (string pdfPath in supportedPdfs)
        {
            FormInspection inspection = _service.Inspect(pdfPath);

            Assert.True(inspection.IsSupportedAcroForm, Path.GetFileName(pdfPath));
            Assert.True(inspection.CanFillValues, Path.GetFileName(pdfPath));
            Assert.Equal("acroform", inspection.FormType);
            Assert.False(inspection.IsXfaForm);
            Assert.NotEmpty(inspection.Fields);
        }

        foreach (string pdfPath in unsupportedPdfs)
        {
            FormInspection inspection = _service.Inspect(pdfPath);

            Assert.False(inspection.IsSupportedAcroForm, Path.GetFileName(pdfPath));
            Assert.False(inspection.CanFillValues, Path.GetFileName(pdfPath));
        }
    }

    [Fact]
    public void Schema_AllReferenceFixtures_MatchStableFieldMetadata()
    {
        string referenceRoot = GetWorkspacePath("fixtures", "reference-fills");
        string[] packageDirectories = Directory.GetDirectories(referenceRoot);

        Assert.Equal(11, packageDirectories.Length);

        foreach (string packageDirectory in packageDirectories)
        {
            string schemaPath = Path.Combine(packageDirectory, "schema.json");
            string originalPdfPath = Path.Combine(packageDirectory, "original.pdf");

            FormSchema expected = JsonSerializer.Deserialize<FormSchema>(File.ReadAllText(schemaPath))
                ?? throw new InvalidOperationException($"Unable to parse {schemaPath}");
            FormSchema actual = _service.Schema(originalPdfPath);

            Assert.Equal(expected.FormType, actual.FormType);
            Assert.Equal(expected.IsXfaForm, actual.IsXfaForm);
            Assert.Equal(expected.IsSupportedAcroForm, actual.IsSupportedAcroForm);
            Assert.Equal(expected.CanFillValues, actual.CanFillValues);
            Assert.Equal(expected.SupportedFillableFieldCount, actual.SupportedFillableFieldCount);
            Assert.Equal(expected.ValidationMessage, actual.ValidationMessage);
            Assert.Equal(expected.FieldCount, actual.FieldCount);
            Assert.Equal(expected.Fields.Count, actual.Fields.Count);

            for (int i = 0; i < expected.Fields.Count; i++)
            {
                FormSchemaFieldInfo expectedField = expected.Fields[i];
                FormSchemaFieldInfo actualField = actual.Fields[i];

                Assert.Equal(expectedField.Name, actualField.Name);
                Assert.Equal(expectedField.ToolTip, actualField.ToolTip);
                Assert.Equal(expectedField.ReadOnly, actualField.ReadOnly);
                Assert.Equal(expectedField.Required, actualField.Required);
                Assert.Equal(expectedField.Choices, actualField.Choices);

                if (IsContractFieldKind(expectedField.Kind) || IsContractFieldKind(actualField.Kind))
                {
                    Assert.Equal(expectedField.Kind, actualField.Kind);
                }
            }
        }
    }

    [Fact]
    public void Fill_TextOnlyReferenceFixture_AppliesAllValues()
    {
        string packageDirectory = GetWorkspacePath(
            "fixtures",
            "reference-fills",
            "bcbsks-prior-authorization-request"
        );
        string pdfPath = Path.Combine(packageDirectory, "original.pdf");
        string valuesPath = Path.Combine(packageDirectory, "reference-values.json");
        string outputPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.pdf");

        try
        {
            FillResult result = _service.Fill(pdfPath, valuesPath, outputPath, flatten: false);
            FormInspection inspection = _service.Inspect(outputPath);

            Assert.Equal(10, result.AppliedFields);
            Assert.Empty(result.SkippedFields);
            Assert.Empty(result.UnusedInputKeys);
            Assert.False(result.Flattened);
            Assert.Equal("Sample 1", inspection.Fields.Single(field => field.Name == "Sec1_provider_addr_city").CurrentValue);
            Assert.Equal("Sample 2", inspection.Fields.Single(field => field.Name == "Sec1_provider_addr_state").CurrentValue);
            Assert.Equal("Sample 10", inspection.Fields.Single(field => field.Name == "Sec1_provider_fax_prefix").CurrentValue);

            EncryptedDocumentSnapshot original = ReadEncryptedDocumentSnapshot(pdfPath)
                ?? throw new InvalidOperationException("Expected encrypted input.");
            EncryptedDocumentSnapshot expected = ReadEncryptedDocumentSnapshot(Path.Combine(packageDirectory, "reference-filled.pdf"))
                ?? throw new InvalidOperationException("Expected encrypted reference output.");
            EncryptedDocumentSnapshot output = ReadEncryptedDocumentSnapshot(outputPath)
                ?? throw new InvalidOperationException("Expected encrypted output.");

            Assert.Equal(expected, output);
            Assert.Equal(original.OwnerValueHex, output.OwnerValueHex);
            Assert.Equal(original.UserValueHex, output.UserValueHex);
            Assert.Equal(original.FirstDocumentIdHex, output.FirstDocumentIdHex);
            Assert.Equal(original.FirstDocumentIdHex, output.SecondDocumentIdHex);
        }
        finally
        {
            File.Delete(outputPath);
        }
    }

    [Fact]
    public void Fill_CheckboxHeavyReferenceFixture_MatchesReferenceResult()
    {
        string packageDirectory = GetWorkspacePath(
            "fixtures",
            "reference-fills",
            "uhc-tx-commercial-prior-auth"
        );
        string pdfPath = Path.Combine(packageDirectory, "original.pdf");
        string valuesPath = Path.Combine(packageDirectory, "reference-values.json");
        string outputPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.pdf");

        try
        {
            FillResult result = _service.Fill(pdfPath, valuesPath, outputPath, flatten: false);
            FormInspection inspection = _service.Inspect(outputPath);

            Assert.Equal(40, result.AppliedFields);
            Assert.Empty(result.SkippedFields);
            Assert.Empty(result.UnusedInputKeys);
            Assert.Equal(true, inspection.Fields.Single(field => field.Name == "Cardiac Rehab").CurrentValue);
            Assert.Equal("Sample 1", inspection.Fields.Single(field => field.Name == "Clinical Reason for Urgency").CurrentValue);
            Assert.Equal(true, inspection.Fields.Single(field => field.Name == "Review Type - Urgent").CurrentValue);
            Assert.Null(ReadEncryptedDocumentSnapshot(outputPath));
        }
        finally
        {
            File.Delete(outputPath);
        }
    }

    [Fact]
    public void Fill_RadioHeavyReferenceFixture_MatchesReferenceResult()
    {
        string packageDirectory = GetWorkspacePath(
            "fixtures",
            "reference-fills",
            "aetna-il-rx-prior-auth"
        );
        string pdfPath = Path.Combine(packageDirectory, "original.pdf");
        string valuesPath = Path.Combine(packageDirectory, "reference-values.json");
        string outputPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.pdf");

        try
        {
            FillResult result = _service.Fill(pdfPath, valuesPath, outputPath, flatten: false);
            FormInspection inspection = _service.Inspect(outputPath);

            Assert.Equal(18, result.AppliedFields);
            Assert.Empty(result.SkippedFields);
            Assert.Empty(result.UnusedInputKeys);
            Assert.Equal("Approved", inspection.Fields.Single(field => field.Name == "Approved").CurrentValue);
            Assert.Equal("Denied", inspection.Fields.Single(field => field.Name == "Denied").CurrentValue);
            Assert.Equal("Yes", inspection.Fields.Single(field => field.Name == "Has the patient already started the medication?").CurrentValue);
            Assert.Equal(true, inspection.Fields.Single(field => field.Name == "Initial Authorization Request").CurrentValue);
            Assert.Equal(true, inspection.Fields.Single(field => field.Name == "Renewal Request").CurrentValue);

            EncryptedDocumentSnapshot original = ReadEncryptedDocumentSnapshot(pdfPath)
                ?? throw new InvalidOperationException("Expected encrypted input.");
            EncryptedDocumentSnapshot expected = ReadEncryptedDocumentSnapshot(Path.Combine(packageDirectory, "reference-filled.pdf"))
                ?? throw new InvalidOperationException("Expected encrypted reference output.");
            EncryptedDocumentSnapshot output = ReadEncryptedDocumentSnapshot(outputPath)
                ?? throw new InvalidOperationException("Expected encrypted output.");

            Assert.Equal(expected, output);
            Assert.Equal(original.OwnerValueHex, output.OwnerValueHex);
            Assert.Equal(original.UserValueHex, output.UserValueHex);
            Assert.Equal(original.FirstDocumentIdHex, output.FirstDocumentIdHex);
            Assert.Equal(original.FirstDocumentIdHex, output.SecondDocumentIdHex);
        }
        finally
        {
            File.Delete(outputPath);
        }
    }

    [Fact]
    public void Fill_AllReferenceFixtures_MatchReferenceOutputs()
    {
        string referenceRoot = GetWorkspacePath("fixtures", "reference-fills");
        string[] packageDirectories = Directory.GetDirectories(referenceRoot)
            .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.Equal(11, packageDirectories.Length);

        foreach (string packageDirectory in packageDirectories)
        {
            string pdfPath = Path.Combine(packageDirectory, "original.pdf");
            string valuesPath = Path.Combine(packageDirectory, "reference-values.json");
            string referenceFilledPath = Path.Combine(packageDirectory, "reference-filled.pdf");
            string expectedFillResultPath = Path.Combine(packageDirectory, "fill-result.json");
            string outputPath = Path.Combine(Path.GetTempPath(), $"{Path.GetFileName(packageDirectory)}-{Guid.NewGuid():N}.pdf");

            try
            {
                FillResult actualResult = _service.Fill(pdfPath, valuesPath, outputPath, flatten: false);
                FillResult expectedResult = JsonSerializer.Deserialize<FillResult>(File.ReadAllText(expectedFillResultPath))
                    ?? throw new InvalidOperationException($"Unable to parse {expectedFillResultPath}");

                Assert.Equal(expectedResult.FormType, actualResult.FormType);
                Assert.Equal(expectedResult.Flattened, actualResult.Flattened);
                Assert.Equal(expectedResult.AppliedFields, actualResult.AppliedFields);
                Assert.Equal(expectedResult.SkippedFields, actualResult.SkippedFields);
                Assert.Equal(expectedResult.UnusedInputKeys, actualResult.UnusedInputKeys);

                FormInspectionSnapshot expectedInspection = CreateInspectionSnapshot(_service.Inspect(referenceFilledPath));
                FormInspectionSnapshot actualInspection = CreateInspectionSnapshot(_service.Inspect(outputPath));

                Assert.Equal(JsonSerializer.Serialize(expectedInspection), JsonSerializer.Serialize(actualInspection));
                Assert.Equal(ReadEncryptedDocumentSnapshot(referenceFilledPath), ReadEncryptedDocumentSnapshot(outputPath));
            }
            finally
            {
                File.Delete(outputPath);
            }
        }
    }

    [Fact]
    public void Fill_WithFlattenTrue_OnEncryptedFixture_RemovesFillableForm()
    {
        string packageDirectory = GetWorkspacePath(
            "fixtures",
            "reference-fills",
            "bcbsks-prior-authorization-request"
        );
        string pdfPath = Path.Combine(packageDirectory, "original.pdf");
        string valuesPath = Path.Combine(packageDirectory, "reference-values.json");
        string outputPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.pdf");

        try
        {
            FillResult result = _service.Fill(pdfPath, valuesPath, outputPath, flatten: true);
            FormInspection inspection = _service.Inspect(outputPath);

            Assert.True(result.Flattened);
            Assert.Equal(10, result.AppliedFields);
            Assert.Empty(result.SkippedFields);
            Assert.Empty(result.UnusedInputKeys);
            Assert.False(inspection.IsSupportedAcroForm);
            Assert.False(inspection.CanFillValues);
            Assert.Equal(0, inspection.SupportedFillableFieldCount);
            Assert.Equal(0, inspection.FieldCount);
        }
        finally
        {
            File.Delete(outputPath);
        }
    }

    [Fact]
    public void Fill_WithFlattenTrue_OnUnencryptedFixture_RemovesFillableForm()
    {
        string packageDirectory = GetWorkspacePath(
            "fixtures",
            "reference-fills",
            "uhc-tx-commercial-prior-auth"
        );
        string pdfPath = Path.Combine(packageDirectory, "original.pdf");
        string valuesPath = Path.Combine(packageDirectory, "reference-values.json");
        string outputPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.pdf");

        try
        {
            FillResult result = _service.Fill(pdfPath, valuesPath, outputPath, flatten: true);
            FormInspection inspection = _service.Inspect(outputPath);

            Assert.True(result.Flattened);
            Assert.Equal(40, result.AppliedFields);
            Assert.Empty(result.SkippedFields);
            Assert.Empty(result.UnusedInputKeys);
            Assert.False(inspection.IsSupportedAcroForm);
            Assert.False(inspection.CanFillValues);
            Assert.Equal(0, inspection.SupportedFillableFieldCount);
            Assert.Equal(0, inspection.FieldCount);
        }
        finally
        {
            File.Delete(outputPath);
        }
    }

    [Fact]
    public void Fill_XfaFixture_RequiresExperimentalFlag()
    {
        string pdfPath = GetWorkspacePath("fixtures", "exploratory-downloads", "insurance", "irs-w4-2026.pdf");
        string valuesPath = CreateExperimentalValuesFile(pdfPath, maximumFieldCount: 20);
        string outputPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.pdf");

        try
        {
            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => _service.Fill(pdfPath, valuesPath, outputPath, flatten: false)
            );

            Assert.Equal("XFA forms are not supported for fill. Provide a true AcroForm PDF.", exception.Message);
        }
        finally
        {
            File.Delete(valuesPath);
            File.Delete(outputPath);
        }
    }

    [Fact]
    public void Fill_XfaFixture_WithExperimentalFlag_WritesShadowFieldValues()
    {
        string pdfPath = GetWorkspacePath("fixtures", "exploratory-downloads", "insurance", "irs-w4-2026.pdf");
        string valuesPath = CreateExperimentalValuesFile(pdfPath, maximumFieldCount: 20);
        string outputPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.pdf");

        try
        {
            FillResult result = _service.Fill(pdfPath, valuesPath, outputPath, flatten: false, experimentalXfa: true);
            FormInspection inspection = _service.Inspect(outputPath);
            JsonObject values = JsonNode.Parse(File.ReadAllText(valuesPath))!.AsObject();

            Assert.Equal("xfa", result.FormType);
            Assert.True(result.AppliedFields > 0);
            Assert.True(File.Exists(outputPath));
            Assert.True(inspection.IsXfaForm);

            Assert.Equal(
                "XFA Test 1",
                inspection.Fields.Single(field => field.Name == "topmostSubform[0].Page1[0].f1_05[0]").CurrentValue
            );
            Assert.Equal(
                "XFA Test 2",
                inspection.Fields.Single(field => field.Name == "topmostSubform[0].Page1[0].f1_08[0]").CurrentValue
            );
            Assert.Equal(
                true,
                inspection.Fields.Single(field => field.Name == "topmostSubform[0].Page1[0].c1_1[0]").CurrentValue
            );
            Assert.Empty(result.UnusedInputKeys);
            Assert.True(values.Count >= 3);
        }
        finally
        {
            File.Delete(valuesPath);
            File.Delete(outputPath);
        }
    }

    private static string GetWorkspacePath(params string[] parts) =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../..", Path.Combine(parts)));

    private static FormInspectionSnapshot CreateInspectionSnapshot(FormInspection inspection) =>
        new(
            inspection.FormType,
            inspection.IsXfaForm,
            inspection.IsSupportedAcroForm,
            inspection.CanFillValues,
            inspection.SupportedFillableFieldCount,
            inspection.ValidationMessage,
            inspection.FieldCount,
            inspection.Fields.Select(field => new FormFieldSnapshot(
                field.Name,
                field.Kind,
                field.ToolTip,
                field.ReadOnly,
                field.Required,
                NormalizeValue(field.CurrentValue),
                field.Choices.ToArray())).ToArray()
        );

    private static EncryptedDocumentSnapshot? ReadEncryptedDocumentSnapshot(string pdfPath)
    {
        using PdfDocument document = PdfReader.Open(pdfPath, PdfDocumentOpenMode.Import);
        object trailer = typeof(PdfDocument)
            .GetProperty("Trailer", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(document)!;
        PdfDictionary.DictionaryElements trailerElements = (PdfDictionary.DictionaryElements)trailer.GetType()
            .GetProperty("Elements", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!
            .GetValue(trailer)!;

        if (!trailerElements.ContainsKey("/Encrypt"))
        {
            return null;
        }

        PdfStandardSecurityHandler securityHandler = (PdfStandardSecurityHandler?)trailer.GetType()
            .GetProperty("SecurityHandler", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!
            .GetValue(trailer)
            ?? throw new InvalidOperationException("Expected a security handler.");

        int version = securityHandler.Elements.GetInteger("/V");
        int revision = securityHandler.Elements.GetInteger("/R");
        int length = securityHandler.Elements.ContainsKey("/Length")
            ? securityHandler.Elements.GetInteger("/Length")
            : 40;
        int permissions = securityHandler.Elements.GetInteger("/P");
        bool encryptMetadata = (securityHandler.Elements["/EncryptMetadata"] as PdfBoolean)?.Value ?? true;
        string cryptFilterMethod = "/V2";
        string ownerValueHex = ToHex(securityHandler.Elements.GetString("/O"));
        string userValueHex = ToHex(securityHandler.Elements.GetString("/U"));
        PdfArray? documentIds = TryGetArray(trailerElements, "/ID");
        string firstDocumentIdHex = documentIds is null ? string.Empty : ToHex(documentIds.Elements.GetString(0));
        string secondDocumentIdHex = documentIds is null ? string.Empty : ToHex(documentIds.Elements.GetString(1));

        PdfDictionary? cryptFilters = TryGetDictionary(securityHandler.Elements, "/CF");
        PdfDictionary? standardCryptFilter = cryptFilters?.Elements.GetDictionary("/StdCF");
        if (standardCryptFilter is not null)
        {
            cryptFilterMethod = standardCryptFilter.Elements.GetName("/CFM");
        }

        return new EncryptedDocumentSnapshot(
            version,
            revision,
            length,
            permissions,
            encryptMetadata,
            cryptFilterMethod,
            ownerValueHex,
            userValueHex,
            firstDocumentIdHex,
            secondDocumentIdHex
        );
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

    private static string ToHex(string value) => Convert.ToHexString(Encoding.Latin1.GetBytes(value));

    private static string NormalizeValue(object? value) => JsonSerializer.Serialize(value);

    private string CreateExperimentalValuesFile(string pdfPath, int maximumFieldCount)
    {
        FormInspection inspection = _service.Inspect(pdfPath);
        JsonObject values = [];
        int textIndex = 1;

        foreach (FormFieldInfo field in inspection.Fields)
        {
            if (field.ReadOnly || values.Count >= maximumFieldCount)
            {
                continue;
            }

            switch (field.Kind)
            {
                case "checkbox":
                    values[field.Name] = true;
                    break;
                case "radio":
                case "combo":
                case "list":
                    if (field.Choices.Count == 0)
                    {
                        continue;
                    }

                    values[field.Name] = field.Choices[0];
                    break;
                case "text":
                    values[field.Name] = $"XFA Test {textIndex++}";
                    break;
                default:
                    continue;
            }
        }

        string path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.json");
        File.WriteAllText(path, values.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        return path;
    }

    private static bool IsContractFieldKind(string kind) =>
        kind is "text" or "checkbox" or "combo" or "list" or "radio" or "signature";

    private sealed record FormInspectionSnapshot(
        string FormType,
        bool IsXfaForm,
        bool IsSupportedAcroForm,
        bool CanFillValues,
        int SupportedFillableFieldCount,
        string ValidationMessage,
        int FieldCount,
        IReadOnlyList<FormFieldSnapshot> Fields
    );

    private sealed record FormFieldSnapshot(
        string Name,
        string Kind,
        string? ToolTip,
        bool ReadOnly,
        bool Required,
        string CurrentValueJson,
        IReadOnlyList<string> Choices
    );

    private sealed record EncryptedDocumentSnapshot(
        int Version,
        int Revision,
        int Length,
        int Permissions,
        bool EncryptMetadata,
        string CryptFilterMethod,
        string OwnerValueHex,
        string UserValueHex,
        string FirstDocumentIdHex,
        string SecondDocumentIdHex
    );
}
