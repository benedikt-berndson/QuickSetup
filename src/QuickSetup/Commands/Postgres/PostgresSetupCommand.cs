using System.Data.Common;
using Dapper;
using DotMake.CommandLine;
using InterpolatedSql.Dapper;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using QuickSetup.Common;
using QuickSetup.Common.Abstractions;
using QuickSetup.Models;

namespace QuickSetup.Commands.Postgres;

[CliCommand(Description = "Setup new databases, users, schemas and default privileges", Parent = typeof(RootCommand),
  Alias = "pg-setup")]
public class PostgresSetupCommand
{
  private readonly IServiceProvider _serviceProvider;
  private readonly QuickSetupSettings _quickSetupSettings;
  private readonly ITracingService _tracingService;

  public PostgresSetupCommand(IServiceProvider serviceProvider, QuickSetupSettings quickSetupSettings,
    ITracingService tracingService)
  {
    _tracingService = tracingService;
    _serviceProvider = serviceProvider;
    _quickSetupSettings = quickSetupSettings;
  }

  [CliArgument(Description = "Define which connection string from settings.toml to use")]
  public string ConnectionStringName { get; set; } = null!;


  [CliOption(Description = "Drop existing databases")]
  public bool DropDatabases { get; set; }

  [CliOption(Description = "Drop existing schemas")]
  public bool DropSchemas { get; set; }

  [CliOption(Description = "Drop existing schemas")]
  public bool DropUsers { get; set; }


  public async Task RunAsync()
  {
    _tracingService.WriteStartLog(this);
    var contexts = GetContexts();
    await using var adminConnection = _serviceProvider.GetRequiredKeyedService<NpgsqlConnection>(ConnectionStringName);

    HandleDatabaseObjectRemovalAsync(adminConnection, contexts);
  }

  private void HandleDatabaseObjectRemovalAsync(DbConnection connection, List<SetupContext> contexts)
  {
    if (!DropDatabases && !DropSchemas && !DropUsers) return;

    _tracingService.WriteDatabaseObjectRemovalStartLog();
    _tracingService.WriteCodeBlockMarker();

    var existingDatabases = connection.Query<string>("SELECT datname FROM pg_database").ToHashSet();

    foreach (var sql in from ctx in contexts
                        where DropSchemas && existingDatabases.Contains(ctx.Database)
                        select $"DROP SCHEMA IF EXISTS {ctx.Database}.{ctx.Schema} CASCADE")
    {
      _tracingService.WriteLine($"{sql}");
      connection.Execute(sql);
    }

    foreach (var cmd in from ctx in contexts
                        where DropDatabases
                        select connection.SqlBuilder($"DROP DATABASE IF EXISTS {ctx.Database}").Build())
    {
      _tracingService.WriteLine($"{cmd.Sql} | {string.Join(", ", cmd.SqlParameters.ToString())}");
      cmd.Execute();
    }

    foreach (var ctx in contexts.Where(ctx => DropUsers))
    {
      connection.SqlBuilder($"DROP USER IF EXISTS {ctx.MachineUser.Name}").Execute();
      connection.SqlBuilder($"DROP USER IF EXISTS {ctx.AppUser.Name}").Execute();
      connection.SqlBuilder($"DROP USER IF EXISTS {ctx.ReadonlyUser.Name}").Execute();
    }

    _tracingService.WriteCodeBlockMarker();
  }

  private void HandleUserCreation(DbConnection connection, List<SetupContext> contexts)
  {
    foreach (var ctx in contexts)
    {
      connection.SqlBuilder($"CREATE USER {ctx.MachineUser.Name} WITH ENCRYPTED PASSWORD {ctx.MachineUser.Password}")
        .Execute();
      connection.SqlBuilder($"CREATE USER {ctx.MachineUser.Name} WITH ENCRYPTED PASSWORD {ctx.MachineUser.Password}")
        .Execute();
      connection.SqlBuilder($"CREATE USER {ctx.MachineUser.Name} WITH ENCRYPTED PASSWORD {ctx.MachineUser.Password}")
        .Execute();
    }
  }


  private List<SetupContext> GetContexts()
    => _quickSetupSettings.DatabaseSchemaDefinitions
      .Select(databaseSchemaPair =>
      {
        var databaseName = databaseSchemaPair.Key;
        var contexts = databaseSchemaPair.Value
          .Select(schemaName => new SetupContext(databaseName,
            schemaName,
            new DbUser($"{schemaName}{_quickSetupSettings.MachineUserNameMiddlePart}{databaseName}",
              PasswordFactory.GetNew()),
            new DbUser($"{schemaName}{_quickSetupSettings.AppUserNameMiddlePart}{databaseName}",
              PasswordFactory.GetNew()),
            new DbUser($"{schemaName}{_quickSetupSettings.ReadonlyUserNameMiddlePart}{databaseName}",
              PasswordFactory.GetNew())));

        return contexts;
      })
      .SelectMany(x => x)
      .ToList();
}