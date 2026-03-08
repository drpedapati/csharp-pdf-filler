using System.Text.Json;
using Syncfusion.Licensing;
using Syncfusion.Pdf;
using Syncfusion.Pdf.Graphics;
using Syncfusion.Pdf.Interactive;

namespace PdfFormFiller.Cli.Tests;

public sealed class PdfFormServiceTests
{
    private readonly PdfFormService _service = new();

    static PdfFormServiceTests()
    {
        SyncfusionLicenseProvider.RegisterLicense("dummy");
    }

    [Fact]
    public void Inspect_SignatureOnlyForm_IsUnsupportedForValueFill()
    {
        string pdfPath = CreateSignatureOnlyPdf();

        FormInspection inspection = _service.Inspect(pdfPath);

        Assert.False(inspection.IsSupportedAcroForm);
        Assert.False(inspection.CanFillValues);
        Assert.Equal(0, inspection.SupportedFillableFieldCount);
        Assert.Equal("PDF contains an AcroForm, but no supported fillable fields were found.", inspection.ValidationMessage);
    }

    [Fact]
    public void Inspect_BlankPdf_ReturnsStructuredUnsupportedResult()
    {
        string pdfPath = CreateBlankPdf();

        FormInspection inspection = _service.Inspect(pdfPath);

        Assert.Equal("acroform", inspection.FormType);
        Assert.False(inspection.IsSupportedAcroForm);
        Assert.False(inspection.CanFillValues);
        Assert.Equal(0, inspection.FieldCount);
        Assert.Equal("Form exists but contains no fields.", inspection.ValidationMessage);
    }

    [Fact]
    public void Schema_OmitsCurrentValues_FromSerializedOutput()
    {
        string pdfPath = CreateMixedFormPdf();

        FormSchema schema = _service.Schema(pdfPath);
        string json = JsonSerializer.Serialize(schema);

        Assert.DoesNotContain("CurrentValue", json);
        Assert.DoesNotContain("Jane Prefilled", json);
        Assert.Equal(4, schema.SupportedFillableFieldCount);
        Assert.Null(typeof(FormSchemaFieldInfo).GetProperty("CurrentValue"));
    }

    [Fact]
    public void Fill_InvalidCheckboxValue_IsSkipped_AndUnusedKeysReported()
    {
        string pdfPath = CreateTextAndCheckboxPdf();
        string valuesPath = WriteValuesFile(new Dictionary<string, object?>
        {
            ["Name"] = "Alice Example",
            ["Agree"] = "maybe",
            ["UnusedField"] = "extra",
        });
        string outputPath = CreateTempPath(".pdf");

        FillResult result = _service.Fill(pdfPath, valuesPath, outputPath, flatten: false);
        FormInspection inspection = _service.Inspect(outputPath);

        Assert.Equal(1, result.AppliedFields);
        Assert.Contains("Agree", result.SkippedFields);
        Assert.Contains("UnusedField", result.UnusedInputKeys);
        Assert.Equal("Alice Example", inspection.Fields.Single(f => f.Name == "Name").CurrentValue?.ToString());
        Assert.Equal(false, inspection.Fields.Single(f => f.Name == "Agree").CurrentValue);
    }

    [Fact]
    public void Fill_RadioAndComboChoices_Persist()
    {
        string pdfPath = CreateMixedFormPdf();
        string valuesPath = WriteValuesFile(new Dictionary<string, object?>
        {
            ["Name"] = "Updated Name",
            ["Subscribe"] = true,
            ["ProviderType"] = "NP",
            ["MayLeaveVoicemail"] = "Yes",
        });
        string outputPath = CreateTempPath(".pdf");

        FillResult result = _service.Fill(pdfPath, valuesPath, outputPath, flatten: false);
        FormInspection inspection = _service.Inspect(outputPath);

        Assert.Equal(4, result.AppliedFields);
        Assert.Empty(result.SkippedFields);
        Assert.Equal("Updated Name", inspection.Fields.Single(f => f.Name == "Name").CurrentValue?.ToString());
        Assert.Equal(true, inspection.Fields.Single(f => f.Name == "Subscribe").CurrentValue);
        Assert.Equal("NP", inspection.Fields.Single(f => f.Name == "ProviderType").CurrentValue?.ToString());
        Assert.Equal("Yes", inspection.Fields.Single(f => f.Name == "MayLeaveVoicemail").CurrentValue?.ToString());
        Assert.Contains("Yes", inspection.Fields.Single(f => f.Name == "MayLeaveVoicemail").Choices);
        Assert.Contains("No", inspection.Fields.Single(f => f.Name == "MayLeaveVoicemail").Choices);
    }

    [Fact]
    public void Fill_WithFlattenTrue_RemovesFillableForm()
    {
        string pdfPath = CreateTextAndCheckboxPdf();
        string valuesPath = WriteValuesFile(new Dictionary<string, object?>
        {
            ["Name"] = "Flattened",
            ["Agree"] = true,
        });
        string outputPath = CreateTempPath(".pdf");

        _service.Fill(pdfPath, valuesPath, outputPath, flatten: true);

        FormInspection inspection = _service.Inspect(outputPath);

        Assert.False(inspection.IsSupportedAcroForm);
        Assert.False(inspection.CanFillValues);
    }

    private static string CreateTextAndCheckboxPdf()
    {
        string path = CreateTempPath(".pdf");
        using PdfDocument document = new();
        PdfPage page = document.Pages.Add();

        PdfTextBoxField text = new(page, "Name")
        {
            Bounds = new Syncfusion.Drawing.RectangleF(20, 20, 200, 24),
        };
        document.Form.Fields.Add(text);

        PdfCheckBoxField checkbox = new(page, "Agree")
        {
            Bounds = new Syncfusion.Drawing.RectangleF(20, 60, 18, 18),
            Checked = false,
        };
        document.Form.Fields.Add(checkbox);

        document.Save(path);
        return path;
    }

    private static string CreateSignatureOnlyPdf()
    {
        string path = CreateTempPath(".pdf");
        using PdfDocument document = new();
        PdfPage page = document.Pages.Add();

        PdfSignatureField signature = new(page, "Signature1")
        {
            Bounds = new Syncfusion.Drawing.RectangleF(20, 20, 120, 40),
        };
        document.Form.Fields.Add(signature);

        document.Save(path);
        return path;
    }

    private static string CreateBlankPdf()
    {
        string path = CreateTempPath(".pdf");
        using PdfDocument document = new();
        document.Pages.Add();
        document.Save(path);
        return path;
    }

    private static string CreateMixedFormPdf()
    {
        string path = CreateTempPath(".pdf");
        using PdfDocument document = new();
        PdfPage page = document.Pages.Add();
        PdfGraphics graphics = page.Graphics;
        PdfFont font = new PdfStandardFont(PdfFontFamily.Helvetica, 10);

        PdfTextBoxField name = new(page, "Name")
        {
            Bounds = new Syncfusion.Drawing.RectangleF(20, 20, 200, 24),
            Text = "Jane Prefilled",
            Font = font,
        };
        document.Form.Fields.Add(name);

        PdfCheckBoxField subscribe = new(page, "Subscribe")
        {
            Bounds = new Syncfusion.Drawing.RectangleF(20, 60, 18, 18),
            Checked = false,
        };
        document.Form.Fields.Add(subscribe);
        graphics.DrawString("Subscribe", font, PdfBrushes.Black, 45, 60);

        PdfComboBoxField providerType = new(page, "ProviderType")
        {
            Bounds = new Syncfusion.Drawing.RectangleF(20, 100, 200, 24),
            Font = font,
        };
        providerType.Items.Add(new PdfListFieldItem("MD", "MD"));
        providerType.Items.Add(new PdfListFieldItem("NP", "NP"));
        providerType.SelectedIndex = 0;
        document.Form.Fields.Add(providerType);

        PdfRadioButtonListField voicemail = new(page, "MayLeaveVoicemail");
        document.Form.Fields.Add(voicemail);
        PdfRadioButtonListItem yesItem = new("Yes")
        {
            Bounds = new Syncfusion.Drawing.RectangleF(20, 140, 18, 18),
        };
        PdfRadioButtonListItem noItem = new("No")
        {
            Bounds = new Syncfusion.Drawing.RectangleF(80, 140, 18, 18),
        };
        voicemail.Items.Add(yesItem);
        voicemail.Items.Add(noItem);
        graphics.DrawString("Yes", font, PdfBrushes.Black, 45, 140);
        graphics.DrawString("No", font, PdfBrushes.Black, 105, 140);

        document.Save(path);
        return path;
    }

    private static string WriteValuesFile(Dictionary<string, object?> values)
    {
        string path = CreateTempPath(".json");
        string json = JsonSerializer.Serialize(values, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(path, json);
        return path;
    }

    private static string CreateTempPath(string extension) =>
        Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}{extension}");
}
