// See https://aka.ms/new-console-template for more information

using DotMake.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using QuickSetup;
using QuickSetup.Commands;
using QuickSetup.Common;
using QuickSetup.Common.Abstractions;
using Serilog;
using Tomlyn;

Log.Logger = new LoggerConfiguration()
  .WriteTo.Console()
  .MinimumLevel.Information()
  .CreateLogger();

var parseResult = Cli.Parse<RootCommand>(args);
var settingsFile = parseResult.GetValue<string>("settings-file");
if (string.IsNullOrEmpty(settingsFile))
{
  Log.Fatal("Settings file not found");
  return;
}

Console.WriteLine($"USING SETTINGS FILE: {settingsFile}");

var text = File.ReadAllText(settingsFile);
var settings = Toml.ToModel<QuickSetupSettings>(text);

Cli.Ext.ConfigureServices(services =>
{
  services.AddSingleton(settings);
  services.AddSingleton<ITracingService, TracingService>();
  foreach (var pair in settings.ConnectionStrings)
  {
    services.AddMultiHostNpgsqlSlimDataSource(pair.Value, serviceKey: pair.Key);
  }
});

Cli.Run<RootCommand>(args);