using System.Text.Json;
using System.Text.Json.Serialization;
using BFF.Application;
using BFF.WebApi.Audit;
using BFF.WebApi.Extensions;
using BFF.WebApi.Middleware;
using BFF.WebApi.Notifications;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.ValidateRequiredConfiguration();

var logLevel = Enum.TryParse<LogEventLevel>(builder.Configuration["LOG_LEVEL"], ignoreCase: true, out var parsedLevel)
    ? parsedLevel
    : LogEventLevel.Information;

builder.Host.UseSerilog((context, services, loggerConfiguration) => loggerConfiguration
    .MinimumLevel.Is(logLevel)
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext()
    .WriteTo.Console(new CompactJsonFormatter()));

builder.Services.AddApplicationServices(builder.Configuration);
builder.Services.AddBffAuthentication(builder.Configuration);
builder.Services.AddBffAuthorization();
builder.Services.AddBffCors(builder.Configuration);
builder.Services.AddSingleton<AuditWriter>();
builder.Services.AddSingleton<INoticePublisher, RabbitMqNoticePublisher>();
builder.Services.AddSingleton<IModelCommandPublisher, RabbitMqModelCommandPublisher>();
builder.Services.AddScoped<ModelDecisionRelay>();
builder.Services.AddSingleton<EmailRateLimiter>();
builder.Services.AddScoped<EmailNotificationService>();
builder.Services.AddScoped<FactNotifier>();
builder.Services.AddHostedService<FactResultsConsumer>();

builder.Services.AddControllers().AddJsonOptions(options =>
    // New domain entities (D1/D3/D4/D6/D8) expose their enums directly on DTOs instead of hand-rolled
    // string conversion (as PrincipalType/MemberType still use, kept for backward compatibility) —
    // camelCase to match every other property.
    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseSerilogRequestLogging();

app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors();

app.UseMiddleware<TokenAuthenticationMiddleware>();
// Журнал действий: после разбора токена (кто) и до авторизации (отказы в праве тоже пишутся).
app.UseMiddleware<AuditMiddleware>();
app.UseAuthorization();

app.MapControllers();

app.Run();
