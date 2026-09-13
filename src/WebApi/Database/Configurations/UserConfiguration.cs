using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WebApi.Database.Models;

namespace WebApi.Database.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasConversion(id => id.Value, v => new UserId(v));
        builder.Property(e => e.Email).HasMaxLength(256).IsRequired();
        builder.Property(e => e.DisplayName).HasMaxLength(256);
        builder.Property(e => e.GivenName).HasMaxLength(256);
        builder.Property(e => e.FamilyName).HasMaxLength(256);
        builder.Property(e => e.AvatarUrl).HasMaxLength(2048);
        builder.HasIndex(e => e.Email).IsUnique();
    }
}
