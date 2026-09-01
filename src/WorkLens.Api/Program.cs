using WorkLens.Infrastructure;
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

// Apply any pending EF Core migrations automatically on startup. Convenient for an
// on-prem single-instance deployment; disable via appsettings if you prefer to run
// `dotnet ef database update` manually as part of your release process.
if (builder.Configuration.GetValue("Database:AutoMigrate", true))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<WorkLensDbContext>();
    db.Database.Migrate();
}

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
