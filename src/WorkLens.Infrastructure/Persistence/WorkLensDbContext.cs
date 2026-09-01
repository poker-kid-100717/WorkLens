using WorkLens.Core.Entities;
using Microsoft.EntityFrameworkCore;

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
        base.OnModelCreating(modelBuilder);
    }
}
