using NetworkPingerService;
using Serilog;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddSerilog(config => 
    config.ReadFrom.Configuration(builder.Configuration));

builder.Services.AddOptions<PingSettings>()
    .Bind(builder.Configuration.GetSection(nameof(PingSettings)));

builder.Services.AddOptions<EmailSettings>()
    .Bind(builder.Configuration.GetSection(nameof(EmailSettings)));

builder.Services.AddSingleton<IEmailService, EmailService>();
builder.Services.AddSingleton<IDelayedEmailService, DelayedEmailService>();

builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
