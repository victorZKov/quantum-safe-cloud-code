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

            // Encrypted email ciphertext — no uniqueness constraint (ciphertext is not deterministic)
            entity.Property(u => u.EmailCiphertext)
                .IsRequired();

            // SHA3-256 hash used for login lookups — this is the unique searchable index
            entity.Property(u => u.SearchableEmail)
                .IsRequired()
                .HasMaxLength(64);

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

            // Unique index on the searchable hash — used for login lookups
            entity.HasIndex(u => u.SearchableEmail)
                .IsUnique()
                .HasFilter("\"IsDeleted\" = false");

            entity.HasQueryFilter(u => !u.IsDeleted);
        });
    }
}
