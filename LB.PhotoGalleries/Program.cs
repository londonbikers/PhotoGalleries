using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.SystemConsole.Themes;
using System;

namespace LB.PhotoGalleries;

public class Program
{
    public static void Main(string[] args)
    {
        Log.Logger = new LoggerConfiguration()
            .Enrich.FromLogContext()
            .Enrich.WithMachineName()
            .MinimumLevel.Debug()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
            .MinimumLevel.Override("Microsoft.Hosting.Lifetime", LogEventLevel.Information)
            .MinimumLevel.Override("System", LogEventLevel.Information)
            .MinimumLevel.Override("Microsoft.AspNetCore.Authentication", LogEventLevel.Information)
            .WriteTo.Async(a => a.Console(theme: AnsiConsoleTheme.Code))
            .CreateBootstrapLogger();

        try
        {
            Log.Information("Starting web host");
            CreateHostBuilder(args).Build().Run();
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Host terminated unexpectedly");
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }

    public static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .UseSerilog((context, services, configuration) =>
            {
                configuration
                    .ReadFrom.Configuration(context.Configuration)
                    .ReadFrom.Services(services)
                    .Enrich.FromLogContext()
                    .WriteTo.Console()
                    .WriteTo.Debug();

                // Only add ApplicationInsights if configured
                var aiConnectionString = context.Configuration["ApplicationInsights:ConnectionString"];
                var aiInstrumentationKey = context.Configuration["ApplicationInsights:InstrumentationKey"];
                if (!string.IsNullOrEmpty(aiConnectionString) || !string.IsNullOrEmpty(aiInstrumentationKey))
                {
                    var telemetryConfig = new TelemetryConfiguration();
                    if (!string.IsNullOrEmpty(aiConnectionString))
                        telemetryConfig.ConnectionString = aiConnectionString;
                    else if (!string.IsNullOrEmpty(aiInstrumentationKey))
                        telemetryConfig.ConnectionString = $"InstrumentationKey={aiInstrumentationKey}";

                    configuration.WriteTo.ApplicationInsights(telemetryConfig, TelemetryConverter.Traces);
                }
            })
            .ConfigureWebHostDefaults(webBuilder =>
            {
                webBuilder.UseStartup<Startup>();
            });
}