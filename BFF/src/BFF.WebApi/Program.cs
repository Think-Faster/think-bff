using BFF.Application;
using BFF.WebApi.Extensions;
using BFF.WebApi.Middleware;
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

builder.Services.AddControllers();
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
app.UseAuthorization();

app.MapControllers();

app.Run();
