using Microsoft.EntityFrameworkCore;
using WebApi.Data;

Console.WriteLine("Fatalis STARTING to fly...");
var builder = WebApplication.CreateBuilder(args);
Console.WriteLine(" Build CREATED");

var connectionString = Environment.GetEnvironmentVariable("DATABASE_URL")
    ?? builder.Configuration.GetConnectionString("DefaultConnection");

if (string.IsNullOrEmpty(connectionString))
{
    Console.WriteLine("CONNECTION STRING IS NULL OR EMPTY!");
    throw new Exception("Database connection string not found!");
}

Console.WriteLine($"CONNECTION STRING FOUND: {connectionString.Substring(0, 20)}...");

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddDbContext<DragonListDbContext>(
    options => options.UseNpgsql(connectionString,
        npgsqlOptions => npgsqlOptions.CommandTimeout(1800)));


// Configura CORS per consentire richieste da qualsiasi origine 
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

Console.WriteLine("SERVICES CONFIGURED");

var app = builder.Build();
Console.WriteLine("CASTLE BUILT");

// Test database connection
try
{
    using (var scope = app.Services.CreateScope())
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<DragonListDbContext>();
        await dbContext.Database.CanConnectAsync();
        Console.WriteLine("DATABASE CONNECTION SUCCESSFUL");
    }
}
catch (Exception ex)
{
    Console.WriteLine($"DATABASE CONNECTION FAILED: {ex.Message}");
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// CORS auth
app.UseCors("AllowAll");
app.UseAuthorization();

// Health check open
app.MapGet("/", () =>
{
    Console.WriteLine("Fatalis destroys the castles in the meantime...");
    return Results.Ok(new { message = "API is running!", timestamp = DateTime.UtcNow });
});

app.MapGet("/health", () =>
{
    Console.WriteLine("Fatalis destroys the castles in the meantime...");
    return Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow });
});

// Mapper controller
app.MapControllers();

var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
app.Urls.Add($"http://0.0.0.0:{port}");
Console.WriteLine($"STARTING ON DEMON PORT {port}");

app.Run();
Console.WriteLine("APP RUNNING!");