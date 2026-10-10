using System.Net;
using System.Net.Http.Json;
using Spectre.Console;
using Spectre.Console.Cli;
using Pluck.Cli.Config;
using Pluck.Cli.Utils;
using Pluck.Shared.Dtos;
using Pluck.Shared.Dtos.Files;
using Pluck.Shared.Dtos.Users;

namespace Pluck.Cli.Commands;

public class AdminCommandSettings : CommandSettings
{
}

public class AdminCommand : AsyncCommand<AdminCommandSettings>
{
    private static readonly HttpClient PluckHttpClient = new();

    public override async Task<int> ExecuteAsync(CommandContext context, AdminCommandSettings settings,
        CancellationToken cancellationToken)
    {
        try
        {
            var pluckConfig = PluckConfigManager.GetConfigOrThrow();
            PluckHttpClient.BaseAddress = new Uri(pluckConfig.ServerUrl);
            PluckHttpClient.DefaultRequestHeaders.Add("X-PLUCK-API-KEY", pluckConfig.ApiUrl);

            while (true)
            {
                AnsiConsole.Clear();
                var action = AnsiConsole.Prompt(
                    new SelectionPrompt<string>()
                        .Title("[dodgerblue1]Admin Dashboard[/] - Select a module:")
                        .PageSize(10)
                        .AddChoices("User Management", "File Analytics", "Storage Graphs", "Exit"));

                if (action == "Exit") break;

                switch (action)
                {
                    case "User Management":
                        await HandleUserManagement();
                        break;
                    case "File Analytics":
                        await HandleFileAnalytics();
                        break;
                    case "Storage Graphs":
                        await HandleStorageGraphs();
                        break;
                }
            }

            return 0;
        }
        catch (Exception e)
        {
            SpectreOutput.Error($"Admin dashboard error: {e.Message}");
            return 1;
        }
        finally
        {
            PluckHttpClient.Dispose();
        }
    }

    private static async Task HandleUserManagement()
    {
        while (true)
        {
            AnsiConsole.Clear();
            var action = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("[dodgerblue1]User Management[/] - Select an action:")
                    .PageSize(10)
                    .AddChoices(new[]
                    {
                        "List Users",
                        "Create User",
                        "Delete User",
                        "Back"
                    }));

            if (action == "Back") break;

            try
            {
                if (action == "List Users")
                {
                    var response = await PluckHttpClient.GetAsync("/api/admin/users");
                    if (!response.IsSuccessStatusCode) throw new Exception("Failed to list users");
                    var users = await response.Content.ReadFromJsonAsync<List<UserResponseDto>>();
                    SpectreOutput.UserTable(users ?? new());
                }
                else if (action == "Create User")
                {
                    var name = AnsiConsole.Prompt(
                        new TextPrompt<string>("Enter [green]new user name[/] (leave empty to cancel):")
                            .AllowEmpty());

                    if (string.IsNullOrWhiteSpace(name)) continue;

                    var response =
                        await PluckHttpClient.PostAsync($"/api/admin/users?name={Uri.EscapeDataString(name)}", null);
                    if (response.IsSuccessStatusCode)
                    {
                        var createdUser = await response.Content.ReadFromJsonAsync<CreateUserResponseDto>();
                        if (createdUser != null)
                        {
                            SpectreOutput.Success($"User '{createdUser.Name}' created.");
                            SpectreOutput.ApiKeyPanel(createdUser.ApiKey);
                        }
                    }
                    else
                    {
                        var error = await response.Content.ReadFromJsonAsync<ErrorResponseDto>();
                        SpectreOutput.Error(error?.Error ?? "Failed to create user.");
                    }
                }
                else if (action == "Delete User")
                {
                    var response = await PluckHttpClient.GetAsync("/api/admin/users");
                    if (!response.IsSuccessStatusCode) throw new Exception("Failed to list users");
                    var users = await response.Content.ReadFromJsonAsync<List<UserResponseDto>>();

                    if (users == null || users.Count == 0)
                    {
                        SpectreOutput.Info("No users found.");
                        continue;
                    }

                    var userToDelete = AnsiConsole.Prompt(
                        new SelectionPrompt<string>()
                            .Title("Select a user to [red]delete[/]:")
                            .PageSize(10)
                            .AddChoices(users.Select(u => u.Name).Concat(new[] { "Cancel" })));

                    if (userToDelete == "Cancel") continue;

                    if (AnsiConsole.Confirm($"Are you sure you want to delete [red]{userToDelete}[/]?", false))
                    {
                        var delResponse =
                            await PluckHttpClient.DeleteAsync($"/api/admin/users/{Uri.EscapeDataString(userToDelete)}");
                        if (delResponse.IsSuccessStatusCode)
                        {
                            SpectreOutput.Success($"User '{userToDelete}' deleted.");
                        }
                        else
                        {
                            var error = await delResponse.Content.ReadFromJsonAsync<ErrorResponseDto>();
                            SpectreOutput.Error(error?.Error ?? "Failed to delete user.");
                        }
                    }
                }
            }
            catch (Exception e)
            {
                SpectreOutput.Error(e.Message);
            }

            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[grey]Press any key to continue...[/]");
            Console.ReadKey(true);
        }
    }

    private static async Task HandleFileAnalytics()
    {
        AnsiConsole.Clear();
        // Ask for file token
        var token = AnsiConsole.Prompt(
            new TextPrompt<string>("Enter the [green]file token[/] to view analytics (leave empty to cancel):")
                .AllowEmpty());
        if (string.IsNullOrWhiteSpace(token)) return;

        await AnsiConsole.Status().StartAsync("Fetching analytics...", async _ =>
        {
            try
            {
                var response = await PluckHttpClient.GetAsync($"/api/files/{Uri.EscapeDataString(token)}/analytics");
                if (!response.IsSuccessStatusCode)
                {
                    if (response.StatusCode == HttpStatusCode.NotFound)
                        throw new Exception("File not found.");

                    var error = await response.Content.ReadFromJsonAsync<ErrorResponseDto>();
                    throw new Exception(error?.Error ?? "Failed to fetch analytics.");
                }

                var analytics = await response.Content.ReadFromJsonAsync<FileAnalyticsDto>();
                if (analytics != null)
                {
                    SpectreOutput.FileAnalytics(analytics);
                }
            }
            catch (Exception e)
            {
                SpectreOutput.Error(e.Message);
            }
        });

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[grey]Press any key to return to main menu...[/]");
        Console.ReadKey(true);
    }

    private static async Task HandleStorageGraphs()
    {
        AnsiConsole.Clear();
        await AnsiConsole.Status().StartAsync("Fetching instance storage data...", async _ =>
        {
            try
            {
                var response = await PluckHttpClient.GetAsync("/api/files");
                if (!response.IsSuccessStatusCode) throw new Exception("Failed to fetch files.");
                var files = await response.Content.ReadFromJsonAsync<List<FileResponseDto>>();
                if (files == null || files.Count == 0)
                {
                    SpectreOutput.Info("No files on this instance.");
                    return;
                }

                long totalStorage = files.Sum(f => f.Size);
                long activeStorage = files
                    .Where(f => f.ExpiresAt > DateTime.UtcNow && (f.DownloadsLeft == null || f.DownloadsLeft > 0))
                    .Sum(f => f.Size);
                long expiredStorage = totalStorage - activeStorage;

                var grid = new Grid();
                grid.AddColumn(new GridColumn().PadRight(2));
                grid.AddColumn();
                grid.AddRow($"[bold]Total Storage Used[/]", Utilities.FormatBytes(totalStorage));
                grid.AddRow($"[bold]Total Files[/]", files.Count.ToString());
                grid.AddRow(new Text(""));

                var storageBreakdown = new BreakdownChart()
                    .Width(60)
                    .AddItem("Active", activeStorage, Color.Green)
                    .AddItem("Expired / Depleted", expiredStorage, Color.Red);

                grid.AddRow(new Markup("[bold]Storage Status[/]"), storageBreakdown);

                var panel = new Panel(grid)
                    .Header("[bold dodgerblue1]Instance Storage[/]")
                    .Border(BoxBorder.Rounded)
                    .BorderStyle(new Style(Color.DodgerBlue1));

                AnsiConsole.Write(panel);
            }
            catch (Exception e)
            {
                SpectreOutput.Error(e.Message);
            }
        });

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[grey]Press any key to return to main menu...[/]");
        Console.ReadKey(true);
    }
}