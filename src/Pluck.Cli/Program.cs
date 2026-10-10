using System;
using Spectre.Console.Cli;
using Pluck.Cli.Commands;
using Pluck.Cli.Utils;

var app = new CommandApp();

app.Configure(config =>
{
    config.SetApplicationName("pluck");

    // Register all commands
    config.AddCommand<AdminCommand>("admin")
        .WithDescription("Opens the interactive Admin Dashboard");

    config.AddCommand<ConfigCommand>("config")
        .WithDescription("Sets the configuration for the Pluck CLI");

    config.AddCommand<CreateUserCommand>("create-user")
        .WithDescription("Creates a user in the Pluck instance");

    config.AddCommand<FileCommand>("file")
        .WithDescription("Displays the details of a file on the Pluck instance");

    config.AddCommand<GetCommand>("get")
        .WithDescription("Downloads a file/folder from a Pluck instance");

    config.AddCommand<KeyGenCommand>("key-gen")
        .WithDescription("Generates a new API key");

    config.AddCommand<ListCommand>("list")
        .WithDescription("Lists files on the Pluck instance");

    config.AddCommand<PurgeCommand>("purge")
        .WithDescription("Deletes a file from the Pluck instance ahead of expiration");

    config.AddCommand<RevokeUserCommand>("revoke-user")
        .WithDescription("Revokes a user from the Pluck instance");

    config.AddCommand<ShareCommand>("share")
        .WithDescription("Uploads a file/folder to the Pluck instance");

    config.AddCommand<UsersCommand>("users")
        .WithDescription("Lists all the users in the Pluck instance");
});

try
{
    return app.Run(args);
}
catch (Exception e)
{
    SpectreOutput.Error($"Unexpected error: {e.Message}");
    return 1;
}