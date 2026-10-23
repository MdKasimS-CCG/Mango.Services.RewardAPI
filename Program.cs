using Mango.Services.RewardAPI.Data;
using Mango.Services.RewardAPI.Messaging;

using Microsoft.EntityFrameworkCore;
using DotNetEnv;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;

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
    Secret = builder.Configuration["ApiSettings:Secret"]
    ?? string.Empty,

    Issuer = builder.Configuration["ApiSettings:Issuer"]
    ?? string.Empty,

    Audience = builder.Configuration["ApiSettings:Audience"]
    ?? string.Empty,

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

builder.Services.Configure<OrderCreatedConsumerSettings>(settings =>
{
    builder.Configuration.GetSection("MessageQueue:RabbitMQ").Bind(settings);
    settings.QueueName = builder.Configuration[
        "TopicAndQueueNames:OrderCreatedRewardQueue"] ?? string.Empty;
});
builder.Services.AddHostedService<OrderCreatedConsumer>();

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Configure JWT authentication to match OrderAPI.
var key = Encoding.ASCII.GetBytes(mangoOptions.Secret);

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme =
        JwtBearerDefaults.AuthenticationScheme;

    options.DefaultChallengeScheme =
        JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(key),

        ValidateIssuer = true,
        ValidIssuer = mangoOptions.Issuer,

        ValidateAudience = true,
        ValidAudience = mangoOptions.Audience
    };
});

builder.Services.AddAuthorization();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthentication();
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
    public string Secret { get; set; } = string.Empty;
    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
}