using System.Text;

namespace PdfFormFiller.Cli.Tests;

public sealed class AppTests
{
    [Fact]
    public void Run_UnknownCommand_PrintsErrorAndReturnsTwo()
    {
        StringBuilder stderr = new();
        using StringWriter errorWriter = new(stderr);
        TextWriter originalError = Console.Error;

        try
        {
            Console.SetError(errorWriter);
            int exitCode = App.Run(["bogus"]);

            Assert.Equal(2, exitCode);
        }
        finally
        {
            Console.SetError(originalError);
        }

        Assert.Contains("Error: unknown command: bogus", stderr.ToString());
    }

    [Fact]
    public void Run_MissingPdfFile_FailsBeforeBackendWork()
    {
        string missingPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.pdf");
        StubPdfFormService service = new();

        StringBuilder stderr = new();
        using StringWriter errorWriter = new(stderr);
        TextWriter originalError = Console.Error;

        try
        {
            Console.SetError(errorWriter);
            int exitCode = App.Run(["inspect", "--pdf", missingPath], service);

            Assert.Equal(1, exitCode);
            Assert.Equal(0, service.InspectCallCount);
        }
        finally
        {
            Console.SetError(originalError);
        }

        Assert.Contains("--pdf file not found", stderr.ToString());
    }

    [Fact]
    public void Run_MissingValuesFile_FailsBeforeBackendWork()
    {
        string pdfPath = CreateTempFile(".pdf");
        string missingValuesPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.json");
        string outputPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.pdf");
        StubPdfFormService service = new();

        StringBuilder stderr = new();
        using StringWriter errorWriter = new(stderr);
        TextWriter originalError = Console.Error;

        try
        {
            Console.SetError(errorWriter);
            int exitCode = App.Run(
                ["fill", "--pdf", pdfPath, "--values", missingValuesPath, "--out", outputPath],
                service
            );

            Assert.Equal(1, exitCode);
            Assert.Equal(0, service.FillCallCount);
        }
        finally
        {
            Console.SetError(originalError);
            File.Delete(pdfPath);
        }

        Assert.Contains("--values file not found", stderr.ToString());
    }

    private static string CreateTempFile(string extension)
    {
        string path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}{extension}");
        File.WriteAllText(path, "placeholder");
        return path;
    }

    private sealed class StubPdfFormService : IPdfFormService
    {
        public int InspectCallCount { get; private set; }

        public int FillCallCount { get; private set; }

        public FormInspection Inspect(string pdfPath)
        {
            InspectCallCount++;
            return new FormInspection(pdfPath, "none", false, false, false, 0, "stub", 0, []);
        }

        public FormSchema Schema(string pdfPath) =>
            new(pdfPath, "none", false, false, false, 0, "stub", 0, []);

        public FillResult Fill(string pdfPath, string valuesPath, string outputPath, bool flatten, bool experimentalXfa = false)
        {
            FillCallCount++;
            return new FillResult(pdfPath, outputPath, experimentalXfa ? "xfa" : "none", flatten, 0, [], []);
        }
    }
}
