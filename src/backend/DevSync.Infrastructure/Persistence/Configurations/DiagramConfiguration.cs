using DevSync.Domain.Diagrams;
using DevSync.Domain.Workspaces;
using DevSync.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DevSync.Infrastructure.Persistence.Configurations;

/// <summary>One row per diagram, its whole document in a jsonb column (spec 004 data-model.md, research R1).</summary>
internal sealed class DiagramConfiguration : IEntityTypeConfiguration<Diagram>
{
    public void Configure(EntityTypeBuilder<Diagram> builder)
    {
        builder.ToTable("diagrams", t =>
        {
            t.HasCheckConstraint("ck_diagrams_name", $"char_length(btrim(name)) BETWEEN 1 AND {DiagramLimits.MaxNameLength}");
            t.HasCheckConstraint("ck_diagrams_revision", "revision >= 1");
            t.HasCheckConstraint("ck_diagrams_schema_version", "schema_version >= 1");
            t.HasCheckConstraint("ck_diagrams_document", "jsonb_typeof(document) = 'object'");
            t.HasCheckConstraint("ck_diagrams_node_count", $"node_count BETWEEN 0 AND {DiagramLimits.MaxNodes}");
            t.HasCheckConstraint("ck_diagrams_edge_count", $"edge_count BETWEEN 0 AND {DiagramLimits.MaxEdges}");
        });

        builder.HasKey(d => d.Id).HasName("pk_diagrams");
        builder.Property(d => d.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(d => d.WorkspaceId).HasColumnName("workspace_id");
        builder.Property(d => d.Name).HasColumnName("name").HasMaxLength(DiagramLimits.MaxNameLength).IsRequired();
        builder.Property(d => d.NormalizedName).HasColumnName("normalized_name").HasMaxLength(DiagramLimits.MaxNameLength).IsRequired();
        builder.Property(d => d.Revision).HasColumnName("revision");
        builder.Property(d => d.SchemaVersion).HasColumnName("schema_version");
        builder.Property(d => d.Document).HasColumnName("document").HasColumnType("jsonb").IsRequired();
        builder.Property(d => d.NodeCount).HasColumnName("node_count");
        builder.Property(d => d.EdgeCount).HasColumnName("edge_count");
        builder.Property(d => d.CreatedAt).HasColumnName("created_at");
        builder.Property(d => d.UpdatedAt).HasColumnName("updated_at");
        builder.Property(d => d.CreatedByUserId).HasColumnName("created_by_user_id");
        builder.Property(d => d.UpdatedByUserId).HasColumnName("updated_by_user_id");

        builder.HasOne<Workspace>()
            .WithMany()
            .HasForeignKey(d => d.WorkspaceId)
            .HasConstraintName("fk_diagrams_workspace_id_workspaces")
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<AppUser>()
            .WithMany()
            .HasForeignKey(d => d.CreatedByUserId)
            .HasConstraintName("fk_diagrams_created_by_user_id_users")
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne<AppUser>()
            .WithMany()
            .HasForeignKey(d => d.UpdatedByUserId)
            .HasConstraintName("fk_diagrams_updated_by_user_id_users")
            .OnDelete(DeleteBehavior.SetNull);

        // Names are unique per workspace ignoring case (FR-004); the leading column also serves the list query.
        builder.HasIndex(d => new { d.WorkspaceId, d.NormalizedName }).IsUnique().HasDatabaseName("ux_diagrams_workspace_name");
        builder.HasIndex(d => d.CreatedByUserId).HasDatabaseName("ix_diagrams_created_by_user_id");
        builder.HasIndex(d => d.UpdatedByUserId).HasDatabaseName("ix_diagrams_updated_by_user_id");
    }
}
