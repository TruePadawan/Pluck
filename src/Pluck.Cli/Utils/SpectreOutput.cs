using Pluck.Shared.Dtos.Files;
using Pluck.Shared.Dtos.Users;
using Spectre.Console;

namespace Pluck.Cli.Utils;

/// <summary>
/// Centralized Spectre.Console output helpers for consistent CLI styling.
/// </summary>
public static class SpectreOutput
{
    // Messages
    public static void Success(string message)
    {
        AnsiConsole.MarkupLine($"[green]✓[/] {Markup.Escape(message)}");
    }

    public static void Error(string message)
    {
        AnsiConsole.MarkupLine($"[red]✗[/] {Markup.Escape(message)}");
    }

    public static void Info(string message)
    {
        AnsiConsole.MarkupLine($"[blue]ℹ[/] {Markup.Escape(message)}");
    }

    public static void Warn(string message)
    {
        AnsiConsole.MarkupLine($"[yellow]⚠[/] {Markup.Escape(message)}");
    }

    public static void Copied(string label)
    {
        AnsiConsole.MarkupLine($"[green]✓[/] {Markup.Escape(label)} copied to clipboard.");
    }

    // File Details Panel

    /// <summary>
    /// Renders a single file's details as a bordered panel with a grid.
    /// </summary>
    public static void FileDetail(FileResponseDto file)
    {
        var detailsGrid = new Grid();
        detailsGrid.AddColumn(new GridColumn().PadRight(2));
        detailsGrid.AddColumn();

        detailsGrid.AddRow("[bold]Token[/]", Markup.Escape(file.Token));
        detailsGrid.AddRow("[bold]Name[/]", Markup.Escape(file.OriginalFileName));
        detailsGrid.AddRow("[bold]Downloads Left[/]", file.DownloadsLeft?.ToString() ?? "Unlimited");
        detailsGrid.AddRow("[bold]Expires At[/]", Markup.Escape(file.ExpiresAt.ToString("g")));
        detailsGrid.AddRow("[bold]Download URL[/]", Markup.Escape(file.DownloadUrl));
        if (file.IsPasswordProtected)
        {
            detailsGrid.AddRow("[bold]Protected[/]", "[yellow]Password Protected[/]");
        }

        var fileDetailsPanel = new Panel(detailsGrid)
            .Header("[bold dodgerblue1]File Details[/]")
            .Border(BoxBorder.Rounded)
            .BorderStyle(new Style(Color.DodgerBlue1));

        var qrCodeMarkup = Utilities.GetQrCodeMarkup(file.DownloadUrl);
        var qrCodePanel = new Panel(qrCodeMarkup)
            .Header("[bold dodgerblue1]QR Code[/]")
            .Padding(0, 0, 0, 0)
            .Border(BoxBorder.Rounded)
            .BorderStyle(new Style(Color.DodgerBlue1));

        // Use a Grid so columns auto-size to content width
        var outerGrid = new Grid();
        outerGrid.AddColumn(new GridColumn().NoWrap().PadRight(1));
        outerGrid.AddColumn(new GridColumn().NoWrap());
        outerGrid.AddRow(fileDetailsPanel, qrCodePanel);

        AnsiConsole.Write(outerGrid);
    }

    // File Table

    /// <summary>
    /// Renders a list of files as a styled table. Shows an info message if empty.
    /// </summary>
    public static void FileTable(List<FileResponseDto> files)
    {
        if (files.Count == 0)
        {
            Info("No files found.");
            return;
        }

        var table = new Table()
            .Border(TableBorder.Rounded)
            .BorderStyle(new Style(Color.DodgerBlue1))
            .Title("[bold dodgerblue1]Files[/]");

        table.AddColumn(new TableColumn("[bold]Token[/]"));
        table.AddColumn(new TableColumn("[bold]Name[/]"));
        table.AddColumn(new TableColumn("[bold]Downloads Left[/]").Centered());
        table.AddColumn(new TableColumn("[bold]Expires At[/]"));
        table.AddColumn(new TableColumn("[bold]Protected[/]").Centered());
        table.AddColumn(new TableColumn("[bold]Download URL[/]"));

        foreach (var file in files)
        {
            table.AddRow(
                Markup.Escape(file.Token),
                Markup.Escape(file.OriginalFileName),
                file.DownloadsLeft?.ToString() ?? "Unlimited",
                Markup.Escape(file.ExpiresAt.ToString("g")),
                file.IsPasswordProtected ? "[yellow]Yes[/]" : "No",
                Markup.Escape(file.DownloadUrl));
        }

        AnsiConsole.Write(table);
    }

    // User Table
    public static void UserTable(List<UserResponseDto> users)
    {
        if (users.Count == 0)
        {
            Info("No users in the Pluck instance");
            return;
        }

        var table = new Table()
            .Border(TableBorder.Rounded)
            .BorderStyle(new Style(Color.DodgerBlue1))
            .Title("[bold dodgerblue1]Users[/]");

        table.AddColumn(new TableColumn("[bold]Name[/]"));
        table.AddColumn(new TableColumn("[bold]Role[/]"));

        foreach (var user in users)
        {
            table.AddRow(
                Markup.Escape(user.Name),
                Markup.Escape(user.Role));
        }

        AnsiConsole.Write(table);
    }

    // API Key Panel

    /// <summary>
    /// Renders an API key in a visually prominent panel.
    /// </summary>
    public static void ApiKeyPanel(string apiKey)
    {
        var panel = new Panel(Markup.Escape(apiKey))
            .Header("[bold yellow]API Key[/]")
            .Border(BoxBorder.Rounded)
            .BorderStyle(new Style(Color.Yellow));

        AnsiConsole.Write(panel);
    }
}