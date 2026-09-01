using WorkLens.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace WorkLens.Infrastructure.Persistence.Configurations;

public class JobListingConfiguration : IEntityTypeConfiguration<JobListing>
{
    public void Configure(EntityTypeBuilder<JobListing> b)
    {
        b.ToTable("JobListings");
        b.HasKey(x => x.Id);

        b.Property(x => x.ExternalId).HasMaxLength(200).IsRequired();
        b.Property(x => x.Source).HasConversion<string>().HasMaxLength(30);
        b.Property(x => x.Title).HasMaxLength(500).IsRequired();
        b.Property(x => x.Company).HasMaxLength(300).IsRequired();
        b.Property(x => x.Location).HasMaxLength(300);
        b.Property(x => x.Url).HasMaxLength(1000).IsRequired();
        b.Property(x => x.CompanyLogoUrl).HasMaxLength(1000);
        b.Property(x => x.SalaryMin).HasMaxLength(50);
        b.Property(x => x.SalaryMax).HasMaxLength(50);
        b.Property(x => x.SalaryCurrency).HasMaxLength(10);
        b.Property(x => x.TagsJson).HasColumnType("nvarchar(max)");
        b.Property(x => x.DescriptionHtml).HasColumnType("nvarchar(max)");

        // De-duplication key: one row per (source, externalId).
        b.HasIndex(x => new { x.Source, x.ExternalId }).IsUnique();
        b.HasIndex(x => x.IsActive);
        b.HasIndex(x => x.PostedAt);

        b.HasOne(x => x.Application)
            .WithOne(a => a.JobListing)
            .HasForeignKey<JobApplication>(a => a.JobListingId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
