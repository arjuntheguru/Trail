using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Trail.Domain.Identity;
using Trail.Domain.Organizations;
using Trail.Infrastructure.Persistence.Conventions;

namespace Trail.Infrastructure.Persistence.Configurations;

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");
        builder.Property(x => x.Email).HasMaxLength(User.EmailMaxLength);
        builder.Property(x => x.NormalizedEmail).HasMaxLength(User.EmailMaxLength);
        builder.Property(x => x.FirstName).HasMaxLength(User.NameMaxLength);
        builder.Property(x => x.LastName).HasMaxLength(User.NameMaxLength);
        builder.Property(x => x.PasswordHash).HasMaxLength(256);
        builder.Ignore(x => x.FullName);
        builder.HasIndex(x => x.NormalizedEmail).IsUnique();
    }
}

internal sealed class MembershipConfiguration : IEntityTypeConfiguration<Membership>
{
    public void Configure(EntityTypeBuilder<Membership> builder)
    {
        builder.ToTable("memberships");
        builder.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Role).WithMany().HasForeignKey(x => x.RoleId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.OrganizationId, x.UserId }).IsUnique();
        builder.HasIndex(x => x.UserId);
    }
}

internal sealed class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.ToTable("roles");
        builder.Property(x => x.Name).HasMaxLength(Role.NameMaxLength);
        builder.Property(x => x.Description).HasMaxLength(Role.DescriptionMaxLength);
        builder.Property(x => x.Permissions).HasJsonConversion();
        builder.HasIndex(x => new { x.OrganizationId, x.Name }).IsUnique();
    }
}

internal sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("refresh_tokens");
        builder.Property(x => x.TokenHash).HasMaxLength(64).IsFixedLength();
        builder.Property(x => x.CreatedByIp).HasMaxLength(45);
        builder.Property(x => x.UserAgent).HasMaxLength(512);
        builder.HasIndex(x => x.TokenHash).IsUnique();
        builder.HasIndex(x => x.FamilyId);
        builder.HasIndex(x => x.UserId);
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Organization>().WithMany().HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class UserTokenConfiguration : IEntityTypeConfiguration<UserToken>
{
    public void Configure(EntityTypeBuilder<UserToken> builder)
    {
        builder.ToTable("user_tokens");
        builder.Property(x => x.TokenHash).HasMaxLength(64).IsFixedLength();
        builder.HasIndex(x => x.TokenHash).IsUnique();
        builder.HasIndex(x => new { x.UserId, x.Purpose });
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Membership>().WithMany().HasForeignKey(x => x.MembershipId).OnDelete(DeleteBehavior.Cascade);
    }
}
