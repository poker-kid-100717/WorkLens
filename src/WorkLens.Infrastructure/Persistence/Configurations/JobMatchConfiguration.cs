using WorkLens.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace WorkLens.Infrastructure.Persistence.Configurations;

public class JobMatchConfiguration : IEntityTypeConfiguration<JobMatch>
{
    public void Configure(EntityTypeBuilder<JobMatch> b)
    {
        b.ToTable("JobMatches");
        b.HasKey(x => x.Id);
        b.Property(x => x.MatchingSkillsJson).HasColumnType("nvarchar(max)");
        b.Property(x => x.MissingSkillsJson).HasColumnType("nvarchar(max)");
        b.Property(x => x.Summary).HasMaxLength(1000);

        b.HasIndex(x => new { x.ResumeId, x.JobListingId }).IsUnique();

        b.HasOne(x => x.Resume)
            .WithMany()
            .HasForeignKey(x => x.ResumeId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(x => x.JobListing)
            .WithMany()
            .HasForeignKey(x => x.JobListingId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
