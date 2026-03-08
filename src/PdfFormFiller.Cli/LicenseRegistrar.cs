namespace PdfFormFiller.Cli;

public static class LicenseRegistrar
{
    public static void Register(string? explicitKey)
    {
        _ = explicitKey;
        _ = Environment.GetEnvironmentVariable("SYNCFUSION_LICENSE_KEY");
    }
}
