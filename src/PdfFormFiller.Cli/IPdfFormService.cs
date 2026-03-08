namespace PdfFormFiller.Cli;

public interface IPdfFormService
{
    FormInspection Inspect(string pdfPath);

    FormSchema Schema(string pdfPath);

    FillResult Fill(string pdfPath, string valuesPath, string outputPath, bool flatten, bool experimentalXfa = false);
}
