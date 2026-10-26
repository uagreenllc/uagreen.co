using Microsoft.EntityFrameworkCore;
using UaGreen.Site.Api.Domain;

namespace UaGreen.Site.Api.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductImage> ProductImages => Set<ProductImage>();
    public DbSet<ProductFile> ProductFiles => Set<ProductFile>();
    public DbSet<ProductOption> ProductOptions => Set<ProductOption>();
    public DbSet<ProductOptionValue> ProductOptionValues => Set<ProductOptionValue>();
    public DbSet<BlogPost> BlogPosts => Set<BlogPost>();
    public DbSet<Vacancy> Vacancies => Set<Vacancy>();
    public DbSet<Application> Applications => Set<Application>();
    public DbSet<ContactRequest> ContactRequests => Set<ContactRequest>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<ContentBlock> Content => Set<ContentBlock>();
    public DbSet<Setting> Settings => Set<Setting>();
    public DbSet<Redirect> Redirects => Set<Redirect>();
    public DbSet<SiteUser> SiteUsers => Set<SiteUser>();

    /// Replace a product's images/files/options wholesale. Children get explicit
    /// Remove/Add calls: entities with a pre-set Guid key that EF discovers through a
    /// navigation of an already-tracked product are otherwise tracked as Modified
    /// (= "exists in DB"), and the save then fails.
    public void ReplaceProductChildren(Product p, IEnumerable<ProductImage> images, IEnumerable<ProductFile> files, IEnumerable<ProductOption> options)
    {
        ProductOptionValues.RemoveRange(p.Options.SelectMany(o => o.Values));
        ProductOptions.RemoveRange(p.Options);
        ProductImages.RemoveRange(p.Images);
        ProductFiles.RemoveRange(p.Files);
        p.Images.Clear(); p.Files.Clear(); p.Options.Clear();

        var imgs = images.ToList(); var fls = files.ToList(); var opts = options.ToList();
        // Into the navigations first: DbSet.Add then finds them there and does not fix them up a second time.
        p.Images.AddRange(imgs); p.Files.AddRange(fls); p.Options.AddRange(opts);
        ProductImages.AddRange(imgs);
        ProductFiles.AddRange(fls);
        ProductOptions.AddRange(opts); // Add() marks the reachable Values as Added too
    }

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Category>(e =>
        {
            e.HasIndex(x => x.Slug).IsUnique();
            e.Property(x => x.Slug).HasMaxLength(120);
        });

        b.Entity<Product>(e =>
        {
            e.HasIndex(x => x.Slug).IsUnique();
            e.Property(x => x.Slug).HasMaxLength(120);
            e.Property(x => x.Price).HasPrecision(12, 2);
            e.Property(x => x.OldPrice).HasPrecision(12, 2);
            e.HasOne(x => x.Category).WithMany(c => c.Products).HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.SetNull);
            e.HasMany(x => x.Images).WithOne().HasForeignKey(i => i.ProductId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.Files).WithOne().HasForeignKey(f => f.ProductId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.Options).WithOne().HasForeignKey(o => o.ProductId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<ProductOption>()
            .HasMany(o => o.Values).WithOne(v => v.Option).HasForeignKey(v => v.OptionId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<ProductOptionValue>().Property(v => v.PriceDelta).HasPrecision(12, 2);

        b.Entity<BlogPost>(e =>
        {
            e.HasIndex(x => x.Slug).IsUnique();
            e.Property(x => x.Slug).HasMaxLength(160);
        });

        b.Entity<Vacancy>(e =>
        {
            e.HasIndex(x => x.Slug).IsUnique();
            e.Property(x => x.Slug).HasMaxLength(120);
        });

        b.Entity<Order>(e =>
        {
            // Sequential human number next to the Guid public id.
            e.Property(x => x.Number).ValueGeneratedOnAdd().UseIdentityByDefaultColumn();
            e.HasIndex(x => x.Number).IsUnique();
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
            e.Property(x => x.PaymentMethod).HasConversion<string>().HasMaxLength(32);
            e.Property(x => x.DeliveryMethod).HasConversion<string>().HasMaxLength(32);
            e.Property(x => x.Subtotal).HasPrecision(12, 2);
            e.Property(x => x.DeliveryCost).HasPrecision(12, 2);
            e.Property(x => x.Total).HasPrecision(12, 2);
            e.HasMany(x => x.Items).WithOne().HasForeignKey(i => i.OrderId).OnDelete(DeleteBehavior.Cascade);
        });
        b.Entity<OrderItem>(e =>
        {
            e.Property(x => x.UnitPrice).HasPrecision(12, 2);
            e.Property(x => x.LineTotal).HasPrecision(12, 2);
        });

        b.Entity<ContentBlock>(e =>
        {
            e.HasKey(x => new { x.Key, x.Lang });
            e.Property(x => x.Key).HasMaxLength(80);
            e.Property(x => x.Lang).HasMaxLength(2);
        });
        b.Entity<Setting>(e =>
        {
            e.HasKey(x => x.Key);
            e.Property(x => x.Key).HasMaxLength(80);
        });
        b.Entity<Redirect>(e =>
        {
            e.HasKey(x => x.FromPath);
            e.Property(x => x.FromPath).HasMaxLength(300);
        });
        b.Entity<SiteUser>(e =>
        {
            // ponytail: comma-joined roles in one column ("Sales,Editor") — provider-agnostic, 4 roles max.
            e.Property(x => x.Roles)
                .HasConversion(
                    v => string.Join(",", v.Select(r => r.ToString())),
                    v => v.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(s => Enum.Parse<SiteRole>(s)).ToList(),
                    new Microsoft.EntityFrameworkCore.ChangeTracking.ValueComparer<List<SiteRole>>(
                        (a, b2) => a!.SequenceEqual(b2!),
                        v => v.Aggregate(0, (h, r) => HashCode.Combine(h, r)),
                        v => v.ToList()))
                .HasMaxLength(100);
        });
    }
}
