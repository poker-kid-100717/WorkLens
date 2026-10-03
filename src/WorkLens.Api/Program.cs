using WorkLens.Infrastructure;
using WorkLens.Infrastructure.Demo;
using WorkLens.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// CORS: the Angular app is served separately (its own container/port) in the on-prem
// deployment, so the API needs to allow cross-origin calls from it. Configure the
// allowed origin(s) via appsettings -> Cors:AllowedOrigins for your actual host/domain.
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? new[] { "http://localhost:4200" };

builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod());
});

builder.Services.AddControllers();
builder.Services.AddOpenApi();

builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

var demoEnabled = builder.Configuration.GetValue("Demo:Enabled", false);

if (demoEnabled)
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<WorkLensDbContext>();
    if (db.Database.IsSqlServer())
    {
        // Public read-only demo on hosted SQL Server: migrate, then replace the fictional
        // tracker data so its relative dates stay current. Live job listings are kept.
        db.Database.Migrate();
        await DemoSeeder.ResetAsync(db);
    }
    else
    {
        // Public read-only demo: rebuild the throwaway SQLite database from the model (the
        // migrations are SQL Server-specific) and seed fictional tracker data on every start.
        db.Database.EnsureDeleted();
        db.Database.EnsureCreated();
    }
    await DemoSeeder.SeedAsync(db);
}
// Apply any pending EF Core migrations automatically on startup. Convenient for an
// on-prem single-instance deployment; disable via appsettings if you prefer to run
// `dotnet ef database update` manually as part of your release process.
else if (builder.Configuration.GetValue("Database:AutoMigrate", true))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<WorkLensDbContext>();
    db.Database.Migrate();
}

if (demoEnabled)
{
    // Every visitor shares the same data, so the demo is read-only: reject all writes, and
    // the Outlook OAuth flow, before they reach a controller.
    app.Use(async (context, next) =>
    {
        var path = context.Request.Path;
        var isWrite = !(HttpMethods.IsGet(context.Request.Method)
            || HttpMethods.IsHead(context.Request.Method)
            || HttpMethods.IsOptions(context.Request.Method));
        var isOutlookAuth = path.StartsWithSegments("/api/outlook/connect")
            || path.StartsWithSegments("/api/outlook/callback");

        if (path.StartsWithSegments("/api") && (isWrite || isOutlookAuth))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new
            {
                error = "This is a read-only public demo of WorkLens. Changes are disabled.",
            });
            return;
        }

        await next();
    });
}

app.MapGet("/api/demo", () => Results.Ok(new { enabled = demoEnabled }));

if (app.Environment.IsDevelopment() || builder.Configuration.GetValue("Swagger:Enabled", false))
{
    // Native .NET OpenAPI document at /openapi/v1.json. View it with any OpenAPI UI
    // (e.g. import into Postman/Insomnia, or add Scalar/Swagger UI packages later).
    app.MapOpenApi();
}

app.UseCors("Frontend");
app.UseAuthorization();
app.MapControllers();

app.Run();
