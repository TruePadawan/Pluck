using DotMake.CommandLine;
using Pluck.Cli.Utils;

namespace Pluck.Cli.Commands;

[CliCommand(Name = "key-gen",
    Description = "Generates a random key and copies it to the clipboard",
    Parent = typeof(PluckCliCommand))]
public class KeyGenCliCommand
{
    public void Run()
    {
        try
        {
            var key = Guid.NewGuid().ToString("N");

            try
            {
                ClipboardHelper.Copy(key);
                SpectreOutput.Copied("Key");
            }
            catch (Exception e)
            {
                SpectreOutput.Warn($"Failed to copy key to clipboard: {e.Message}");
            }

            SpectreOutput.Success("Generated key successfully.");
            SpectreOutput.ApiKeyPanel(key);
        }
        catch (Exception e)
        {
            SpectreOutput.Error($"Failed to generate key: {e.Message}");
        }
    }
}
