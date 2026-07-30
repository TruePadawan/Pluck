using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using File = Pluck.Shared.Models.File;

namespace Pluck.Api.Persistence.Configurations;

public class FileConfiguration : IEntityTypeConfiguration<File>
{
    public void Configure(EntityTypeBuilder<File> builder)
    {
        builder.ToTable("Files");

        builder.HasKey(f => f.Id);
        builder.Property(f => f.Token).IsRequired().HasMaxLength(10);
        builder.Property(f => f.DiskFileName).IsRequired().HasMaxLength(200);
        builder.Property(f => f.OriginalFileName).IsRequired().HasMaxLength(200);
        builder.Property(f => f.ContentType).IsRequired().HasMaxLength(100);
        builder.Property(m => m.CreatedAt)
            .IsRequired()
            .ValueGeneratedOnAdd();
        builder.Property(m => m.LastModifiedAt)
            .IsRequired()
            .ValueGeneratedOnUpdate();

        builder.HasIndex(f => f.Token).IsUnique();

        // Can't delete a user if they have files
        builder.HasOne(f => f.Owner)
            .WithMany(u => u.Files)
            .HasForeignKey(f => f.OwnerId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}