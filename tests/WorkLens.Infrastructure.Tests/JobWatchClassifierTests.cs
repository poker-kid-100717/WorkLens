using System.Text.Json;
using WorkLens.Core.Entities;
using WorkLens.Infrastructure.Services;
using Xunit;

namespace WorkLens.Infrastructure.Tests;

public class JobWatchClassifierTests
{
    [Fact]
    public void ApplyTags_AddsCareerAndSalaryWatch_ForRemoteTargetRole()
    {
        var job = new JobListing
        {
            Title = "Senior Software Engineer",
            IsRemote = true,
            SalaryMax = "$165,000",
            TagsJson = "[]"
        };

        JobWatchClassifier.ApplyTags(job);

        var tags = JsonSerializer.Deserialize<List<string>>(job.TagsJson)!;
        Assert.Contains("Career Watch", tags);
        Assert.Contains("$160k+ watch", tags);
    }

    [Fact]
    public void ApplyTags_DoesNotClassifyManagementRoles()
    {
        var job = new JobListing
        {
            Title = "Software Engineering Manager",
            IsRemote = true,
            SalaryMax = "180000",
            TagsJson = "[]"
        };

        JobWatchClassifier.ApplyTags(job);

        Assert.Equal("[]", job.TagsJson);
    }

    [Fact]
    public void ApplyTags_DoesNotClassifyNonRemoteRoles()
    {
        var job = new JobListing
        {
            Title = "Lead .NET Engineer",
            IsRemote = false,
            SalaryMax = "160000",
            TagsJson = "[]"
        };

        JobWatchClassifier.ApplyTags(job);

        Assert.Equal("[]", job.TagsJson);
    }
}
