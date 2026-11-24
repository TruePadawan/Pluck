using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pluck.Shared.Models;

namespace Pluck.Api.Persistence.Configurations;

/// <summary>Configures persistence for queued webhook notifications.</summary>
public sealed class WebhookDeliveryConfiguration : IEntityTypeConfiguration<WebhookDelivery>
{
    /// <summary>Indexes delivery status and next attempt time for the worker’s due-delivery query.</summary>
    public void Configure(EntityTypeBuilder<WebhookDelivery> builder)
    {
        builder.HasIndex(delivery => new { delivery.Status, delivery.NextAttemptAt });
    }
}
