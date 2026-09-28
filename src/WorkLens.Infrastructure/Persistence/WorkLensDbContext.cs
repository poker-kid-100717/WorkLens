using WorkLens.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace WorkLens.Infrastructure.Persistence;

public class WorkLensDbContext : DbContext
{
    public WorkLensDbContext(DbContextOptions<WorkLensDbContext> options) : base(options) { }

    public DbSet<JobListing> JobListings => Set<JobListing>();
    public DbSet<JobApplication> JobApplications => Set<JobApplication>();
    public DbSet<ApplicationStatusHistory> ApplicationStatusHistories => Set<ApplicationStatusHistory>();
    public DbSet<SearchProfile> SearchProfiles => Set<SearchProfile>();
    public DbSet<Resume> Resumes => Set<Resume>();
    public DbSet<JobMatch> JobMatches => Set<JobMatch>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(WorkLensDbContext).Assembly);

        // The public demo runs on SQLite. Adapt the SQL Server-oriented model there only:
        // SQLite cannot compare or ORDER BY DateTimeOffset values natively (store them as
        // sortable binary ticks) and does not understand nvarchar(max) (use TEXT).
        if (Database.IsSqlite())
        {
            foreach (var property in modelBuilder.Model.GetEntityTypes().SelectMany(t => t.GetProperties()))
            {
                if (property.ClrType == typeof(DateTimeOffset) || property.ClrType == typeof(DateTimeOffset?))
                    property.SetValueConverter(new DateTimeOffsetToBinaryConverter());

                if (property.GetColumnType() is { } columnType && columnType.Contains("(max)", StringComparison.OrdinalIgnoreCase))
                    property.SetColumnType("TEXT");
            }
        }

        base.OnModelCreating(modelBuilder);
    }
}
