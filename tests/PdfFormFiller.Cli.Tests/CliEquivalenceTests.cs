using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Text.Json;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using PdfSharp.Pdf.Security;
using Xunit.Sdk;

namespace PdfFormFiller.Cli.Tests;

[Trait("Category", "SyncfusionEquivalence")]
public sealed class CliEquivalenceTests
{
    private static readonly Lazy<CliArtifacts> BuiltCliArtifacts = new(CreateCliArtifacts);

    [Fact]
    public void Inspect_CorpusMatchesOldCli()
    {
        CliArtifacts artifacts = RequireCliArtifacts();
        string[] pdfPaths = Directory
            .GetFiles(GetWorkspacePath("fixtures", "corpus-originals"), "*.pdf", SearchOption.AllDirectories)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.Equal(24, pdfPaths.Length);

        foreach (string pdfPath in pdfPaths)
        {
            CliInvocationResult oldResult = RunCli(artifacts.OldCliDllPath, ["inspect", "--pdf", pdfPath, "--json"], artifacts.SyncfusionLicenseKey);
            CliInvocationResult newResult = RunCli(artifacts.NewCliDllPath, ["inspect", "--pdf", pdfPath, "--json"]);

            Assert.Equal(oldResult.ExitCode, newResult.ExitCode);

            FormInspectionSnapshot expected = CreateInspectionSnapshot(ParseJson<FormInspection>(oldResult.StandardOutput));
            FormInspectionSnapshot actual = CreateInspectionSnapshot(ParseJson<FormInspection>(newResult.StandardOutput));
            string expectedJson = JsonSerializer.Serialize(expected);
            string actualJson = JsonSerializer.Serialize(actual);

            Assert.True(
                string.Equals(expectedJson, actualJson, StringComparison.Ordinal),
                $"Inspect mismatch for {pdfPath}{Environment.NewLine}Expected: {expectedJson}{Environment.NewLine}Actual: {actualJson}"
            );
        }
    }

    [Fact]
    public void Schema_ReferencePackagesMatchOldCli()
    {
        CliArtifacts artifacts = RequireCliArtifacts();
        string[] packageDirectories = Directory.GetDirectories(GetWorkspacePath("fixtures", "reference-fills"))
            .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.Equal(11, packageDirectories.Length);

        foreach (string packageDirectory in packageDirectories)
        {
            string pdfPath = Path.Combine(packageDirectory, "original.pdf");

            CliInvocationResult oldResult = RunCli(artifacts.OldCliDllPath, ["schema", "--pdf", pdfPath], artifacts.SyncfusionLicenseKey);
            CliInvocationResult newResult = RunCli(artifacts.NewCliDllPath, ["schema", "--pdf", pdfPath]);

            Assert.Equal(oldResult.ExitCode, newResult.ExitCode);

            FormSchemaSnapshot expected = CreateSchemaSnapshot(ParseJson<FormSchema>(oldResult.StandardOutput));
            FormSchemaSnapshot actual = CreateSchemaSnapshot(ParseJson<FormSchema>(newResult.StandardOutput));
            string expectedJson = JsonSerializer.Serialize(expected);
            string actualJson = JsonSerializer.Serialize(actual);

            Assert.True(
                string.Equals(expectedJson, actualJson, StringComparison.Ordinal),
                $"Schema mismatch for {pdfPath}{Environment.NewLine}Expected: {expectedJson}{Environment.NewLine}Actual: {actualJson}"
            );
        }
    }

    [Fact]
    public void Fill_ReferencePackagesMatchOldCli()
    {
        CliArtifacts artifacts = RequireCliArtifacts();
        string[] packageDirectories = Directory.GetDirectories(GetWorkspacePath("fixtures", "reference-fills"))
            .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.Equal(11, packageDirectories.Length);

        foreach (string packageDirectory in packageDirectories)
        {
            string pdfPath = Path.Combine(packageDirectory, "original.pdf");
            string valuesPath = Path.Combine(packageDirectory, "reference-values.json");
            string oldOutputPath = Path.Combine(Path.GetTempPath(), $"old-{Path.GetFileName(packageDirectory)}-{Guid.NewGuid():N}.pdf");
            string newOutputPath = Path.Combine(Path.GetTempPath(), $"new-{Path.GetFileName(packageDirectory)}-{Guid.NewGuid():N}.pdf");

            try
            {
                CliInvocationResult oldFillResult = RunCli(
                    artifacts.OldCliDllPath,
                    ["fill", "--pdf", pdfPath, "--values", valuesPath, "--out", oldOutputPath, "--json"],
                    artifacts.SyncfusionLicenseKey
                );
                CliInvocationResult newFillResult = RunCli(
                    artifacts.NewCliDllPath,
                    ["fill", "--pdf", pdfPath, "--values", valuesPath, "--out", newOutputPath, "--json"]
                );

                Assert.Equal(oldFillResult.ExitCode, newFillResult.ExitCode);

                FillResultSnapshot expectedFill = CreateFillResultSnapshot(ParseJson<FillResult>(oldFillResult.StandardOutput));
                FillResultSnapshot actualFill = CreateFillResultSnapshot(ParseJson<FillResult>(newFillResult.StandardOutput));
                string expectedFillJson = JsonSerializer.Serialize(expectedFill);
                string actualFillJson = JsonSerializer.Serialize(actualFill);

                Assert.True(
                    string.Equals(expectedFillJson, actualFillJson, StringComparison.Ordinal),
                    $"Fill result mismatch for {pdfPath}{Environment.NewLine}Expected: {expectedFillJson}{Environment.NewLine}Actual: {actualFillJson}"
                );

                FormInspectionSnapshot oldCliSnapshotForOldOutput = CreateInspectionSnapshot(
                    ParseInspectionWithCli(artifacts.OldCliDllPath, oldOutputPath, artifacts.SyncfusionLicenseKey)
                );
                FormInspectionSnapshot oldCliSnapshotForNewOutput = CreateInspectionSnapshot(
                    ParseInspectionWithCli(artifacts.OldCliDllPath, newOutputPath, artifacts.SyncfusionLicenseKey)
                );
                FormInspectionSnapshot newCliSnapshotForOldOutput = CreateInspectionSnapshot(
                    ParseInspectionWithCli(artifacts.NewCliDllPath, oldOutputPath)
                );
                FormInspectionSnapshot newCliSnapshotForNewOutput = CreateInspectionSnapshot(
                    ParseInspectionWithCli(artifacts.NewCliDllPath, newOutputPath)
                );
                string oldCliSnapshotForOldOutputJson = JsonSerializer.Serialize(oldCliSnapshotForOldOutput);
                string oldCliSnapshotForNewOutputJson = JsonSerializer.Serialize(oldCliSnapshotForNewOutput);
                string newCliSnapshotForOldOutputJson = JsonSerializer.Serialize(newCliSnapshotForOldOutput);
                string newCliSnapshotForNewOutputJson = JsonSerializer.Serialize(newCliSnapshotForNewOutput);

                Assert.True(
                    string.Equals(oldCliSnapshotForOldOutputJson, oldCliSnapshotForNewOutputJson, StringComparison.Ordinal),
                    $"Old CLI inspect mismatch for fill output {pdfPath}{Environment.NewLine}Expected: {oldCliSnapshotForOldOutputJson}{Environment.NewLine}Actual: {oldCliSnapshotForNewOutputJson}"
                );
                Assert.True(
                    string.Equals(newCliSnapshotForOldOutputJson, newCliSnapshotForNewOutputJson, StringComparison.Ordinal),
                    $"New CLI inspect mismatch for fill output {pdfPath}{Environment.NewLine}Expected: {newCliSnapshotForOldOutputJson}{Environment.NewLine}Actual: {newCliSnapshotForNewOutputJson}"
                );

                EncryptedDocumentSnapshot? oldEncryption = ReadEncryptedDocumentSnapshot(oldOutputPath);
                EncryptedDocumentSnapshot? newEncryption = ReadEncryptedDocumentSnapshot(newOutputPath);
                Assert.True(
                    oldEncryption == newEncryption,
                    $"Encryption mismatch for fill output {pdfPath}{Environment.NewLine}Expected: {JsonSerializer.Serialize(oldEncryption)}{Environment.NewLine}Actual: {JsonSerializer.Serialize(newEncryption)}"
                );
            }
            finally
            {
                File.Delete(oldOutputPath);
                File.Delete(newOutputPath);
            }
        }
    }

    [Fact]
    public void FillFlatten_RepresentativeFixturesMatchOldCli()
    {
        CliArtifacts artifacts = RequireCliArtifacts();
        string[] packageDirectories =
        [
            GetWorkspacePath("fixtures", "reference-fills", "bcbsks-prior-authorization-request"),
            GetWorkspacePath("fixtures", "reference-fills", "uhc-tx-commercial-prior-auth"),
        ];

        foreach (string packageDirectory in packageDirectories)
        {
            string pdfPath = Path.Combine(packageDirectory, "original.pdf");
            string valuesPath = Path.Combine(packageDirectory, "reference-values.json");
            string oldOutputPath = Path.Combine(Path.GetTempPath(), $"old-flat-{Path.GetFileName(packageDirectory)}-{Guid.NewGuid():N}.pdf");
            string newOutputPath = Path.Combine(Path.GetTempPath(), $"new-flat-{Path.GetFileName(packageDirectory)}-{Guid.NewGuid():N}.pdf");

            try
            {
                CliInvocationResult oldFillResult = RunCli(
                    artifacts.OldCliDllPath,
                    ["fill", "--pdf", pdfPath, "--values", valuesPath, "--out", oldOutputPath, "--flatten", "--json"],
                    artifacts.SyncfusionLicenseKey
                );
                CliInvocationResult newFillResult = RunCli(
                    artifacts.NewCliDllPath,
                    ["fill", "--pdf", pdfPath, "--values", valuesPath, "--out", newOutputPath, "--flatten", "--json"]
                );

                Assert.Equal(oldFillResult.ExitCode, newFillResult.ExitCode);
                FillResultSnapshot oldFillSnapshot = CreateFillResultSnapshot(ParseJson<FillResult>(oldFillResult.StandardOutput));
                FillResultSnapshot newFillSnapshot = CreateFillResultSnapshot(ParseJson<FillResult>(newFillResult.StandardOutput));
                string oldFillJson = JsonSerializer.Serialize(oldFillSnapshot);
                string newFillJson = JsonSerializer.Serialize(newFillSnapshot);
                Assert.True(
                    string.Equals(oldFillJson, newFillJson, StringComparison.Ordinal),
                    $"Flatten fill result mismatch for {pdfPath}{Environment.NewLine}Expected: {oldFillJson}{Environment.NewLine}Actual: {newFillJson}"
                );

                FormInspectionSnapshot oldCliSnapshotForOldOutput = CreateInspectionSnapshot(
                    ParseInspectionWithCli(artifacts.OldCliDllPath, oldOutputPath, artifacts.SyncfusionLicenseKey)
                );
                FormInspectionSnapshot oldCliSnapshotForNewOutput = CreateInspectionSnapshot(
                    ParseInspectionWithCli(artifacts.OldCliDllPath, newOutputPath, artifacts.SyncfusionLicenseKey)
                );
                FormInspectionSnapshot newCliSnapshotForOldOutput = CreateInspectionSnapshot(
                    ParseInspectionWithCli(artifacts.NewCliDllPath, oldOutputPath)
                );
                FormInspectionSnapshot newCliSnapshotForNewOutput = CreateInspectionSnapshot(
                    ParseInspectionWithCli(artifacts.NewCliDllPath, newOutputPath)
                );
                string oldCliSnapshotForOldOutputJson = JsonSerializer.Serialize(oldCliSnapshotForOldOutput);
                string oldCliSnapshotForNewOutputJson = JsonSerializer.Serialize(oldCliSnapshotForNewOutput);
                string newCliSnapshotForOldOutputJson = JsonSerializer.Serialize(newCliSnapshotForOldOutput);
                string newCliSnapshotForNewOutputJson = JsonSerializer.Serialize(newCliSnapshotForNewOutput);

                Assert.True(
                    string.Equals(oldCliSnapshotForOldOutputJson, oldCliSnapshotForNewOutputJson, StringComparison.Ordinal),
                    $"Old CLI flatten inspect mismatch for {pdfPath}{Environment.NewLine}Expected: {oldCliSnapshotForOldOutputJson}{Environment.NewLine}Actual: {oldCliSnapshotForNewOutputJson}"
                );
                Assert.True(
                    string.Equals(newCliSnapshotForOldOutputJson, newCliSnapshotForNewOutputJson, StringComparison.Ordinal),
                    $"New CLI flatten inspect mismatch for {pdfPath}{Environment.NewLine}Expected: {newCliSnapshotForOldOutputJson}{Environment.NewLine}Actual: {newCliSnapshotForNewOutputJson}"
                );
            }
            finally
            {
                File.Delete(oldOutputPath);
                File.Delete(newOutputPath);
            }
        }
    }

    [Fact]
    public void Fill_ExperimentalXfa_RepresentativeFixturesMatchOldCliStructurally()
    {
        CliArtifacts artifacts = RequireCliArtifacts();
        string[] pdfPaths =
        [
            GetWorkspacePath("fixtures", "exploratory-downloads", "insurance", "irs-w4-2026.pdf"),
            GetWorkspacePath("fixtures", "exploratory-downloads", "insurance", "dol-ca-17-duty-status-report.pdf"),
        ];

        foreach (string pdfPath in pdfPaths)
        {
            string valuesPath = Path.Combine(Path.GetTempPath(), $"xfa-values-{Guid.NewGuid():N}.json");
            string oldOutputPath = Path.Combine(Path.GetTempPath(), $"old-xfa-{Path.GetFileNameWithoutExtension(pdfPath)}-{Guid.NewGuid():N}.pdf");
            string newOutputPath = Path.Combine(Path.GetTempPath(), $"new-xfa-{Path.GetFileNameWithoutExtension(pdfPath)}-{Guid.NewGuid():N}.pdf");

            try
            {
                FormInspection sourceInspection = ParseInspectionWithCli(artifacts.OldCliDllPath, pdfPath, artifacts.SyncfusionLicenseKey);
                File.WriteAllText(valuesPath, CreateExperimentalXfaValuesJson(sourceInspection));

                CliInvocationResult oldFillResult = RunCli(
                    artifacts.OldCliDllPath,
                    ["fill", "--pdf", pdfPath, "--values", valuesPath, "--out", oldOutputPath, "--json"],
                    artifacts.SyncfusionLicenseKey
                );
                CliInvocationResult newFillResult = RunCli(
                    artifacts.NewCliDllPath,
                    ["fill", "--pdf", pdfPath, "--values", valuesPath, "--out", newOutputPath, "--experimental-xfa", "--json"]
                );

                Assert.Equal(0, oldFillResult.ExitCode);
                Assert.Equal(0, newFillResult.ExitCode);

                ExperimentalFillResultSnapshot expectedFill = CreateExperimentalFillResultSnapshot(
                    ParseJson<FillResult>(oldFillResult.StandardOutput)
                );
                ExperimentalFillResultSnapshot actualFill = CreateExperimentalFillResultSnapshot(
                    ParseJson<FillResult>(newFillResult.StandardOutput)
                );

                Assert.Equal(JsonSerializer.Serialize(expectedFill), JsonSerializer.Serialize(actualFill));

                ExperimentalInspectionSnapshot newCliSnapshotForOldOutput = CreateExperimentalInspectionSnapshot(
                    ParseInspectionWithCli(artifacts.NewCliDllPath, oldOutputPath)
                );
                ExperimentalInspectionSnapshot newCliSnapshotForNewOutput = CreateExperimentalInspectionSnapshot(
                    ParseInspectionWithCli(artifacts.NewCliDllPath, newOutputPath)
                );

                Assert.Equal(JsonSerializer.Serialize(newCliSnapshotForOldOutput), JsonSerializer.Serialize(newCliSnapshotForNewOutput));
            }
            finally
            {
                File.Delete(valuesPath);
                File.Delete(oldOutputPath);
                File.Delete(newOutputPath);
            }
        }
    }

    [Fact]
    public void Inspect_ExploratoryCorpus_MatchesOldCli_Structurally()
    {
        CliArtifacts artifacts = RequireCliArtifacts();
        ExploratoryPdfFixture[] fixtures = LoadExploratoryCatalog()
            .Where(fixture => string.Equals(fixture.Expectation, "structural_match_old", StringComparison.Ordinal))
            .OrderBy(fixture => fixture.RelativePath, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.Equal(19, fixtures.Length);

        foreach (ExploratoryPdfFixture fixture in fixtures)
        {
            string pdfPath = GetWorkspacePath("fixtures", "exploratory-downloads", fixture.RelativePath);
            CliInvocationResult oldInspectResult = RunCli(
                artifacts.OldCliDllPath,
                ["inspect", "--pdf", pdfPath, "--json"],
                artifacts.SyncfusionLicenseKey
            );
            CliInvocationResult newInspectResult = RunCli(artifacts.NewCliDllPath, ["inspect", "--pdf", pdfPath, "--json"]);

            Assert.Equal(oldInspectResult.ExitCode, newInspectResult.ExitCode);

            FormInspectionSnapshot expectedInspect = CreateExploratoryInspectionSnapshot(
                ParseJson<FormInspection>(oldInspectResult.StandardOutput)
            );
            FormInspectionSnapshot actualInspect = CreateExploratoryInspectionSnapshot(
                ParseJson<FormInspection>(newInspectResult.StandardOutput)
            );
            string expectedInspectJson = JsonSerializer.Serialize(expectedInspect);
            string actualInspectJson = JsonSerializer.Serialize(actualInspect);

            Assert.True(
                string.Equals(expectedInspectJson, actualInspectJson, StringComparison.Ordinal),
                $"Exploratory inspect mismatch for {fixture.Id} ({pdfPath}){Environment.NewLine}Expected: {expectedInspectJson}{Environment.NewLine}Actual: {actualInspectJson}"
            );

            FormInspection parsedInspect = ParseJson<FormInspection>(newInspectResult.StandardOutput);
            if (!parsedInspect.IsSupportedAcroForm)
            {
                continue;
            }

            CliInvocationResult oldSchemaResult = RunCli(
                artifacts.OldCliDllPath,
                ["schema", "--pdf", pdfPath],
                artifacts.SyncfusionLicenseKey
            );
            CliInvocationResult newSchemaResult = RunCli(artifacts.NewCliDllPath, ["schema", "--pdf", pdfPath]);

            Assert.Equal(oldSchemaResult.ExitCode, newSchemaResult.ExitCode);

            FormSchemaSnapshot expectedSchema = CreateExploratorySchemaSnapshot(ParseJson<FormSchema>(oldSchemaResult.StandardOutput));
            FormSchemaSnapshot actualSchema = CreateExploratorySchemaSnapshot(ParseJson<FormSchema>(newSchemaResult.StandardOutput));
            string expectedSchemaJson = JsonSerializer.Serialize(expectedSchema);
            string actualSchemaJson = JsonSerializer.Serialize(actualSchema);

            Assert.True(
                string.Equals(expectedSchemaJson, actualSchemaJson, StringComparison.Ordinal),
                $"Exploratory schema mismatch for {fixture.Id} ({pdfPath}){Environment.NewLine}Expected: {expectedSchemaJson}{Environment.NewLine}Actual: {actualSchemaJson}"
            );
        }
    }

    [Fact]
    public void Inspect_ExploratoryGovernmentXfaForm_IsRejectedByNewCli()
    {
        string newProjectPath = GetWorkspacePath("src", "PdfFormFiller.Cli", "PdfFormFiller.Cli.csproj");
        string newCliDllPath = GetProjectDllPath(newProjectPath);
        if (!File.Exists(newCliDllPath))
        {
            BuildProject(newProjectPath);
        }

        ExploratoryPdfFixture[] fixtures = LoadExploratoryCatalog()
            .Where(entry => string.Equals(entry.Expectation, "new_rejects_xfa", StringComparison.Ordinal))
            .OrderBy(entry => entry.RelativePath, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.Equal(11, fixtures.Length);

        foreach (ExploratoryPdfFixture fixture in fixtures)
        {
            string pdfPath = GetWorkspacePath("fixtures", "exploratory-downloads", fixture.RelativePath);
            CliInvocationResult inspectResult = RunCli(newCliDllPath, ["inspect", "--pdf", pdfPath, "--json"]);
            FormInspection inspection = ParseJson<FormInspection>(inspectResult.StandardOutput);

            Assert.Equal(1, inspectResult.ExitCode);
            Assert.Equal("xfa", inspection.FormType);
            Assert.True(inspection.IsXfaForm);
            Assert.False(inspection.IsSupportedAcroForm);
            Assert.False(inspection.CanFillValues);
            Assert.Equal("PDF contains an XFA form. Only true AcroForm PDFs are supported.", inspection.ValidationMessage);
            Assert.True(inspection.FieldCount > 0);
        }
    }

    [Fact]
    public void ExploratoryCatalog_CoversAllDownloadedPdfs()
    {
        ExploratoryPdfFixture[] fixtures = LoadExploratoryCatalog();
        string[] actualPdfPaths = Directory
            .GetFiles(GetWorkspacePath("fixtures", "exploratory-downloads"), "*.pdf", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(GetWorkspacePath("fixtures", "exploratory-downloads"), path).Replace('\\', '/'))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        string[] catalogPdfPaths = fixtures
            .Select(fixture => fixture.RelativePath.Replace('\\', '/'))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.Equal(actualPdfPaths, catalogPdfPaths);
    }

    private static CliArtifacts RequireCliArtifacts()
    {
        string? syncfusionLicenseKey = ResolveSyncfusionLicenseKey();
        if (string.IsNullOrWhiteSpace(syncfusionLicenseKey))
        {
            throw SkipException.ForSkip(
                "Set SYNCFUSION_LICENSE_KEY or create .env.local to run live Syncfusion equivalence tests."
            );
        }

        return BuiltCliArtifacts.Value;
    }

    private static CliArtifacts CreateCliArtifacts()
    {
        string newProjectPath = GetWorkspacePath("src", "PdfFormFiller.Cli", "PdfFormFiller.Cli.csproj");
        string oldProjectPath = GetWorkspacePath(
            "reference-src",
            "current-syncfusion-tool",
            "PdfFormFiller.Cli",
            "PdfFormFiller.Cli.csproj"
        );
        string newCliDllPath = GetProjectDllPath(newProjectPath);
        string oldCliDllPath = GetProjectDllPath(oldProjectPath);

        if (!File.Exists(newCliDllPath))
        {
            BuildProject(newProjectPath);
        }

        BuildProject(oldProjectPath);

        return new CliArtifacts(
            newCliDllPath,
            oldCliDllPath,
            ResolveSyncfusionLicenseKey() ?? throw new InvalidOperationException("Missing Syncfusion license key.")
        );
    }

    private static void BuildProject(string projectPath)
    {
        CliInvocationResult result = RunProcess(
            "dotnet",
            ["build", projectPath, "--configuration", "Debug", "--nologo", "--verbosity", "minimal"]
        );

        Assert.True(result.ExitCode == 0, $"Build failed for {projectPath}:{Environment.NewLine}{result.StandardError}");
    }

    private static string GetProjectDllPath(string projectPath)
    {
        string projectDirectory = Path.GetDirectoryName(projectPath)
            ?? throw new InvalidOperationException($"Unable to determine project directory for {projectPath}");

        return Path.Combine(projectDirectory, "bin", "Debug", "net10.0", "pdf-form-filler.dll");
    }

    private static CliInvocationResult RunCli(string dllPath, IReadOnlyList<string> args, string? syncfusionLicenseKey = null)
    {
        List<string> command = [dllPath, .. args];
        return RunProcess("dotnet", command, syncfusionLicenseKey);
    }

    private static CliInvocationResult RunProcess(
        string fileName,
        IReadOnlyList<string> args,
        string? syncfusionLicenseKey = null
    )
    {
        ProcessStartInfo startInfo = new()
        {
            FileName = fileName,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            WorkingDirectory = GetWorkspacePath(),
        };

        foreach (string arg in args)
        {
            startInfo.ArgumentList.Add(arg);
        }

        if (!string.IsNullOrWhiteSpace(syncfusionLicenseKey))
        {
            startInfo.Environment["SYNCFUSION_LICENSE_KEY"] = syncfusionLicenseKey;
        }

        using Process process = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"Failed to start process: {fileName}");
        string standardOutput = process.StandardOutput.ReadToEnd();
        string standardError = process.StandardError.ReadToEnd();

        process.WaitForExit();

        return new CliInvocationResult(process.ExitCode, standardOutput, standardError);
    }

    private static FormInspection ParseInspectionWithCli(string dllPath, string pdfPath, string? syncfusionLicenseKey = null)
    {
        CliInvocationResult result = RunCli(dllPath, ["inspect", "--pdf", pdfPath, "--json"], syncfusionLicenseKey);

        return ParseJson<FormInspection>(result.StandardOutput);
    }

    private static T ParseJson<T>(string json)
    {
        T? value = JsonSerializer.Deserialize<T>(json);
        return value ?? throw new InvalidOperationException($"Unable to parse JSON: {json}");
    }

    private static ExploratoryPdfFixture[] LoadExploratoryCatalog()
    {
        string catalogPath = GetWorkspacePath("fixtures", "exploratory-downloads", "catalog.json");
        ExploratoryPdfFixture[]? fixtures = JsonSerializer.Deserialize<ExploratoryPdfFixture[]>(
            File.ReadAllText(catalogPath),
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
            }
        );
        return fixtures ?? throw new InvalidOperationException($"Unable to parse exploratory catalog: {catalogPath}");
    }

    private static string? ResolveSyncfusionLicenseKey()
    {
        string? fromEnvironment = Environment.GetEnvironmentVariable("SYNCFUSION_LICENSE_KEY");
        if (!string.IsNullOrWhiteSpace(fromEnvironment))
        {
            return fromEnvironment;
        }

        string envLocalPath = GetWorkspacePath(".env.local");
        if (!File.Exists(envLocalPath))
        {
            return null;
        }

        foreach (string rawLine in File.ReadLines(envLocalPath))
        {
            string line = rawLine.Trim();
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#'))
            {
                continue;
            }

            if (line.StartsWith("export ", StringComparison.Ordinal))
            {
                line = line["export ".Length..].Trim();
            }

            int separatorIndex = line.IndexOf('=');
            if (separatorIndex < 0)
            {
                continue;
            }

            string key = line[..separatorIndex].Trim();
            if (!string.Equals(key, "SYNCFUSION_LICENSE_KEY", StringComparison.Ordinal))
            {
                continue;
            }

            string value = line[(separatorIndex + 1)..].Trim();
            if (value.Length >= 2 && value[0] == '\'' && value[^1] == '\'')
            {
                return value[1..^1];
            }

            if (value.Length >= 2 && value[0] == '"' && value[^1] == '"')
            {
                return value[1..^1];
            }

            return value;
        }

        return null;
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
                NormalizeKind(field.Kind),
                field.ToolTip,
                field.ReadOnly,
                field.Required,
                JsonSerializer.Serialize(field.CurrentValue),
                field.Choices.ToArray())).ToArray()
        );

    private static FormSchemaSnapshot CreateSchemaSnapshot(FormSchema schema) =>
        new(
            schema.FormType,
            schema.IsXfaForm,
            schema.IsSupportedAcroForm,
            schema.CanFillValues,
            schema.SupportedFillableFieldCount,
            schema.ValidationMessage,
            schema.FieldCount,
            schema.Fields.Select(field => new SchemaFieldSnapshot(
                field.Name,
                NormalizeKind(field.Kind),
                field.ToolTip,
                field.ReadOnly,
                field.Required,
                field.Choices.ToArray())).ToArray()
        );

    private static FormInspectionSnapshot CreateExploratoryInspectionSnapshot(FormInspection inspection) =>
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
                NormalizeKind(field.Kind),
                field.ToolTip,
                field.ReadOnly,
                field.Required,
                JsonSerializer.Serialize(NormalizeExploratoryCurrentValue(field)),
                NormalizeExploratoryChoices(field.Choices))).ToArray()
        );

    private static FormSchemaSnapshot CreateExploratorySchemaSnapshot(FormSchema schema) =>
        new(
            schema.FormType,
            schema.IsXfaForm,
            schema.IsSupportedAcroForm,
            schema.CanFillValues,
            schema.SupportedFillableFieldCount,
            schema.ValidationMessage,
            schema.FieldCount,
            schema.Fields.Select(field => new SchemaFieldSnapshot(
                field.Name,
                NormalizeKind(field.Kind),
                field.ToolTip,
                field.ReadOnly,
                field.Required,
                NormalizeExploratoryChoices(field.Choices))).ToArray()
        );

    private static ExperimentalInspectionSnapshot CreateExperimentalInspectionSnapshot(FormInspection inspection) =>
        new(
            inspection.FieldCount,
            inspection.Fields.Select(field => new FormFieldSnapshot(
                field.Name,
                NormalizeKind(field.Kind),
                field.ToolTip,
                field.ReadOnly,
                field.Required,
                JsonSerializer.Serialize(NormalizeExploratoryCurrentValue(field)),
                NormalizeExploratoryChoices(field.Choices))).ToArray()
        );

    private static FillResultSnapshot CreateFillResultSnapshot(FillResult result) =>
        new(result.FormType, result.Flattened, result.AppliedFields, result.SkippedFields.ToArray(), result.UnusedInputKeys.ToArray());

    private static ExperimentalFillResultSnapshot CreateExperimentalFillResultSnapshot(FillResult result) =>
        new(result.Flattened, result.AppliedFields, result.SkippedFields.ToArray(), result.UnusedInputKeys.ToArray());

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

    private static string NormalizeKind(string kind) =>
        kind is "text" or "checkbox" or "combo" or "list" or "radio" or "signature"
            ? kind
            : "unsupported";

    private static IReadOnlyList<string> NormalizeExploratoryChoices(IReadOnlyList<string> choices) =>
        choices
            .Where(choice => !string.IsNullOrWhiteSpace(choice))
            .Select(choice => choice.TrimEnd())
            .ToArray();

    private static object? NormalizeExploratoryCurrentValue(FormFieldInfo field)
    {
        if (!field.ReadOnly || NormalizeKind(field.Kind) != "text")
        {
            return field.CurrentValue;
        }

        if (field.CurrentValue is string text && !string.IsNullOrEmpty(text))
        {
            return "<read-only-text>";
        }

        if (field.CurrentValue is JsonElement element && element.ValueKind == JsonValueKind.String && !string.IsNullOrEmpty(element.GetString()))
        {
            return "<read-only-text>";
        }

        return field.CurrentValue;
    }

    private static string CreateExperimentalXfaValuesJson(FormInspection inspection)
    {
        Dictionary<string, object?> values = new(StringComparer.OrdinalIgnoreCase);
        int textIndex = 1;

        foreach (FormFieldInfo field in inspection.Fields)
        {
            if (field.ReadOnly)
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
                    values[field.Name] = $"XFA {textIndex++}";
                    break;
            }
        }

        return JsonSerializer.Serialize(values, new JsonSerializerOptions { WriteIndented = true });
    }

    private sealed record CliArtifacts(string NewCliDllPath, string OldCliDllPath, string SyncfusionLicenseKey);

    private sealed record ExploratoryPdfFixture(
        string Id,
        string Category,
        string RelativePath,
        string DirectPdfUrl,
        string SourcePageUrl,
        string Expectation,
        string Notes
    );

    private sealed record CliInvocationResult(int ExitCode, string StandardOutput, string StandardError);

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

    private sealed record FormSchemaSnapshot(
        string FormType,
        bool IsXfaForm,
        bool IsSupportedAcroForm,
        bool CanFillValues,
        int SupportedFillableFieldCount,
        string ValidationMessage,
        int FieldCount,
        IReadOnlyList<SchemaFieldSnapshot> Fields
    );

    private sealed record SchemaFieldSnapshot(
        string Name,
        string Kind,
        string? ToolTip,
        bool ReadOnly,
        bool Required,
        IReadOnlyList<string> Choices
    );

    private sealed record ExperimentalInspectionSnapshot(
        int FieldCount,
        IReadOnlyList<FormFieldSnapshot> Fields
    );

    private sealed record FillResultSnapshot(
        string FormType,
        bool Flattened,
        int AppliedFields,
        IReadOnlyList<string> SkippedFields,
        IReadOnlyList<string> UnusedInputKeys
    );

    private sealed record ExperimentalFillResultSnapshot(
        bool Flattened,
        int AppliedFields,
        IReadOnlyList<string> SkippedFields,
        IReadOnlyList<string> UnusedInputKeys
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
