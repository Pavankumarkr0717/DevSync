using DevSync.Domain.Identity;
using DevSync.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DevSync.Infrastructure.Persistence.Configurations;

internal sealed class SessionConfiguration : IEntityTypeConfiguration<Session>
{
    public void Configure(EntityTypeBuilder<Session> builder)
    {
        builder.ToTable("sessions", t =>
            t.HasCheckConstraint("ck_sessions_revoked_reason", "revoked_reason IN ('logout','logout_all','refresh_token_reuse')"));

        builder.HasKey(s => s.Id).HasName("pk_sessions");
        builder.Property(s => s.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(s => s.UserId).HasColumnName("user_id");
        builder.Property(s => s.CreatedAt).HasColumnName("created_at");
        builder.Property(s => s.LastRefreshedAt).HasColumnName("last_refreshed_at");
        builder.Property(s => s.IdleExpiresAt).HasColumnName("idle_expires_at");
        builder.Property(s => s.AbsoluteExpiresAt).HasColumnName("absolute_expires_at");
        builder.Property(s => s.RevokedAt).HasColumnName("revoked_at");
        builder.Property(s => s.RevokedReason)
            .HasColumnName("revoked_reason")
            .HasMaxLength(32)
            .HasConversion(r => EnumNames.ToDb(r!.Value), v => EnumNames.RevocationFromDb(v));

        builder.HasOne<AppUser>()
            .WithMany()
            .HasForeignKey(s => s.UserId)
            .HasConstraintName("fk_sessions_user_id_users")
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(s => s.UserId)
            .HasDatabaseName("ix_sessions_user_id_active")
            .HasFilter("revoked_at IS NULL");
    }
}

internal sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("refresh_tokens", t =>
            t.HasCheckConstraint("ck_refresh_tokens_token_hash_length", $"octet_length(token_hash) = {RefreshToken.HashLength}"));

        builder.HasKey(t => t.Id).HasName("pk_refresh_tokens");
        builder.Property(t => t.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(t => t.SessionId).HasColumnName("session_id");
        builder.Property(t => t.TokenHash).HasColumnName("token_hash").IsRequired();
        builder.Property(t => t.IssuedAt).HasColumnName("issued_at");
        builder.Property(t => t.UsedAt).HasColumnName("used_at");
        builder.Property(t => t.ReplacedById).HasColumnName("replaced_by_id");

        builder.HasOne<Session>()
            .WithMany()
            .HasForeignKey(t => t.SessionId)
            .HasConstraintName("fk_refresh_tokens_session_id_sessions")
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<RefreshToken>()
            .WithMany()
            .HasForeignKey(t => t.ReplacedById)
            .HasConstraintName("fk_refresh_tokens_replaced_by_id_refresh_tokens")
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(t => t.TokenHash).IsUnique().HasDatabaseName("ux_refresh_tokens_token_hash");
        builder.HasIndex(t => t.SessionId).HasDatabaseName("ix_refresh_tokens_session_id");
        builder.HasIndex(t => t.ReplacedById).HasDatabaseName("ix_refresh_tokens_replaced_by_id");
    }
}
