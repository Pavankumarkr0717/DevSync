using DevSync.Domain.Files;
using DevSync.Domain.Workspaces;
using DevSync.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DevSync.Infrastructure.Persistence.Configurations;

/// <summary>The folder/file tree as an adjacency list (spec 003 data-model.md, research R1).</summary>
internal sealed class WorkspaceItemConfiguration : IEntityTypeConfiguration<WorkspaceItem>
{
    public void Configure(EntityTypeBuilder<WorkspaceItem> builder)
    {
        builder.ToTable("workspace_items", t =>
        {
            t.HasCheckConstraint("ck_workspace_items_kind", "kind IN ('folder','file')");
            t.HasCheckConstraint("ck_workspace_items_name_length", $"char_length(name) BETWEEN 1 AND {ProjectLimits.MaxNameLength}");
        });

        builder.HasKey(i => i.Id).HasName("pk_workspace_items");
        builder.Property(i => i.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(i => i.WorkspaceId).HasColumnName("workspace_id");
        builder.Property(i => i.ParentId).HasColumnName("parent_id");
        builder.Property(i => i.Kind)
            .HasColumnName("kind")
            .HasMaxLength(8)
            .HasConversion(k => EnumNames.ToDb(k), v => EnumNames.ItemKindFromDb(v));
        builder.Property(i => i.Name).HasColumnName("name").HasMaxLength(ProjectLimits.MaxNameLength).IsRequired();
        builder.Property(i => i.NormalizedName).HasColumnName("normalized_name").HasMaxLength(ProjectLimits.MaxNameLength).IsRequired();
        builder.Property(i => i.CreatedAt).HasColumnName("created_at");
        builder.Property(i => i.UpdatedAt).HasColumnName("updated_at");
        builder.Property(i => i.CreatedByUserId).HasColumnName("created_by_user_id");

        builder.HasOne<Workspace>()
            .WithMany()
            .HasForeignKey(i => i.WorkspaceId)
            .HasConstraintName("fk_workspace_items_workspace_id_workspaces")
            .OnDelete(DeleteBehavior.Cascade);

        // Deleting a folder deletes its subtree in the same statement (FR-015).
        builder.HasOne<WorkspaceItem>()
            .WithMany()
            .HasForeignKey(i => i.ParentId)
            .HasConstraintName("fk_workspace_items_parent_id_workspace_items")
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<AppUser>()
            .WithMany()
            .HasForeignKey(i => i.CreatedByUserId)
            .HasConstraintName("fk_workspace_items_created_by_user_id_users")
            .OnDelete(DeleteBehavior.SetNull);

        // Sibling names are unique ignoring case, including at the top level (FR-004). Also serves
        // "load the whole tree of a workspace" through its leading column.
        builder.HasIndex(i => new { i.WorkspaceId, i.ParentId, i.NormalizedName })
            .IsUnique()
            .AreNullsDistinct(false)
            .HasDatabaseName("ux_workspace_items_sibling_name");

        builder.HasIndex(i => i.ParentId).HasDatabaseName("ix_workspace_items_parent_id");
        builder.HasIndex(i => i.CreatedByUserId).HasDatabaseName("ix_workspace_items_created_by_user_id");
    }
}
