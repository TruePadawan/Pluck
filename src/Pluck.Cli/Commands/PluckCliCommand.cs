using DotMake.CommandLine;

namespace Pluck.Cli.Commands;

/// <summary>
/// Defines the root pluck cli command
/// </summary>
[CliCommand(Name = "pluck", Description = "Pluck CLI")]
public class PluckCliCommand
{
    public void Run(CliContext context)
    {
        if (!context.Result.HasArgs)
        {
            context.ShowHelp();
        }
    }
}