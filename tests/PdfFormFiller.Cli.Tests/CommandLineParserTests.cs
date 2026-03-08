namespace PdfFormFiller.Cli.Tests;

public sealed class CommandLineParserTests
{
    [Fact]
    public void Parse_WithNoArgs_ShowsHelp()
    {
        ParseResult result = CommandLineParser.Parse([]);

        Assert.True(result.ShowHelp);
        Assert.Equal(0, result.ExitCode);
        Assert.Null(result.Error);
    }

    [Fact]
    public void Parse_InspectCommand_ParsesExpectedOptions()
    {
        ParseResult result = CommandLineParser.Parse(["inspect", "--pdf", "form.pdf", "--json"]);

        Assert.False(result.ShowHelp);
        Assert.Null(result.Error);
        Assert.NotNull(result.Options);
        Assert.Equal(CommandKind.Inspect, result.Options!.Command);
        Assert.Equal("form.pdf", result.Options.PdfPath);
        Assert.True(result.Options.JsonOutput);
    }

    [Fact]
    public void Parse_FillWithoutValues_ReturnsError()
    {
        ParseResult result = CommandLineParser.Parse(["fill", "--pdf", "form.pdf", "--out", "filled.pdf"]);

        Assert.Equal(2, result.ExitCode);
        Assert.Equal("--values is required for fill", result.Error);
    }

    [Fact]
    public void Parse_FillWithoutOut_ReturnsError()
    {
        ParseResult result = CommandLineParser.Parse(["fill", "--pdf", "form.pdf", "--values", "values.json"]);

        Assert.Equal(2, result.ExitCode);
        Assert.Equal("--out is required for fill", result.Error);
    }

    [Fact]
    public void Parse_UnknownOption_ReturnsError()
    {
        ParseResult result = CommandLineParser.Parse(["inspect", "--pdf", "form.pdf", "--bogus"]);

        Assert.Equal(2, result.ExitCode);
        Assert.Equal("unknown option: --bogus", result.Error);
    }
}
