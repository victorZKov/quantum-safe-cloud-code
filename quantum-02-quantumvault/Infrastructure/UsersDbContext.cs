using Microsoft.EntityFrameworkCore;
using UsersApi.Domain;

namespace UsersApi.Infrastructure;

public class UsersDbContext : DbContext
{
    public UsersDbContext(DbContextOptions<UsersDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(u => u.Id);

            entity.Property(u => u.Email)
                .IsRequired()
                .HasMaxLength(320);

            entity.Property(u => u.PasswordHash)
                .IsRequired();

            entity.Property(u => u.FirstName)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(u => u.LastName)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(u => u.CreatedAt)
                .IsRequired();

            entity.Property(u => u.IsDeleted)
                .IsRequired()
                .HasDefaultValue(false);

            entity.HasIndex(u => u.Email)
                .IsUnique()
                .HasFilter("\"IsDeleted\" = false");

            entity.HasQueryFilter(u => !u.IsDeleted);
        });
    }
}
