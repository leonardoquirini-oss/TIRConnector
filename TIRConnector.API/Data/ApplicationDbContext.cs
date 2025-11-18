using Microsoft.EntityFrameworkCore;

namespace TIRConnector.API.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Entity configurations will be added here when scaffolding from database
        // For now, this DbContext is used for raw SQL queries only
    }
}
