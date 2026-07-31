using Microsoft.EntityFrameworkCore;
using Pluck.Api.Endpoints;
using Pluck.Api.Middlewares;
using Pluck.Api.Persistence;
using Pluck.Api.Repositories;
using Pluck.Api.Security;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddScoped<UserRepository>();
builder.Services.AddScoped<FileRepository>();
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 10L * 1024 * 1024 * 1024; // 10GB
});
builder.Services.AddDbContext<AppDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    options.UseSqlite(connectionString);
});

// Ensure environment variables are set and loaded
builder.Services.AddOptions<PluckApiOptions>()
    .Bind(builder.Configuration.GetSection(PluckApiOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();
var app = builder.Build();
// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseHttpsRedirection();
// custom auth middleware
app.UseWhen(context => context.Request.Path.StartsWithSegments("/api"),
    appBuilder => appBuilder.UseMiddleware<ApiKeyAuthMiddleware>());

// Set up the database and seed admin user if necessary
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    // Ensure the DB directory exists before EF tries to create the file
    var dbPath = db.Database.GetDbConnection().Database;
    var dbDirectory = Path.GetDirectoryName(dbPath);
    if (!string.IsNullOrEmpty(dbDirectory) && !Directory.Exists(dbDirectory))
    {
        Directory.CreateDirectory(dbDirectory);
    }

    await db.Database.MigrateAsync();
}

app.MapUploadEndpoints();
app.MapDownloadEndpoints();
app.MapAdminEndpoints();
app.Run();