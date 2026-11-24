# Repository Guidelines

## Project structure

Pluck is a self-hosted file-sharing service built with .NET 10. Work in the project that owns the behavior:

- `src/Pluck.Api/` contains HTTP endpoints, background workers, SQLite persistence, and Entity Framework Core migrations.
- `src/Pluck.Cli/` contains terminal commands such as `share` and `get`.
- `src/Pluck.Shared/` contains models, request and response types, and utilities used across projects.
- `src/Pluck.Tests/` contains unit, integration, and worker tests. Shared fixtures live under `TestInfrastructure/`.

Docker Compose files live at the repository root. Reverse proxy configuration lives in `nginx/`.

## Build and run

Run these commands from the repository root with the .NET 10 SDK installed:

- `dotnet build Pluck.slnx` restores dependencies and builds the solution.
- `dotnet run --project src/Pluck.Cli -- share --help` shows upload options.
- `dotnet run --project src/Pluck.Api` starts the API. Configure the admin key, upload directory, and SQLite connection first. Apply migrations with `dotnet ef database update --project src/Pluck.Api`.

## C# conventions

Use four-space indentation and follow the surrounding file’s namespace and brace style. Use PascalCase for types and methods and camelCase for parameters and local variables. Add a C# XML documentation comment (`///`) with a meaningful `<summary>` above every new class, method, and function, including local functions.

## Testing

Run `dotnet test Pluck.slnx` after every change and fix regressions before submitting. Use xUnit and follow the existing `Operation_Condition_ExpectedResult` naming pattern. Put endpoint and repository tests under `Integration/` and isolated behavior tests under `Unit/`.

## Commits and pull requests

Use a short, imperative commit subject. Recent examples include `refactor: stop applying migrations on app startup` and scoped subjects such as `tests(api): …`. In pull requests, describe the behavior change, link the relevant issue, and report test results. Call out any migration or configuration step reviewers need to reproduce the change.
