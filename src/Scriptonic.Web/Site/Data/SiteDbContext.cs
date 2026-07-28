using Microsoft.EntityFrameworkCore;

namespace Scriptonic.Web.Site.Data;

public class SiteDbContext : DbContext
{
    public SiteDbContext(DbContextOptions<SiteDbContext> options) : base(options)
    {
    }

    public DbSet<ContactMessage> ContactMessages => Set<ContactMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ContactMessage>(entity =>
        {
            entity.HasKey(m => m.Id);
            entity.Property(m => m.Name).HasMaxLength(200).IsRequired();
            entity.Property(m => m.Email).HasMaxLength(320).IsRequired();
            entity.Property(m => m.Company).HasMaxLength(200);
            entity.Property(m => m.Subject).HasMaxLength(100).IsRequired();
            entity.Property(m => m.Message).HasMaxLength(4000).IsRequired();
            entity.HasIndex(m => m.CreatedUtc);
        });
    }
}

public class ContactMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime CreatedUtc { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Company { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public bool Handled { get; set; }
}
