using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WebApi.Database.Models;

namespace WebApi.Database.Configurations;

public class TodoConfiguration : IEntityTypeConfiguration<Todo>
{
    public void Configure(EntityTypeBuilder<Todo> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasConversion(id => id.Value, v => new TodoId(v));
        builder.Property(e => e.Title).HasMaxLength(500);
        builder.Property(e => e.OwnerUserId)
            .HasConversion(id => id.Value, v => new UserId(v))
            .IsRequired();
        builder.HasOne(e => e.Owner)
            .WithMany(u => u.Todos)
            .HasForeignKey(e => e.OwnerUserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}