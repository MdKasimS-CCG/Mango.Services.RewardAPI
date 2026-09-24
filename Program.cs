using Mango.Services.RewardAPI.Data;

using Microsoft.EntityFrameworkCore;
using DotNetEnv;
using Microsoft.Extensions.Options;

bool isRunningInContainer =
    Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER") == "true";

if (!isRunningInContainer)
{
    Env.Load(".env");
}

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddEnvironmentVariables();

var mangoOptions = new MangoOptions
{
    DefaultConnection =
    (isRunningInContainer
        ? builder.Configuration["Docker:ConnectionStrings:DefaultConnection"]
        : builder.Configuration["Http:ConnectionStrings:DefaultConnection"])
    ?? string.Empty
};

builder.Services.AddSingleton(
    Microsoft.Extensions.Options.Options.Create(mangoOptions));

// Add services to the container.
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(mangoOptions.DefaultConnection));

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();
ApplyMigration();
app.Run();

void ApplyMigration()
{
    using (var scope = app.Services.CreateScope())
    {
        var _db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        if (_db.Database.GetPendingMigrations().Count() > 0)
        {
            _db.Database.Migrate();
        }
    }
}

public class MangoOptions
{
    public string DefaultConnection { get; set; } = string.Empty;
}