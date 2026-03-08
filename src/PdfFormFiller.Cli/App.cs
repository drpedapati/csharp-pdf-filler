using System.Text.Json;

namespace PdfFormFiller.Cli;

public static class App
{
    public static int Run(string[] args) => Run(args, new PdfFormService());

    internal static int Run(string[] args, IPdfFormService service)
    {
        ParseResult parse = CommandLineParser.Parse(args);
        if (parse.ShowHelp)
        {
            if (!string.IsNullOrWhiteSpace(parse.Error))
            {
                Console.Error.WriteLine($"Error: {parse.Error}");
                Console.Error.WriteLine();
                Console.Error.WriteLine(CommandLineParser.HelpText);
            }
            else
            {
                Console.WriteLine(CommandLineParser.HelpText);
            }

            return parse.ExitCode;
        }

        if (parse.Error is not null || parse.Options is null)
        {
            if (!string.IsNullOrWhiteSpace(parse.Error))
            {
                Console.Error.WriteLine($"Error: {parse.Error}");
                Console.Error.WriteLine();
            }

            Console.Error.WriteLine(CommandLineParser.HelpText);
            return parse.ExitCode;
        }

        try
        {
            PreflightInputs(parse.Options);
            LicenseRegistrar.Register(parse.Options.LicenseKey);
            return parse.Options.Command switch
            {
                CommandKind.Inspect => RunInspect(service, parse.Options),
                CommandKind.Schema => RunSchema(service, parse.Options),
                CommandKind.Fill => RunFill(service, parse.Options),
                _ => 0,
            };
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 1;
        }
    }

    private static void PreflightInputs(CliOptions options)
    {
        EnsureFileExists("--pdf", options.PdfPath);
        if (options.Command == CommandKind.Fill)
        {
            EnsureFileExists("--values", options.ValuesPath);
        }
    }

    private static void EnsureFileExists(string flagName, string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        string fullPath = Path.GetFullPath(path);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException($"{flagName} file not found: {fullPath}");
        }
    }

    private static int RunInspect(IPdfFormService service, CliOptions options)
    {
        FormInspection inspection = service.Inspect(options.PdfPath!);
        if (options.JsonOutput)
        {
            WriteJson(inspection);
            return inspection.IsSupportedAcroForm ? 0 : 1;
        }

        if (inspection.IsSupportedAcroForm)
        {
            Console.WriteLine("AcroForm inspection passed");
            Console.WriteLine($"  PDF: {inspection.PdfPath}");
            Console.WriteLine($"  Form type: {inspection.FormType}");
            Console.WriteLine($"  Fields: {inspection.FieldCount}");
            Console.WriteLine($"  Fillable fields: {inspection.SupportedFillableFieldCount}");
            Console.WriteLine($"  Validation: {inspection.ValidationMessage}");
            return 0;
        }

        Console.Error.WriteLine("AcroForm inspection failed");
        Console.Error.WriteLine($"  PDF: {inspection.PdfPath}");
        Console.Error.WriteLine($"  Form type: {inspection.FormType}");
        Console.Error.WriteLine($"  Fields: {inspection.FieldCount}");
        Console.Error.WriteLine($"  Fillable fields: {inspection.SupportedFillableFieldCount}");
        Console.Error.WriteLine($"  Validation: {inspection.ValidationMessage}");
        return 1;
    }

    private static int RunSchema(IPdfFormService service, CliOptions options)
    {
        FormSchema schema = service.Schema(options.PdfPath!);
        if (!schema.IsSupportedAcroForm)
        {
            throw new InvalidOperationException(schema.ValidationMessage);
        }

        WriteJson(schema);
        return 0;
    }

    private static int RunFill(IPdfFormService service, CliOptions options)
    {
        FillResult result = service.Fill(options.PdfPath!, options.ValuesPath!, options.OutputPath!, options.Flatten);
        if (options.JsonOutput)
        {
            WriteJson(result);
            return 0;
        }

        Console.WriteLine("Fill completed");
        Console.WriteLine($"  Input: {result.PdfPath}");
        Console.WriteLine($"  Output: {result.OutputPath}");
        Console.WriteLine($"  Form type: {result.FormType}");
        Console.WriteLine($"  Applied fields: {result.AppliedFields}");
        Console.WriteLine($"  Flattened: {result.Flattened}");
        if (result.SkippedFields.Count > 0)
        {
            Console.WriteLine($"  Skipped fields: {result.SkippedFields.Count}");
            Console.WriteLine($"  Skipped field names: {string.Join(", ", result.SkippedFields.Take(10))}");
        }

        if (result.UnusedInputKeys.Count > 0)
        {
            Console.WriteLine($"  Unused input keys: {result.UnusedInputKeys.Count}");
            Console.WriteLine($"  Unused key names: {string.Join(", ", result.UnusedInputKeys.Take(10))}");
        }

        return 0;
    }

    private static void WriteJson<T>(T value)
    {
        JsonSerializerOptions options = new()
        {
            WriteIndented = true,
        };
        Console.WriteLine(JsonSerializer.Serialize(value, options));
    }
}
