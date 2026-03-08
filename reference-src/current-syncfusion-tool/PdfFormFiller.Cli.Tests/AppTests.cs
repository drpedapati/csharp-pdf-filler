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
}
