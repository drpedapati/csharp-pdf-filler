namespace PdfFormFiller.Cli;

public enum CommandKind
{
    Help,
    Inspect,
    Schema,
    Fill,
}

public sealed record CliOptions(
    CommandKind Command,
    string? PdfPath,
    string? ValuesPath,
    string? OutputPath,
    string? LicenseKey,
    bool JsonOutput,
    bool Flatten
);

public sealed record ParseResult(CliOptions? Options, string? Error, bool ShowHelp, int ExitCode);

public static class CommandLineParser
{
    public static string HelpText =>
        """
        pdf-form-filler

        Standalone CLI for inspecting and filling true AcroForm PDFs.

        Commands:
          inspect --pdf <file> [--json] [--license-key <key>]
          schema  --pdf <file> [--license-key <key>]
          fill    --pdf <file> --values <file.json> --out <filled.pdf> [--flatten] [--json] [--license-key <key>]

        Notes:
          - The tool rejects PDFs that are not real AcroForms.
          - XFA forms are detected and rejected for fill.
          - Set SYNCFUSION_LICENSE_KEY or pass --license-key.

        Values JSON:
          {
            "FieldName": "text value",
            "CheckboxField": true,
            "RadioField": "OptionValue",
            "ListField": ["A", "B"]
          }
        """;

    public static ParseResult Parse(string[] args)
    {
        if (args.Length == 0)
        {
            return new ParseResult(null, null, true, 0);
        }

        string commandToken = args[0].Trim().ToLowerInvariant();
        if (commandToken is "help" or "-h" or "--help")
        {
            return new ParseResult(null, null, true, 0);
        }

        CommandKind command = commandToken switch
        {
            "inspect" => CommandKind.Inspect,
            "schema" => CommandKind.Schema,
            "fill" => CommandKind.Fill,
            _ => CommandKind.Help,
        };

        if (command == CommandKind.Help)
        {
            return new ParseResult(null, $"unknown command: {args[0]}", true, 2);
        }

        string? pdfPath = null;
        string? valuesPath = null;
        string? outputPath = null;
        string? licenseKey = null;
        bool jsonOutput = false;
        bool flatten = false;

        for (int i = 1; i < args.Length; i++)
        {
            string token = args[i];
            switch (token)
            {
                case "--pdf":
                    if (!TryReadValue(args, ref i, out pdfPath))
                    {
                        return Error("--pdf requires a value");
                    }
                    break;
                case "--values":
                    if (!TryReadValue(args, ref i, out valuesPath))
                    {
                        return Error("--values requires a value");
                    }
                    break;
                case "--out":
                    if (!TryReadValue(args, ref i, out outputPath))
                    {
                        return Error("--out requires a value");
                    }
                    break;
                case "--license-key":
                    if (!TryReadValue(args, ref i, out licenseKey))
                    {
                        return Error("--license-key requires a value");
                    }
                    break;
                case "--json":
                    jsonOutput = true;
                    break;
                case "--flatten":
                    flatten = true;
                    break;
                case "--help":
                case "-h":
                    return new ParseResult(null, null, true, 0);
                default:
                    return Error($"unknown option: {token}");
            }
        }

        if (string.IsNullOrWhiteSpace(pdfPath))
        {
            return Error("--pdf is required");
        }

        if (command == CommandKind.Fill)
        {
            if (string.IsNullOrWhiteSpace(valuesPath))
            {
                return Error("--values is required for fill");
            }
            if (string.IsNullOrWhiteSpace(outputPath))
            {
                return Error("--out is required for fill");
            }
        }

        return new ParseResult(
            new CliOptions(command, pdfPath, valuesPath, outputPath, licenseKey, jsonOutput, flatten),
            null,
            false,
            0
        );
    }

    private static ParseResult Error(string message) => new(null, message, false, 2);

    private static bool TryReadValue(string[] args, ref int index, out string? value)
    {
        if (index + 1 >= args.Length)
        {
            value = null;
            return false;
        }

        value = args[++index];
        return true;
    }
}
