using Microsoft.EntityFrameworkCore;
using Hamco.Data;
using Hamco.Core.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();

// Configure PostgreSQL
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
    ?? "Host=localhost;Database=hamco_dev;Username=art;Password=";

builder.Services.AddDbContext<HamcoDbContext>(options =>
    options.UseNpgsql(connectionString));

// Configure JWT Authentication
// Configure JWT Authentication using AddAuthServices extension
var jwtKey = builder.Configuration["Jwt:Key"] ?? "your-very-secret-key-that-is-at-least-32-characters-long";
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "hamco-api";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "hamco-client";

builder.Services.AddAuthServices(jwtKey, jwtIssuer, jwtAudience);

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

// Make Program accessible to tests
public partial class Program { }
