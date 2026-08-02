using DotMake.CommandLine;
using Pluck.Cli.Commands;
using Pluck.Cli.Utils;

try
{
    Cli.Run<PluckCliCommand>(args);
}
catch (Exception e)
{
    SpectreOutput.Error($"Unexpected error: {e.Message}");
}