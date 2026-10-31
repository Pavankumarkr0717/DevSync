using DevSync.Domain.Files;
using DevSync.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DevSync.Infrastructure.Persistence.Configurations;

/// <summary>One row per file: its text and revision (spec 003 data-model.md, research R3/R4).</summary>
internal sealed class FileContentConfiguration : IEntityTypeConfiguration<FileContent>
{
    public void Configure(EntityTypeBuilder<FileContent> builder)
    {
        builder.ToTable("file_contents", t =>
        {
            t.HasCheckConstraint(
                "ck_file_contents_size",
                $"size_bytes BETWEEN 0 AND {ProjectLimits.MaxContentBytes} AND size_bytes = octet_length(content)");
            t.HasCheckConstraint("ck_file_contents_revision", "revision >= 1");
        });

        builder.HasKey(c => c.ItemId).HasName("pk_file_contents");
        builder.Property(c => c.ItemId).HasColumnName("item_id").ValueGeneratedNever();
        builder.Property(c => c.Content).HasColumnName("content").HasColumnType("text").HasDefaultValue(string.Empty).IsRequired();
        builder.Property(c => c.SizeBytes).HasColumnName("size_bytes");
        builder.Property(c => c.Revision).HasColumnName("revision");
        builder.Property(c => c.UpdatedAt).HasColumnName("updated_at");
        builder.Property(c => c.UpdatedByUserId).HasColumnName("updated_by_user_id");

        builder.HasOne<WorkspaceItem>()
            .WithOne()
            .HasForeignKey<FileContent>(c => c.ItemId)
            .HasConstraintName("fk_file_contents_item_id_workspace_items")
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<AppUser>()
            .WithMany()
            .HasForeignKey(c => c.UpdatedByUserId)
            .HasConstraintName("fk_file_contents_updated_by_user_id_users")
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(c => c.UpdatedByUserId).HasDatabaseName("ix_file_contents_updated_by_user_id");
    }
}
