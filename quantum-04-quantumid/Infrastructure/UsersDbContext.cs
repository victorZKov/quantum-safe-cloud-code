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

            entity.HasIndex(u => u.Email)
                .IsUnique()
                .HasFilter("\"IsDeleted\" = false");

            entity.HasQueryFilter(u => !u.IsDeleted);
        });
    }
}
