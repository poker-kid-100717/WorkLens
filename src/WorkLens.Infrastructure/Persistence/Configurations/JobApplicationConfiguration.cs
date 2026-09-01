using WorkLens.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace WorkLens.Infrastructure.Persistence.Configurations;

public class JobApplicationConfiguration : IEntityTypeConfiguration<JobApplication>
{
    public void Configure(EntityTypeBuilder<JobApplication> b)
    {
        b.ToTable("JobApplications");
        b.HasKey(x => x.Id);

        b.Property(x => x.Title).HasMaxLength(500).IsRequired();
        b.Property(x => x.Company).HasMaxLength(300).IsRequired();
        b.Property(x => x.Location).HasMaxLength(300);
        b.Property(x => x.Url).HasMaxLength(1000);
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(30);
        b.Property(x => x.Notes).HasColumnType("nvarchar(max)");
        b.Property(x => x.ContactName).HasMaxLength(200);
        b.Property(x => x.ContactEmail).HasMaxLength(300);

        b.HasIndex(x => x.Status);
        b.HasIndex(x => x.FollowUpAt);

        b.HasMany(x => x.StatusHistory)
            .WithOne(h => h.JobApplication)
            .HasForeignKey(h => h.JobApplicationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
