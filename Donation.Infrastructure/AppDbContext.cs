using Donation.Core.OTPs;
using Donation.Core.Subscriptions;
using Donation.Core.Users;
using Microsoft.EntityFrameworkCore;

namespace Donation.Infrastructure;
public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Subscription> Subscriptions => Set<Subscription>();
    public DbSet<Otp> Otp => Set<Otp>();
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.UseOpenIddict();

        modelBuilder.Entity<User>(e =>
        {
            e.ToTable("users");

            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Email).IsUnique();
            e.Property(x => x.Email).IsRequired().HasMaxLength(256);
            e.Property(x => x.Name).IsRequired();
            e.Property(x => x.Lastname).IsRequired();
        });

        modelBuilder.Entity<Subscription>(e =>
        {
            e.ToTable("subscriptions");

            e.HasKey(x => x.Id);
            e.HasIndex(x => x.UserId);
            e.HasIndex(x => x.ExternalId);
            e.Property(x => x.Currency).IsRequired();
            e.Property(x => x.Status).IsRequired();
        });

        modelBuilder.Entity<Otp>(e =>
        {
            e.ToTable("otps");

            e.HasKey(x => x.Id);
            e.Property(x => x.Email).IsRequired();
            e.Property(x => x.Code).IsRequired().HasMaxLength(4);
        });
    }
}

