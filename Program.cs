using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using System.Reflection;
using System.Threading.RateLimiting;
using TopMail.Rest.Options;
using TopMail.Rest.Security;
using TopMail.Rest.Services;
using TopMail.Rest.Swagger;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<SmtpOptions>(builder.Configuration.GetSection("Smtp"));
builder.Services.Configure<AzureAdOptions>(builder.Configuration.GetSection("AzureAd"));
builder.Services.Configure<TrackingOptions>(builder.Configuration.GetSection("Tracking"));
builder.Services.Configure<MailBodyOptions>(builder.Configuration.GetSection("MailBody"));
builder.Services.Configure<AttachmentSecurityOptions>(builder.Configuration.GetSection("AttachmentSecurity"));
builder.Services.Configure<AuthorizedClientsOptions>(builder.Configuration.GetSection("AuthorizedClients"));
builder.Services.Configure<FileLoggingOptions>(builder.Configuration.GetSection(FileLoggingOptions.SectionName));
builder.Services.AddSingleton<IValidateOptions<AuthorizedClientsOptions>, AuthorizedClientsOptionsValidator>();

var fileLoggingOptions = builder.Configuration
    .GetSection(FileLoggingOptions.SectionName)
    .Get<FileLoggingOptions>() ?? new FileLoggingOptions();
builder.Logging.AddProvider(new FileLoggerProvider(fileLoggingOptions));

builder.Services.AddMemoryCache();
builder.Services.AddSingleton<INonceReplayProtector, MemoryNonceReplayProtector>();
builder.Services.AddSingleton<IClientPolicyEvaluator, ClientPolicyEvaluator>();
builder.Services.AddSingleton<AzureAdTokenProvider>();
builder.Services.AddSingleton<IMailBodyFormatter, MailBodyFormatter>();
builder.Services.AddScoped<IMailService, SmtpOAuthMailService>();

builder.Services
    .AddAuthentication(ApiKeyHmacAuthenticationDefaults.SchemeName)
    .AddScheme<AuthenticationSchemeOptions, ApiKeyHmacAuthenticationHandler>(
        ApiKeyHmacAuthenticationDefaults.SchemeName,
        _ => { });

builder.Services.AddAuthorization();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
    {
        var clientId = httpContext.User.FindFirst("client_id")?.Value;
        var maxRpmClaim = httpContext.User.FindFirst("client_max_rpm")?.Value;
        var maxRpm = 60;
        if (!string.IsNullOrWhiteSpace(maxRpmClaim) && int.TryParse(maxRpmClaim, out var parsedMaxRpm) && parsedMaxRpm > 0)
        {
            maxRpm = parsedMaxRpm;
        }

        if (!string.IsNullOrWhiteSpace(clientId))
        {
            return RateLimitPartition.GetFixedWindowLimiter(
                $"client:{clientId}",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = maxRpm,
                    Window = TimeSpan.FromMinutes(1),
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = 0,
                    AutoReplenishment = true
                });
        }

        var remoteIp = httpContext.Connection.RemoteIpAddress?.ToString() ?? "anonymous";
        return RateLimitPartition.GetFixedWindowLimiter(
            $"anonymous:{remoteIp}",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0,
                AutoReplenishment = true
            });
    });
});

builder.Services.AddControllers().AddJsonOptions(options =>
{
    options.JsonSerializerOptions.PropertyNamingPolicy = null;
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "TopMail.Rest",
        Version = "v1",
        Description = "API per invio email SMTP OAuth2 con autenticazione HMAC."
    });

    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        options.IncludeXmlComments(xmlPath);
    }

    options.OperationFilter<SendMultipartRequestBodyOperationFilter>();
});

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.RoutePrefix = string.Empty;
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "TopMail.Rest v1");
});

app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();
app.MapControllers();

app.Run();
