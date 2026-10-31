using DevSync.Domain.Common;
using DevSync.Domain.Workspaces;
using DevSync.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DevSync.Infrastructure.Persistence.Configurations;

internal sealed class WorkspaceInvitationConfiguration : IEntityTypeConfiguration<Invitation>
{
    public void Configure(EntityTypeBuilder<Invitation> builder)
    {
        builder.ToTable("workspace_invitations", t =>
        {
            t.HasCheckConstraint("ck_workspace_invitations_role", "role IN ('editor','viewer')");
            t.HasCheckConstraint("ck_workspace_invitations_status", "status IN ('pending','accepted','declined','revoked','expired')");
            t.HasCheckConstraint("ck_workspace_invitations_expiry", "expires_at > created_at");
        });

        builder.HasKey(i => i.Id).HasName("pk_workspace_invitations");
        builder.Property(i => i.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(i => i.WorkspaceId).HasColumnName("workspace_id");
        builder.Property(i => i.Email).HasColumnName("email").HasMaxLength(EmailAddress.MaxLength).IsRequired();
        builder.Property(i => i.NormalizedEmail).HasColumnName("normalized_email").HasMaxLength(EmailAddress.MaxLength).IsRequired();
        builder.Property(i => i.Role)
            .HasColumnName("role")
            .HasMaxLength(20)
            .HasConversion(r => RoleNames.ToDb(r), v => RoleNames.FromDb(v));
        builder.Property(i => i.InvitedByUserId).HasColumnName("invited_by_user_id");
        builder.Property(i => i.Status)
            .HasColumnName("status")
            .HasMaxLength(16)
            .HasConversion(s => EnumNames.ToDb(s), v => EnumNames.InvitationStatusFromDb(v));
        builder.Property(i => i.CreatedAt).HasColumnName("created_at");
        builder.Property(i => i.ExpiresAt).HasColumnName("expires_at");
        builder.Property(i => i.RespondedAt).HasColumnName("responded_at");
        builder.Property(i => i.AcceptedUserId).HasColumnName("accepted_user_id");

        builder.HasOne<Workspace>()
            .WithMany()
            .HasForeignKey(i => i.WorkspaceId)
            .HasConstraintName("fk_workspace_invitations_workspace_id_workspaces")
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<AppUser>()
            .WithMany()
            .HasForeignKey(i => i.InvitedByUserId)
            .HasConstraintName("fk_workspace_invitations_invited_by_user_id_users")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<AppUser>()
            .WithMany()
            .HasForeignKey(i => i.AcceptedUserId)
            .HasConstraintName("fk_workspace_invitations_accepted_user_id_users")
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(i => new { i.WorkspaceId, i.NormalizedEmail })
            .IsUnique()
            .HasFilter("status = 'pending'")
            .HasDatabaseName("ux_workspace_invitations_pending");

        builder.HasIndex(i => i.NormalizedEmail)
            .HasFilter("status = 'pending'")
            .HasDatabaseName("ix_workspace_invitations_pending_email");

        // Foreign-key lookups (cascades and restrict checks).
        builder.HasIndex(i => i.WorkspaceId).HasDatabaseName("ix_workspace_invitations_workspace_id");
        builder.HasIndex(i => i.InvitedByUserId).HasDatabaseName("ix_workspace_invitations_invited_by_user_id");
        builder.HasIndex(i => i.AcceptedUserId).HasDatabaseName("ix_workspace_invitations_accepted_user_id");
    }
}
