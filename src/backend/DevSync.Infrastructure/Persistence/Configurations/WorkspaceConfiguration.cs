using DevSync.Domain.Workspaces;
using DevSync.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DevSync.Infrastructure.Persistence.Configurations;

internal sealed class WorkspaceConfiguration : IEntityTypeConfiguration<Workspace>
{
    public void Configure(EntityTypeBuilder<Workspace> builder)
    {
        builder.ToTable("workspaces", t =>
            t.HasCheckConstraint("ck_workspaces_name_not_blank", "char_length(btrim(name)) > 0"));

        builder.HasKey(w => w.Id).HasName("pk_workspaces");
        builder.Property(w => w.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(w => w.Name).HasColumnName("name").HasMaxLength(WorkspaceName.MaxLength).IsRequired();
        builder.Property(w => w.Description).HasColumnName("description").HasMaxLength(WorkspaceDescription.MaxLength);
        builder.Property(w => w.OwnerId).HasColumnName("owner_id").IsRequired();
        builder.Property(w => w.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(w => w.UpdatedAt).HasColumnName("updated_at").IsRequired();

        builder.HasOne<AppUser>()
            .WithMany()
            .HasForeignKey(w => w.OwnerId)
            .HasConstraintName("fk_workspaces_owner_id_users")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(w => w.OwnerId).HasDatabaseName("ix_workspaces_owner_id");

        builder.HasMany(w => w.Members)
            .WithOne()
            .HasForeignKey(m => m.WorkspaceId)
            .HasConstraintName("fk_workspace_memberships_workspace_id_workspaces")
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(w => w.Members).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
