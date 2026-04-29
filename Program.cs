using TopMail.Rest.Options;
using TopMail.Rest.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<SmtpOptions>(builder.Configuration.GetSection("Smtp"));
builder.Services.Configure<AzureAdOptions>(builder.Configuration.GetSection("AzureAd"));
builder.Services.Configure<TrackingOptions>(builder.Configuration.GetSection("Tracking"));
builder.Services.Configure<MailBodyOptions>(builder.Configuration.GetSection("MailBody"));
builder.Services.Configure<AttachmentSecurityOptions>(builder.Configuration.GetSection("AttachmentSecurity"));

builder.Services.AddSingleton<AzureAdTokenProvider>();
builder.Services.AddSingleton<IMailBodyFormatter, MailBodyFormatter>();
builder.Services.AddScoped<IMailService, SmtpOAuthMailService>();

builder.Services.AddControllers().AddJsonOptions(options =>
{
    options.JsonSerializerOptions.PropertyNamingPolicy = null;
});

var app = builder.Build();

app.MapControllers();

app.Run();
