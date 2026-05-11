using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using BiasAudit.Api.Data;
using BiasAudit.Api.Middleware;
using BiasAudit.Api.Options;
using BiasAudit.Api.OpenApi;
using BiasAudit.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new() { Title = "Body Positive Bias Audit API", Version = "v1" });
    options.AddSecurityDefinition("ApiKey", new OpenApiSecurityScheme
    {
        Name = "X-Api-Key",
        Type = SecuritySchemeType.ApiKey,
        In = ParameterLocation.Header,
        Description = "Podaj klucz API, np. dev-api-key-change-me."
    });
    options.OperationFilter<ApiKeyOperationFilter>();
});

builder.Services.Configure<ApiKeyOptions>(builder.Configuration.GetSection(ApiKeyOptions.SectionName));
builder.Services.Configure<StorageOptions>(builder.Configuration.GetSection(StorageOptions.SectionName));
builder.Services.Configure<NudeNetOptions>(builder.Configuration.GetSection(NudeNetOptions.SectionName));
builder.Services.Configure<AuditOptions>(builder.Configuration.GetSection(AuditOptions.SectionName));

builder.Services.AddDbContext<AuditDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Postgres")));

builder.Services.AddHttpClient<INudeNetClient, NudeNetHttpClient>();
builder.Services.AddSingleton<IBackgroundAuditQueue, BackgroundAuditQueue>();
builder.Services.AddScoped<IObjectStorage, S3ObjectStorage>();
builder.Services.AddScoped<IReportRenderer, HtmlReportRenderer>();
builder.Services.AddHostedService<AuditWorker>();

builder.Services.AddSingleton<IAmazonS3>(sp =>
{
    var storage = sp.GetRequiredService<IOptions<StorageOptions>>().Value;
    var credentials = new BasicAWSCredentials(storage.AccessKey, storage.SecretKey);
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
        config.RegionEndpoint = RegionEndpoint.GetBySystemName(storage.Region);
    }

    return new AmazonS3Client(credentials, config);
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");
    try
    {
        var db = scope.ServiceProvider.GetRequiredService<AuditDbContext>();
        await db.Database.EnsureCreatedAsync();
    }
    catch (Exception ex)
    {
        logger.LogWarning(ex, "Database initialization was skipped. The API can start, but database-backed endpoints require PostgreSQL.");
    }
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseMiddleware<ApiKeyMiddleware>();
app.MapControllers();

app.Run();
