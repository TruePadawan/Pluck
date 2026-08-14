using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Pluck.Api.Security;
using Pluck.Shared.Models;
using File = Pluck.Shared.Models.File;

namespace Pluck.Api.Persistence;

public class AppDbContext : DbContext
{
    private IOptions<PluckApiOptions> _options;

    public AppDbContext(DbContextOptions<AppDbContext> dbContextOptions, IOptions<PluckApiOptions> options)
        : base(dbContextOptions)
    {
        _options = options;
    }

    public DbSet<File> Files => Set<File>();
    public DbSet<User> Users => Set<User>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        // Seed default admin user if necessary
        var config = _options.Value;
        optionsBuilder.UseAsyncSeeding(async (db, _, cancellationToken) =>
            {
                var adminUser =
                    await db.Set<User>().FirstOrDefaultAsync(user => user.Role == "Admin", cancellationToken);
                if (adminUser is null)
                {
                    var hashedKey = KeyHasher.ComputeHash(config.AdminKey);
                    adminUser = User.Create(new UserParams
                    {
                        Name = "Admin",
                        ApiKeyHash = hashedKey,
                        Role = "Admin"
                    });
                    await db.Set<User>().AddAsync(adminUser, cancellationToken);
                    await db.SaveChangesAsync(cancellationToken);
                }
            })
            .UseSeeding((db, _) =>
            {
                var adminUser = db.Set<User>().FirstOrDefault(user => user.Role == "Admin");
                if (adminUser is null)
                {
                    var hashedKey = KeyHasher.ComputeHash(config.AdminKey);
                    adminUser = User.Create(new UserParams
                    {
                        Name = "Admin",
                        ApiKeyHash = hashedKey,
                        Role = "Admin"
                    });
                    db.Set<User>().Add(adminUser);
                    db.SaveChangesAsync();
                }
            });
        base.OnConfiguring(optionsBuilder);
    }
}