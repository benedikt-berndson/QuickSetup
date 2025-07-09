// See https://aka.ms/new-console-template for more information

using DotMake.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using QuickSetup.Commands.Postgres;
using QuickSetup.Common;
using QuickSetup.Common.Abstractions;
using Serilog;
using RootCommand = QuickSetup.Commands.RootCommand;

Log.Logger = new LoggerConfiguration()
  .WriteTo.Console()
  .MinimumLevel.Information()
  .CreateLogger();

Cli.Ext.ConfigureServices(services =>
{
  services.AddSingleton<ISettingsProvider, SettingsProvider>();
  services.AddSingleton<IPostgresRepository, PostgresRepository>();
});

try
{
  Cli.Run<RootCommand>(args);
}
catch (Exception e)
{
  Log.Error( "{Message}", e.Message);
}