using Dapper;
using Microsoft.Data.Sqlite;
using Pluck.Api.Data;
using Pluck.Api.Endpoints;
using Pluck.Api.Middlewares;
using Pluck.Api.Repositories;
using Pluck.Api.Security;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddSingleton<DbConnectionFactory>();
builder.Services.AddScoped<UserRepository>();
builder.Services.AddScoped<FileRepository>();
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 10L * 1024 * 1024 * 1024; // 10GB
});
var app = builder.Build();
// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
// custom auth middleware
app.UseWhen(context => context.Request.Path.StartsWithSegments("/api"),
    appBuilder => appBuilder.UseMiddleware<ApiKeyAuthMiddleware>());

// Set up the database and seed admin user if necessary
using (var scope = app.Services.CreateScope())
{
    var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
    var dbFactory = scope.ServiceProvider.GetRequiredService<DbConnectionFactory>();

    var dbConnectionString = config.GetConnectionString("PluckDb");
    var stringBuilder = new SqliteConnectionStringBuilder(dbConnectionString);
    var dbDirectory = Path.GetDirectoryName(stringBuilder.DataSource);

    // Create the db directory if it doesn't exist
    if (!string.IsNullOrEmpty(dbDirectory) && !Directory.Exists(dbDirectory))
    {
        Directory.CreateDirectory(dbDirectory);
    }

    using var dbConnection = dbFactory.CreateConnection();
    dbConnection.Open();

    const string createTablesCommand = """
                                       CREATE TABLE IF NOT EXISTS Users (
                                           Id INTEGER PRIMARY KEY AUTOINCREMENT,
                                           Name TEXT NOT NULL,
                                           ApiKeyHash TEXT NOT NULL UNIQUE,
                                           Role TEXT NOT NULL DEFAULT 'User',
                                           CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP
                                       );

                                       CREATE TABLE IF NOT EXISTS Files (
                                           Token TEXT PRIMARY KEY,
                                           OwnerId INTEGER NOT NULL,
                                           DiskFileName TEXT NOT NULL,
                                           OriginalFileName TEXT NOT NULL,
                                           DownloadsLeft INTEGER,
                                           CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
                                           ExpiresAt DATETIME NOT NULL,
                                           FOREIGN KEY (OwnerId) REFERENCES Users(Id) ON DELETE CASCADE
                                       );
                                       """;
    dbConnection.Execute(createTablesCommand);

    // Seed the admin user if necessary
    var userCount = dbConnection.ExecuteScalar<int>("SELECT COUNT(*) FROM Users");
    if (userCount == 0)
    {
        var adminKey = config["PLUCK_ADMIN_KEY"];
        if (string.IsNullOrEmpty(adminKey))
        {
            throw new Exception("PLUCK_ADMIN_KEY env variable is not set");
        }

        var hashedKey = KeyHasher.ComputeHash(adminKey);
        dbConnection.Execute(
            "INSERT INTO Users (Name, ApiKeyHash, Role) VALUES (@Name, @Hash, @Role)",
            new { Name = "Admin", Hash = hashedKey, Role = "Admin" });
        app.Logger.LogInformation("Admin user created");
    }
}

app.MapGet("/", () => "Pluck");
app.MapUploadEndpoints();
app.Run();