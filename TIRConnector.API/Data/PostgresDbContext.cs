using Microsoft.EntityFrameworkCore;
using TIRConnector.API.Models.Entities;

namespace TIRConnector.API.Data;

/// <summary>
/// DbContext per la connessione al database PostgreSQL contenente i query templates
/// </summary>
public class PostgresDbContext : DbContext
{
    public PostgresDbContext(DbContextOptions<PostgresDbContext> options)
        : base(options)
    {
    }

    public DbSet<QueryTemplate> QueryTemplates { get; set; } = null!;
    public DbSet<QueryTag> QueryTags { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<QueryTemplate>(entity =>
        {
            entity.ToTable("query_templates");

            entity.HasKey(e => e.IdQueryTemplate);

            entity.Property(e => e.IdQueryTemplate)
                .HasColumnName("id_query_template");

            entity.Property(e => e.Name)
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(e => e.QuerySql)
                .IsRequired();

            entity.Property(e => e.Params)
                .HasColumnType("jsonb")
                .HasDefaultValue("[]");

            entity.Property(e => e.OutputFormat)
                .HasMaxLength(20)
                .HasDefaultValue("json");

            entity.Property(e => e.MaxResults)
                .HasDefaultValue(10000);

            entity.Property(e => e.TimeoutSeconds)
                .HasDefaultValue(30);

            entity.Property(e => e.Version)
                .HasDefaultValue(1);

            entity.Property(e => e.CreationDate)
                .HasDefaultValueSql("NOW()");

            entity.Property(e => e.Active)
                .HasDefaultValue(true);

            entity.Property(e => e.Deprecated)
                .HasDefaultValue(false);
        });

        modelBuilder.Entity<QueryTag>(entity =>
        {
            entity.ToTable("query_tags");

            entity.HasKey(e => e.IdQueryQueryTag);

            entity.Property(e => e.IdQueryQueryTag)
                .HasColumnName("id_query_query_tag");

            entity.Property(e => e.IdQueryTemplate)
                .HasColumnName("id_query_template");

            entity.Property(e => e.Version)
                .HasColumnName("version");

            entity.Property(e => e.QuerySql)
                .IsRequired()
                .HasColumnName("query_sql");

            entity.Property(e => e.Params)
                .HasColumnType("jsonb")
                .HasColumnName("params");

            entity.Property(e => e.Name)
                .HasMaxLength(200)
                .HasColumnName("name");

            entity.Property(e => e.Description)
                .HasColumnName("description");

            entity.Property(e => e.CreationDate)
                .HasDefaultValueSql("NOW()")
                .HasColumnName("creation_date");

            entity.Property(e => e.ChangeReason)
                .HasColumnName("change_reason");

            entity.Property(e => e.ChangeType)
                .HasMaxLength(20)
                .HasColumnName("change_type");

            entity.Property(e => e.SqlDiff)
                .HasColumnName("sql_diff");

            entity.HasOne(e => e.QueryTemplate)
                .WithMany()
                .HasForeignKey(e => e.IdQueryTemplate)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
