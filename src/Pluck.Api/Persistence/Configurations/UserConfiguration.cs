using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pluck.Shared.Models;

namespace Pluck.Api.Persistence.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");

        builder.HasKey(u => u.Id);
        builder.Property(u => u.Name).IsRequired().HasMaxLength(100);
        builder.Property(u => u.ApiKeyHash).IsRequired().HasMaxLength(100);
        builder.Property(u => u.Role).IsRequired().HasMaxLength(50);
        builder.Property(m => m.CreatedAt)
            .IsRequired()
            .ValueGeneratedOnAdd();
        builder.Property(m => m.LastModifiedAt)
            .IsRequired()
            .ValueGeneratedOnUpdate();

        builder.HasIndex(u => u.ApiKeyHash).IsUnique();
        builder.HasIndex(u => u.Name).IsUnique();
    }
}