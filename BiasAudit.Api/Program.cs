using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using System.Text;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using BiasAudit.Api.Data;
using BiasAudit.Api.Middleware;
using BiasAudit.Api.Options;
using BiasAudit.Api.OpenApi;
using BiasAudit.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// ---------------- CONTROLLERS ----------------
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// ---------------- SWAGGER ----------------
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new()
    {
        Title = "Bias Audit API",
        Version = "v1"
    });

    options.AddSecurityDefinition("ApiKey", new OpenApiSecurityScheme
    {
        Name = "X-Api-Key",
        Type = SecuritySchemeType.ApiKey,
        In = ParameterLocation.Header,
        Description = "API Key"
    });

    options.OperationFilter<ApiKeyOperationFilter>();
});

// ---------------- OPTIONS ----------------
builder.Services.Configure<ApiKeyOptions>(
    builder.Configuration.GetSection(ApiKeyOptions.SectionName));

builder.Services.Configure<StorageOptions>(
    builder.Configuration.GetSection(StorageOptions.SectionName));

builder.Services.Configure<NudeNetOptions>(
    builder.Configuration.GetSection(NudeNetOptions.SectionName));

builder.Services.Configure<AuditOptions>(
    builder.Configuration.GetSection(AuditOptions.SectionName));

builder.Services.Configure<JwtOptions>(
    builder.Configuration.GetSection(JwtOptions.SectionName));

// ---------------- SERVICES ----------------
builder.Services.AddScoped<IJwtService, JwtService>();

builder.Services.AddDbContext<AuditDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("Postgres")));

builder.Services.AddHttpClient<INudeNetClient, NudeNetHttpClient>();
builder.Services.AddSingleton<IBackgroundAuditQueue, BackgroundAuditQueue>();
builder.Services.AddScoped<IObjectStorage, S3ObjectStorage>();
builder.Services.AddScoped<IReportRenderer, HtmlReportRenderer>();
builder.Services.AddHostedService<AuditWorker>();

// ---------------- JWT ----------------
var jwt = builder.Configuration
    .GetSection(JwtOptions.SectionName)
    .Get<JwtOptions>()!;

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,

            ValidIssuer = jwt.Issuer,
            ValidAudience = jwt.Audience,

            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwt.Key)),

            NameClaimType = ClaimTypes.NameIdentifier
        };

        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = async context =>
            {
                var db = context.HttpContext.RequestServices
                    .GetRequiredService<AuditDbContext>();

                var jti = context.Principal?
                    .FindFirst(JwtRegisteredClaimNames.Jti)?
                    .Value;

                if (string.IsNullOrEmpty(jti))
                {
                    context.Fail("Missing jti");
                    return;
                }

                var revoked = await db.RevokedTokens
                    .AnyAsync(x => x.JwtId == jti);

                if (revoked)
                {
                    context.Fail("Token revoked");
                }
            }
        };
    });

builder.Services.AddAuthorization();

// ---------------- AWS S3 ----------------
builder.Services.AddSingleton<IAmazonS3>(sp =>
{
    var storage = sp.GetRequiredService<IOptions<StorageOptions>>().Value;

    var credentials = new BasicAWSCredentials(
        storage.AccessKey,
        storage.SecretKey);

    var config = new AmazonS3Config
    {
        ForcePathStyle = storage.ForcePathStyle
    };

    if (!string.IsNullOrWhiteSpace(storage.ServiceUrl))
    {
        config.ServiceURL = storage.ServiceUrl;
    }
    else
    {
        config.RegionEndpoint =
            RegionEndpoint.GetBySystemName(storage.Region);
    }

    return new AmazonS3Client(credentials, config);
});

var app = builder.Build();

var apiKey = builder.Configuration
    .GetSection(ApiKeyOptions.SectionName)
    .Get<ApiKeyOptions>();

// ---------------- DB INIT ----------------
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AuditDbContext>();
    await db.Database.EnsureCreatedAsync();
}

// ---------------- SWAGGER ----------------
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// ---------------- PIPELINE (KLUCZOWE) ----------------
app.UseHttpsRedirection();
app.UseStaticFiles();

// 🔥 API KEY MIDDLEWARE (przed auth)
app.UseMiddleware<ApiKeyMiddleware>();

// 🔥 AUTH
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();