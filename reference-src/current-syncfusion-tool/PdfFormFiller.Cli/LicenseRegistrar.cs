using Syncfusion.Licensing;

namespace PdfFormFiller.Cli;

public static class LicenseRegistrar
{
    public static void Register(string? explicitKey)
    {
        string? key = string.IsNullOrWhiteSpace(explicitKey)
            ? Environment.GetEnvironmentVariable("SYNCFUSION_LICENSE_KEY")
            : explicitKey;

        if (string.IsNullOrWhiteSpace(key))
        {
            throw new InvalidOperationException(
                "Syncfusion license key is required. Set SYNCFUSION_LICENSE_KEY or pass --license-key."
            );
        }

        SyncfusionLicenseProvider.RegisterLicense(key);
    }
}
