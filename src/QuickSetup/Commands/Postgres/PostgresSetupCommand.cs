using System.Data.Common;
using System.Text;
using Dapper;
using DotMake.CommandLine;
using QuickSetup.Common;
using QuickSetup.Common.Abstractions;
using QuickSetup.Models;

namespace QuickSetup.Commands.Postgres;

[CliCommand(Description = "Setup new databases, users, schemas and default privileges", Parent = typeof(RootCommand),
  Alias = "pg-setup")]
public class PostgresSetupCommand : AbstractSettingsRequiringCommand, ICliRunAsyncWithContext
{
  private readonly ITracingService _tracingService;
  private readonly IDbConnectionFactory _dbConnectionFactory;

  public PostgresSetupCommand(ISettingsFactory settingsFactoryInstance,
    ITracingService tracingService, IDbConnectionFactory dbConnectionFactory) : base(settingsFactoryInstance)
  {
    _tracingService = tracingService;
    _dbConnectionFactory = dbConnectionFactory;
  }

  [CliArgument(Description = "Define which connection string from settings.toml to use")]
  public string ConnectionStringName { get; set; } = null!;

  [CliOption(Description = "Drop existing databases")]
  public bool DropDatabases { get; set; }

  [CliOption(Description = "Drop existing schemas")]
  public bool DropSchemas { get; set; }

  [CliOption(Description = "Drop existing users")]
  public bool DropUsers { get; set; }


  public async Task RunAsync(CliContext cliContext)
  {
    SettingsFactoryInstance.Init(SettingsFile);
    _tracingService.WriteStartLog(this);
    var contexts = GetContexts();
    await using var adminConnection = _dbConnectionFactory.GetPostgresConnection(ConnectionStringName);

    HandleDatabaseObjectRemovalAsync(adminConnection, contexts);
    HandleUserCreation(adminConnection, contexts);
  }

  private void HandleDatabaseObjectRemovalAsync(DbConnection connection, List<SetupContext> contexts)
  {
    if (!DropDatabases && !DropSchemas && !DropUsers) return;

    _tracingService.WriteDatabaseObjectRemovalStartLog();
    _tracingService.WriteCodeBlockMarker();

    var existingDatabases = connection.Query<string>("SELECT datname FROM pg_database").ToHashSet();

    var queries = new StringBuilder();
    if (DropSchemas)
    {
      var dropSchemaStatements = contexts
        .Where(x => existingDatabases.Contains(x.Database))
        .Select(x => $"DROP SCHEMA IF EXISTS {x.Database}.{x.Schema} CASCADE;");
      queries.AppendLine(string.Join("\n", dropSchemaStatements));
    }

    if (DropDatabases)
    {
      var dropDatabaseStatements = contexts.DistinctBy(x => x.Database)
        .Select(x => $"DROP DATABASE IF EXISTS {x.Database};");
      queries.AppendLine(string.Join("\n", dropDatabaseStatements));
    }

    if (DropUsers)
    {
      var dropUserStatements = contexts.Select(x => x.GetUsers())
        .SelectMany(x => x)
        .Select(GetDropUserStatement);
      queries.AppendLine(string.Join("\n", dropUserStatements));
    }

    var stmts = queries.ToString();
    _tracingService.WriteLine(stmts);
    connection.Execute(stmts);
    _tracingService.WriteCodeBlockMarker();
  }

  private void HandleUserCreation(DbConnection connection, List<SetupContext> contexts)
  {
    var queries = new StringBuilder();
    var createStatements = contexts
      .Select(x => x.GetUsers())
      .SelectMany(x => x)
      .Select(x => $"CREATE USER {x.Name} WITH ENCRYPTED PASSWORD '{x.Password}';");
    var sql = queries.AppendLine(string.Join("\n", createStatements)).ToString();

    _tracingService.WriteCodeBlockMarker();
    _tracingService.WriteLine(sql);
    _tracingService.WriteCodeBlockMarker();

    connection.Execute(sql);
  }


  private List<SetupContext> GetContexts()
  {
    var settings = SettingsFactoryInstance.GetInstance();
    var result = settings.DatabaseSchemaDefinitions
      .Select(databaseSchemaPair =>
      {
        var databaseName = databaseSchemaPair.Key;
        var contexts = databaseSchemaPair.Value
          .Select(schemaName =>
          {
            var owner = User.From(settings.OwningUserTemplate.Name
                .Replace("{{SCHEMA}}", schemaName, StringComparison.InvariantCultureIgnoreCase).Replace("{{DATABASE}}",
                  databaseName, StringComparison.InvariantCultureIgnoreCase),
              settings.OwningUserTemplate.GeneratePassword
                ? PasswordFactory.GetNew()
                : settings.OwningUserTemplate.Password);

            var readWriteUsers = settings.ReadWriteUserTemplates
              .Select(x => User.From(x.Name
                  .Replace("{{SCHEMA}}", schemaName, StringComparison.InvariantCultureIgnoreCase)
                  .Replace("{{DATABASE}}", databaseName, StringComparison.InvariantCultureIgnoreCase),
                x.GeneratePassword ? PasswordFactory.GetNew() : x.Password))
              .ToList();

            var readOnlyUsers = settings.ReadonlyUserTemplates
              .Select(x => User.From(x.Name
                  .Replace("{{SCHEMA}}", schemaName, StringComparison.InvariantCultureIgnoreCase)
                  .Replace("{{DATABASE}}", databaseName, StringComparison.InvariantCultureIgnoreCase),
                x.GeneratePassword ? PasswordFactory.GetNew() : x.Password))
              .ToList();

            return new SetupContext(databaseName, schemaName, owner, readWriteUsers, readOnlyUsers);
          });

        return contexts;
      })
      .SelectMany(x => x)
      .ToList();

    return result;
  }

  private static string GetDropUserStatement(User user)
    => $"DROP USER IF EXISTS {user.Name};";
}