using WorkLens.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace WorkLens.Infrastructure.Persistence.Configurations;

public class SearchProfileConfiguration : IEntityTypeConfiguration<SearchProfile>
{
    public void Configure(EntityTypeBuilder<SearchProfile> b)
    {
        b.ToTable("SearchProfiles");
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).HasMaxLength(200).IsRequired();
        b.Property(x => x.KeywordsJson).HasColumnType("nvarchar(max)");
        b.Property(x => x.LocationFilter).HasMaxLength(300);
    }
}
