using WorkLens.Core.Interfaces;
using WorkLens.Infrastructure.FeedProviders;
using WorkLens.Infrastructure.Persistence;
using WorkLens.Infrastructure.Repositories;
using WorkLens.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace WorkLens.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("WorkLensDb")
            ?? throw new InvalidOperationException("Missing ConnectionStrings:WorkLensDb in configuration.");

        services.AddDbContext<WorkLensDbContext>(options =>
            options.UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure(5)));

        services.AddScoped<IJobListingRepository, JobListingRepository>();
        services.AddScoped<IJobApplicationRepository, JobApplicationRepository>();
        services.AddScoped<ISearchProfileRepository, SearchProfileRepository>();
        services.AddScoped<IResumeRepository, ResumeRepository>();
        services.AddScoped<IJobMatchRepository, JobMatchRepository>();

        services.Configure<FeedRefreshOptions>(configuration.GetSection("JobFeeds"));
        services.Configure<GreenhouseOptions>(configuration.GetSection("JobFeeds:Greenhouse"));
        services.Configure<OpenAiOptions>(configuration.GetSection("JobFeeds:OpenAi"));

        services.AddHttpClient<RemoteOkFeedProvider>(c =>
        {
            c.Timeout = TimeSpan.FromSeconds(20);
            c.DefaultRequestHeaders.UserAgent.ParseAdd("WorkLens/1.0 (+on-prem personal job tracker)");
        });
        services.AddHttpClient<RemotiveFeedProvider>(c =>
        {
            c.Timeout = TimeSpan.FromSeconds(20);
            c.DefaultRequestHeaders.UserAgent.ParseAdd("WorkLens/1.0 (+on-prem personal job tracker)");
        });
        services.AddHttpClient<GreenhouseFeedProvider>(c =>
        {
            c.Timeout = TimeSpan.FromSeconds(20);
            c.DefaultRequestHeaders.UserAgent.ParseAdd("WorkLens/1.0 (+on-prem personal job tracker)");
        });
        services.AddHttpClient<DiceFeedProvider>(c =>
        {
            c.Timeout = TimeSpan.FromSeconds(20);
            c.DefaultRequestHeaders.UserAgent.ParseAdd("WorkLens/1.0 (+on-prem personal job tracker)");
        });
        services.AddHttpClient<JobicyFeedProvider>(c =>
        {
            c.Timeout = TimeSpan.FromSeconds(20);
            c.DefaultRequestHeaders.UserAgent.ParseAdd("WorkLens/1.0 (+personal job discovery)");
        });
        services.AddHttpClient<WeWorkRemotelyFeedProvider>(c =>
        {
            c.Timeout = TimeSpan.FromSeconds(20);
            c.DefaultRequestHeaders.UserAgent.ParseAdd("WorkLens/1.0 (+personal job discovery)");
        });
        services.AddHttpClient<ChatGptWatchFeedProvider>(c =>
        {
            c.Timeout = TimeSpan.FromSeconds(15);
            c.DefaultRequestHeaders.UserAgent.ParseAdd("WorkLens/1.0 (+personal job discovery)");
        });

        services.AddScoped<IJobFeedProvider, RemoteOkFeedProvider>();
        services.AddScoped<IJobFeedProvider, RemotiveFeedProvider>();
        services.AddScoped<IJobFeedProvider, GreenhouseFeedProvider>();
        services.AddScoped<IJobFeedProvider, DiceFeedProvider>();
        services.AddScoped<IJobFeedProvider, JobicyFeedProvider>();
        services.AddScoped<IJobFeedProvider, WeWorkRemotelyFeedProvider>();
        services.AddScoped<IJobFeedProvider, ChatGptWatchFeedProvider>();

        services.AddScoped<JobFeedAggregatorService>();
        services.AddSingleton<FeedRefreshState>();
        services.AddHostedService<FeedRefreshBackgroundService>();

        services.AddHttpClient<OpenAiResumeMatchingService>(c => c.Timeout = TimeSpan.FromSeconds(45));
        services.AddScoped<IResumeMatchingService, OpenAiResumeMatchingService>();
        services.AddScoped<ResumeMatchOrchestrator>();

        return services;
    }
}
