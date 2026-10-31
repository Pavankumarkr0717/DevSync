using DevSync.Domain.Workspaces;
using DevSync.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DevSync.Infrastructure.Persistence.Configurations;

internal sealed class WorkspaceMembershipConfiguration : IEntityTypeConfiguration<WorkspaceMembership>
{
    public void Configure(EntityTypeBuilder<WorkspaceMembership> builder)
    {
        builder.ToTable("workspace_memberships", t =>
            t.HasCheckConstraint("ck_workspace_memberships_role", "role IN ('owner','editor','viewer')"));

        builder.HasKey(m => new { m.WorkspaceId, m.UserId }).HasName("pk_workspace_memberships");
        builder.Property(m => m.WorkspaceId).HasColumnName("workspace_id");
        builder.Property(m => m.UserId).HasColumnName("user_id");
        builder.Property(m => m.Role)
            .HasColumnName("role")
            .HasMaxLength(20)
            .HasConversion(role => RoleNames.ToDb(role), value => RoleNames.FromDb(value))
            .IsRequired();
        builder.Property(m => m.JoinedAt).HasColumnName("joined_at").IsRequired();

        builder.HasOne<AppUser>()
            .WithMany()
            .HasForeignKey(m => m.UserId)
            .HasConstraintName("fk_workspace_memberships_user_id_users")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(m => m.UserId).HasDatabaseName("ix_workspace_memberships_user_id");

        // Exactly one owner per workspace (spec 002 FR-021/FR-023).
        builder.HasIndex(m => m.WorkspaceId)
            .IsUnique()
            .HasFilter("role = 'owner'")
            .HasDatabaseName("ux_workspace_memberships_one_owner");
    }
}
