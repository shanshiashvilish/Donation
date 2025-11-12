using Donation.Core.OTPs;
using Donation.Core.Payments;
using Donation.Core.Subscriptions;
using Donation.Core.Users;
using Microsoft.EntityFrameworkCore;

namespace Donation.Infrastructure;
public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Subscription> Subscriptions => Set<Subscription>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<Otp> Otp => Set<Otp>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>(e =>
        {
            e.ToTable("users");

            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Email).IsUnique();
            e.Property(x => x.Email).IsRequired().HasMaxLength(100);
            e.Property(x => x.Name).IsRequired().HasMaxLength(100);
            e.Property(x => x.Lastname).IsRequired().HasMaxLength(100);

            e.HasMany(x => x.Subscriptions)
             .WithOne(s => s.User)
             .HasForeignKey(s => s.UserId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasMany(x => x.Payments)
             .WithOne(p => p.User)
             .HasForeignKey(p => p.UserId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Subscription>(e =>
        {
            e.ToTable("subscriptions");

            e.HasKey(x => x.Id);
            e.HasIndex(x => x.UserId);
            e.HasIndex(x => x.ExternalId).IsUnique();
            e.Property(x => x.Currency).IsRequired();
            e.Property(x => x.Status).IsRequired();

            e.HasMany(s => s.Payments)
             .WithOne(p => p.Subscription)
             .HasForeignKey(p => p.SubscriptionId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Payment>(e =>
        {
            e.ToTable("payments");

            e.HasKey(x => x.Id);
            e.HasIndex(x => x.SubscriptionId);
            e.HasIndex(x => x.UserId);
            e.Property(x => x.Amount).IsRequired();
            e.Property(x => x.Type).IsRequired();
            e.Property(x => x.Amount).IsRequired();
            e.Property(x => x.Type).IsRequired();
            e.Property(x => x.SubscriptionId).IsRequired(false);

            e.HasOne(x => x.Subscription)
             .WithMany(s => s.Payments)
             .HasForeignKey(x => x.SubscriptionId) 
             .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(x => x.User)
             .WithMany(u => u.Payments)
             .HasForeignKey(x => x.UserId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Otp>(e =>
        {
            e.ToTable("otps");

            e.HasKey(x => x.Id);
            e.Property(x => x.Email).IsRequired();
            e.Property(x => x.Code).IsRequired();
        });
    }
}

