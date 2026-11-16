using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Licensing.Server.Data;

public sealed class AppDbContext : IdentityDbContext<AppUser>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Edition> Editions => Set<Edition>();
    public DbSet<FeatureDefinition> FeatureDefinitions => Set<FeatureDefinition>();
    public DbSet<License> Licenses => Set<License>();
    public DbSet<LicenseFeature> LicenseFeatures => Set<LicenseFeature>();
    public DbSet<Activation> Activations => Set<Activation>();
    public DbSet<RehostEvent> RehostEvents => Set<RehostEvent>();
    public DbSet<StaffCustomer> StaffCustomers => Set<StaffCustomer>();
    public DbSet<SigningKey> SigningKeys => Set<SigningKey>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Customer>(b =>
        {
            b.Property(x => x.Name).HasMaxLength(200);
            b.Property(x => x.Email).HasMaxLength(200);
            b.Property(x => x.Company).HasMaxLength(200);
            b.Property(x => x.Phone).HasMaxLength(80);
            b.Property(x => x.Notes).HasMaxLength(2000);
        });

        builder.Entity<Product>(b =>
        {
            b.Property(x => x.PublicId).HasMaxLength(64);
            b.Property(x => x.Name).HasMaxLength(200);
            b.HasIndex(x => x.PublicId).IsUnique();
        });

        builder.Entity<Edition>(b =>
        {
            b.Property(x => x.Code).HasMaxLength(64);
            b.Property(x => x.Name).HasMaxLength(200);
            b.HasIndex(x => new { x.ProductId, x.Code }).IsUnique();
            b.HasOne(x => x.Product).WithMany(x => x.Editions).HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<FeatureDefinition>(b =>
        {
            b.Property(x => x.Code).HasMaxLength(64);
            b.Property(x => x.Name).HasMaxLength(200);
            b.HasIndex(x => new { x.ProductId, x.Code }).IsUnique();
            b.HasOne(x => x.Product).WithMany(x => x.Features).HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<License>(b =>
        {
            b.Property(x => x.KeyDisplay).HasMaxLength(200);
            b.Property(x => x.KeyNormalized).HasMaxLength(200);
            b.Property(x => x.EditionCode).HasMaxLength(64);
            b.Property(x => x.PersonLabel).HasMaxLength(200);
            b.Property(x => x.DongleId).HasMaxLength(200);
            b.Property(x => x.Kind).HasConversion<string>().HasMaxLength(32);
            b.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
            b.HasIndex(x => x.KeyNormalized).IsUnique();
            b.HasIndex(x => x.DongleId);
            b.HasOne(x => x.Customer).WithMany(x => x.Licenses).HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<LicenseFeature>(b =>
        {
            b.HasKey(x => new { x.LicenseId, x.FeatureCode });
            b.Property(x => x.FeatureCode).HasMaxLength(64);
            b.HasOne(x => x.License).WithMany(x => x.Features).HasForeignKey(x => x.LicenseId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Activation>(b =>
        {
            b.Property(x => x.MachineId).HasMaxLength(200);
            b.Property(x => x.DongleId).HasMaxLength(200);
            b.Property(x => x.AppVersion).HasMaxLength(80);
            b.Property(x => x.ReleaseReason).HasMaxLength(64);
            b.Property(x => x.SnapshotEdition).HasMaxLength(64);
            b.Property(x => x.SnapshotFeatures).HasMaxLength(2000);
            b.HasIndex(x => new { x.LicenseId, x.MachineId });
            b.HasOne(x => x.License).WithMany(x => x.Activations).HasForeignKey(x => x.LicenseId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<RehostEvent>(b =>
        {
            b.Property(x => x.FromMachineId).HasMaxLength(200);
            b.Property(x => x.ToMachineId).HasMaxLength(200);
            b.Property(x => x.Actor).HasConversion<string>().HasMaxLength(16);
            b.HasIndex(x => new { x.LicenseId, x.Year, x.Actor });
            b.HasOne(x => x.License).WithMany().HasForeignKey(x => x.LicenseId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<StaffCustomer>(b =>
        {
            b.HasKey(x => new { x.UserId, x.CustomerId });
            b.HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<SigningKey>(b =>
        {
            b.HasKey(x => x.Kid);
            b.Property(x => x.Kid).HasMaxLength(40);
            b.Property(x => x.Modulus).HasMaxLength(2000);
            b.Property(x => x.Exponent).HasMaxLength(64);
        });

        builder.Entity<AuditLog>(b =>
        {
            b.Property(x => x.Action).HasMaxLength(40);
            b.Property(x => x.Detail).HasMaxLength(2000);
            b.Property(x => x.Ip).HasMaxLength(64);
            b.Property(x => x.ActorUserId).HasMaxLength(450);
            b.HasIndex(x => x.At);
            b.HasIndex(x => x.CustomerId);
        });
    }
}

public sealed class DesignTimeDbContextFactory : Microsoft.EntityFrameworkCore.Design.IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite("Data Source=storage/licensing.db")
            .Options;
        return new AppDbContext(options);
    }
}
