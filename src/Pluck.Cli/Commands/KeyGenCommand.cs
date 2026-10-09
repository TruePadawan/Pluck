using System.Threading;
using Spectre.Console.Cli;
using Pluck.Cli.Utils;
using System;
using System.ComponentModel;

namespace Pluck.Cli.Commands;

public class KeyGenCommandSettings : CommandSettings
{
}

public class KeyGenCommand : Command<KeyGenCommandSettings>
{
    public override int Execute(CommandContext context, KeyGenCommandSettings settings, CancellationToken cancellationToken)
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
            return 0;
        }
        catch (Exception e)
        {
            SpectreOutput.Error($"Failed to generate key: {e.Message}");
            return 1;
        }
    }
}
