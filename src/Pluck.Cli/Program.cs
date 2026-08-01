using DotMake.CommandLine;
using Pluck.Cli.Commands;

try
{
    Cli.Run<PluckCliCommand>(args);
}
catch (Exception e)
{
    Console.WriteLine($"Exception in main: {e.Message}");
}