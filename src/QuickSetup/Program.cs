// See https://aka.ms/new-console-template for more information

using Microsoft.Extensions.DependencyInjection;
using QuickSetup;
using QuickSetup.Commands.Initialize;
using QuickSetup.Commands.Postgres;
using QuickSetup.Common;
using QuickSetup.Common.Abstractions;
using Spectre.Console.Cli;

var serviceCollection = new ServiceCollection();
serviceCollection.AddSingleton<ISettingsProvider, SettingsProvider>();
serviceCollection.AddSingleton<IPostgresRepository, PostgresRepository>();

var registrar = new QuickSetupTypeRegistrar(serviceCollection);

var app = new CommandApp(registrar);
app.Configure(config =>
{
  config
    .AddCommand<PgSetupCommand>(PgSetupCommand.CommandName)
    .WithDescription("Setup new databases, users, schemas and default privileges");
  config.AddCommand<InitializeUserSettingsCommand>("init");
});

return app.Run(args);
