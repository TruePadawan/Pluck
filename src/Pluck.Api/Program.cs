using Asp.Versioning;
using Microsoft.EntityFrameworkCore;
using Pluck.Api.Endpoints;
using Pluck.Api.Middlewares;
using Pluck.Api.Persistence;
using Pluck.Api.Repositories;
using Pluck.Api.Security;
using Pluck.Api.Workers;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// builder.Services.AddOpenApi();
builder.Services.AddScoped<UserRepository>();
builder.Services.AddScoped<FileRepository>();
builder.Services.AddHostedService<FileCleanupBackgroundService>();
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 10L * 1024 * 1024 * 1024; // 10GB
});
// An exception thrown in the background service will not cause the host to exit
builder.Services.Configure<HostOptions>(options =>
{
    options.BackgroundServiceExceptionBehavior = BackgroundServiceExceptionBehavior.Ignore;
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
builder.Services.AddApiVersioning(options =>
    {
        options.DefaultApiVersion = new ApiVersion(1, 0);
        // Use header and query string to determine the API version
        options.ApiVersionReader = ApiVersionReader.Combine(new HeaderApiVersionReader("X-API-VERSION"),
            new QueryStringApiVersionReader("api-version"));
        options.ReportApiVersions = true;
        options.AssumeDefaultVersionWhenUnspecified = true;
    })
    .AddApiExplorer(options =>
    {
        options.GroupNameFormat = "'v'V";
        options.SubstituteApiVersionInUrl = true;
    }).AddOpenApi();

var app = builder.Build();
// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().WithDocumentPerVersion();
    // MapScalarApiReference sets up the Scalar UI at /scalar
    // AddDocuments registers all known API versions so Scalar shows a dropdown to switch between them
    app.MapScalarApiReference(options =>
    {
        var descriptions = app.DescribeApiVersions();
        for (var i = 0; i < descriptions.Count; i++)
        {
            var description = descriptions[i];
            var isDefault = i == descriptions.Count - 1;

            // isDefault is used to mark the default API version in Scalar.
            // This decides which version is selected by default when users visit the Scalar UI.
            options.AddDocument(description.GroupName, description.GroupName, isDefault: isDefault);
        }
    });
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

app.MapMiscEndpoints();
app.MapUploadEndpoints();
app.MapDownloadEndpoints();
app.MapAdminEndpoints();
app.MapFileEndpoints();
app.Run();