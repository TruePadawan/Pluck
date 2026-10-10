using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pluck.Shared.Models.Events;

namespace Pluck.Api.Persistence.Configurations;

public class FileDownloadEventsConfiguration : IEntityTypeConfiguration<FileDownloadEvents>
{
    public void Configure(EntityTypeBuilder<FileDownloadEvents> builder)
    {
        builder.ToTable("FileDownloadEvents");

        builder.HasKey(e => e.Id);
        
        builder.Property(e => e.HashedIp).IsRequired().HasMaxLength(256);
        builder.Property(e => e.City).HasMaxLength(100);
        builder.Property(e => e.Country).HasMaxLength(100);
        
        builder.Property(m => m.CreatedAt)
            .IsRequired()
            .ValueGeneratedOnAdd();
            
        builder.Property(m => m.LastModifiedAt)
            .IsRequired()
            .ValueGeneratedOnUpdate();

        builder.HasOne(e => e.File)
            .WithMany(f => f.DownloadEvents)
            .HasForeignKey(e => e.FileId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);
    }
}
