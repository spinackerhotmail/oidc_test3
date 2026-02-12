using Microsoft.EntityFrameworkCore;
using OIDC_test3.Configuration;
using OIDC_test3.Data;
using OIDC_test3.Services;

var builder = WebApplication.CreateBuilder(args);

// Configuration
builder.Services.Configure<EgiszOidcSettings>(
    builder.Configuration.GetSection(EgiszOidcSettings.SectionName));

// EF Core + PostgreSQL
builder.Services.AddDbContext<AuthDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("AuthDb")));

// Application services
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<ISessionService, SessionService>();
builder.Services.AddHttpClient<IEgiszOidcClient, EgiszOidcClient>();

builder.Services.AddControllers();
builder.Services.AddOpenApi();

var app = builder.Build();

// Auto-migrate database on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
    await db.Database.MigrateAsync();
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/openapi/v1.json", "EGISZ Auth API v1");
    });
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();
