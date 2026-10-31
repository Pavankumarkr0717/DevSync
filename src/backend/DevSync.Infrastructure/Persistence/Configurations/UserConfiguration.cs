using DevSync.Domain.Common;
using DevSync.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DevSync.Infrastructure.Persistence.Configurations;

/// <summary>Maps the Identity user onto the 001 <c>users</c> table (see 002 data-model.md).</summary>
internal sealed class UserConfiguration : IEntityTypeConfiguration<AppUser>
{
    public void Configure(EntityTypeBuilder<AppUser> builder)
    {
        builder.ToTable("users");

        builder.HasKey(u => u.Id).HasName("pk_users");
        builder.Property(u => u.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(u => u.DisplayName).HasColumnName("display_name").HasMaxLength(AppUser.DisplayNameMaxLength).IsRequired();
        builder.Property(u => u.CreatedAt).HasColumnName("created_at").IsRequired();

        builder.Property(u => u.UserName).HasColumnName("user_name").HasMaxLength(256).IsRequired();
        builder.Property(u => u.NormalizedUserName).HasColumnName("normalized_user_name").HasMaxLength(256).IsRequired();
        builder.Property(u => u.Email).HasColumnName("email").HasMaxLength(EmailAddress.MaxLength).IsRequired();
        builder.Property(u => u.NormalizedEmail).HasColumnName("normalized_email").HasMaxLength(EmailAddress.MaxLength).IsRequired();
        builder.Property(u => u.EmailConfirmed).HasColumnName("email_confirmed").HasDefaultValue(false);
        builder.Property(u => u.PasswordHash).HasColumnName("password_hash").IsRequired();
        builder.Property(u => u.SecurityStamp).HasColumnName("security_stamp").IsRequired();
        builder.Property(u => u.ConcurrencyStamp).HasColumnName("concurrency_stamp").IsRequired().IsConcurrencyToken();
        builder.Property(u => u.LockoutEnabled).HasColumnName("lockout_enabled").HasDefaultValue(false);
        builder.Property(u => u.LockoutEnd).HasColumnName("lockout_end");
        builder.Property(u => u.AccessFailedCount).HasColumnName("access_failed_count").HasDefaultValue(0);

        builder.Ignore(u => u.PhoneNumber);
        builder.Ignore(u => u.PhoneNumberConfirmed);
        builder.Ignore(u => u.TwoFactorEnabled);

        builder.HasIndex(u => u.NormalizedUserName).IsUnique().HasDatabaseName("ux_users_normalized_user_name");
        builder.HasIndex(u => u.NormalizedEmail).IsUnique().HasDatabaseName("ux_users_normalized_email");
    }
}
