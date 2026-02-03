using Microsoft.EntityFrameworkCore;
using WebApi.Data;

Console.WriteLine("✅ APP STARTING...");

var builder = WebApplication.CreateBuilder(args);

Console.WriteLine("✅ BUILDER CREATED");

var connectionString = Environment.GetEnvironmentVariable("DATABASE_URL")
    ?? builder.Configuration.GetConnectionString("DefaultConnection");

Console.WriteLine($"✅ CONNECTION STRING: {(connectionString != null ? "FOUND" : "NULL")}");

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddDbContext<DragonListDbContext>(
    options => options.UseNpgsql(connectionString,
        npgsqlOptions => npgsqlOptions.CommandTimeout(1800)));

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

Console.WriteLine("✅ SERVICES CONFIGURED");

var app = builder.Build();

Console.WriteLine("✅ APP BUILT");

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowAll");
app.UseAuthorization();
app.MapControllers();

var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
app.Urls.Add($"http://0.0.0.0:{port}");

Console.WriteLine($"✅ STARTING ON PORT {port}");

app.Run();

Console.WriteLine("✅ APP RUNNING");