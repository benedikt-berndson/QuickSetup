using System.Text;
using Cottle;
using DotMake.CommandLine;
using Npgsql;
using QuickSetup.Common;
using QuickSetup.Common.Abstractions;
using QuickSetup.Models;
using Serilog;

namespace QuickSetup.Commands.Postgres;

[CliCommand(Description = "Setup new databases, users, schemas and default privileges", Parent = typeof(RootCommand))]
public class PgSetupCommand : AbstractSettingsRequiringCommand, ICliRunWithContext
{
  private readonly IPostgresRepository _postgresRepository;
  // private readonly PgSetupExecLog _log;

  public PgSetupCommand(ISettingsProvider settingsProvider, IPostgresRepository postgresRepository) :
    base(settingsProvider)
  {
    _postgresRepository = postgresRepository;
  }

  [CliArgument(Description = "Define which connection string from settings.toml to use")]
  public string ConnectionStringName { get; set; } = null!;

  [CliOption(Description = "Drop existing databases")]
  public bool DropDatabases { get; set; }

  [CliOption(Description = "Drop existing schemas")]
  public bool DropSchemas { get; set; }

  public void Run(CliContext cliContext)
  {
    Log.Information("Running {CommandName}", GetType().Name);
    cliContext.Output.WriteLine($"Start {nameof(PgSetupCommand)}");
    Log.Information("Reading settings file: {SettingsFile}", SettingsFile);
    cliContext.Output.WriteLine($"Reading settings file: {SettingsFile}");
    SettingsProvider.Init(SettingsFile);
    var settings = SettingsProvider.GetSettings();

    foreach (var ctx in settings.DatabaseToSchemaMap.Select(entry =>
      GetSetupContext(cliContext, settings, entry.Key, entry.Value)))
    {
      try
      {
        Log.Information("  Running setup for {Database}.{Schema}", ctx.SetupModel.Database, ctx.SetupModel.Schema);
        cliContext.Output.WriteLine($"\n  Running setup for {ctx.SetupModel.Database}.{ctx.SetupModel.Schema}");
        ctx.Log.AddSettingsTable(this, ctx);

        cliContext.Output.WriteLine("  Running deletion of database objects");
        DeleteDatabaseObjects(ctx);

        cliContext.Output.WriteLine("  Running user creation");
        CreateUsers(ctx);

        cliContext.Output.WriteLine("  Running database creation");
        CreateDatabase(ctx);

        cliContext.Output.WriteLine("  Running schema creation");
        CreateSchema(ctx);

        cliContext.Output.WriteLine("  Grant usages on schema");
        GrantUsageOnSchema(ctx);

        cliContext.Output.WriteLine("  Grant default privileges");
        GrantDefaultPrivileges(ctx);

        cliContext.Output.WriteLine("  Write audit log");
        WriteAuditLog(ctx);
      }
      catch (Exception e)
      {
        ctx.Log.AddException(e);
        WriteAuditLog(ctx);
        throw;
      }
    }
  }

  private void DeleteDatabaseObjects(DbSetupContext ctx)
  {
    if (!DropDatabases && !DropSchemas)
    {
      ctx.CliContext.Output.WriteLine("    Nothing to do");
      ctx.Log.AddDropDatabaseStatementSkipped();
      return;
    }

    var existingDatabases = _postgresRepository.GetDatabaseNames(ConnectionStringName);
    var dbExists = existingDatabases.Contains(ctx.SetupModel.Database);
    if (DropSchemas && dbExists)
    {
      ctx.CliContext.Output.WriteLine($"    Dropping schema: {ctx.SetupModel.Schema}");
      var dropSchemaStatement = $"DROP SCHEMA IF EXISTS {ctx.SetupModel.Schema} CASCADE";
      ctx.Log.AddDropSchemaStatement(dropSchemaStatement);
      _postgresRepository.ExecuteAsDbScopedAdmin(ConnectionStringName, ctx.SetupModel.Database,
        dropSchemaStatement);
    }
    else
    {
      var skipDropSchemaMsg = $"Skipping dropping schema. --drop-schemas={DropSchemas} && dbExists={dbExists}";
      ctx.CliContext.Output.WriteLine($"    {skipDropSchemaMsg}");
      ctx.Log.AddDropSchemaStatement(skipDropSchemaMsg);
    }

    if (DropDatabases && dbExists)
    {
      ctx.CliContext.Output.WriteLine($"    Dropping database: {ctx.SetupModel.Database}");
      var dropDatabaseStatement = $"DROP DATABASE IF EXISTS {ctx.SetupModel.Database}";
      ctx.Log.AddDropDatabaseStatement(dropDatabaseStatement);
      _postgresRepository.ExecuteAsRootAdmin(ConnectionStringName, dropDatabaseStatement);
    }
    else
    {
      var skipDropDatabaseMsg = $"Skipping dropping database. --drop-databases={DropDatabases} && dbExists={dbExists}";
      ctx.CliContext.Output.WriteLine($"    {skipDropDatabaseMsg}");
      ctx.Log.AddDropDatabaseStatement(skipDropDatabaseMsg);
    }

    var dropUserStatements = string.Join(";\n", ctx.SetupModel.GetUsers()
      .Select(x => $"DROP USER IF EXISTS {x.Name}"));
    ctx.CliContext.Output.WriteLine(
      $"    Dropping users: {string.Join(", ", ctx.SetupModel.GetUsers().Select(x => x.Name))}");
    ctx.Log.AddDropUsersStatement(dropUserStatements);
    _postgresRepository.ExecuteAsRootAdmin(ConnectionStringName, dropUserStatements);
    ;
  }

  private void CreateUsers(DbSetupContext ctx)
  {
    var createStatements = string.Join(";\n", ctx.SetupModel.GetUsers()
      .Select(x => $"CREATE USER {x.Name} WITH ENCRYPTED PASSWORD '{x.Password}'"));

    ctx.CliContext.Output.WriteLine(
      $"    Create users: {string.Join(", ", ctx.SetupModel.GetUsers().Select(x => x.Name))}");
    ctx.Log.AddUserCreationStatements(createStatements);
    _postgresRepository.ExecuteAsRootAdmin(ConnectionStringName, createStatements);
  }

  private static void WriteAuditLog(DbSetupContext ctx)
  {
    var path = Path.Join(Directory.GetCurrentDirectory(), "Commands", "Postgres", "setup_log_template.md");
    var template = File.ReadAllText(path);
    var config = new DocumentConfiguration
    {
      NoOptimize = true,
      Trimmer = DocumentConfiguration.TrimNothing
    };
    var document = Document.CreateDefault(template, config).DocumentOrThrow;

    var renderContext = Context.CreateBuiltin(ctx.Log.Log);
    var rendered = document.Render(renderContext);

    var filename =
      $"{DateTimeOffset.Now:yyyy-MM-dd_HH-mm-ss}_audit_log__{ctx.SetupModel.Database}__{ctx.SetupModel.Schema}.md";
    var outputPath = Path.IsPathFullyQualified(ctx.Settings.AuditLogPath)
      ? Path.Join(ctx.Settings.AuditLogPath, filename)
      : Path.Join(Directory.GetCurrentDirectory(), ctx.Settings.AuditLogPath, filename);

    var dir = Path.GetDirectoryName(outputPath);
    if (!Directory.Exists(outputPath))
    {
      Directory.CreateDirectory(dir!);
    }

    ctx.CliContext.Output.WriteLine($"    Writing audit log to: {outputPath}");
    File.WriteAllText(outputPath, rendered);
  }

  private void CreateDatabase(DbSetupContext ctx)
  {
    // CREATE DATABASE
    var existingDatabases = _postgresRepository.GetDatabaseNames(ConnectionStringName);
    if (!existingDatabases.Contains(ctx.SetupModel.Database))
    {
      var createDatabaseStatement = $"""
                                     CREATE DATABASE {ctx.SetupModel.Database}
                                     WITH
                                     OWNER {ctx.Settings.CreateDatabaseMetadata.Owner}
                                     ENCODING = {ctx.Settings.CreateDatabaseMetadata.Encoding}
                                     TABLESPACE = {ctx.Settings.CreateDatabaseMetadata.Tablespace}
                                     CONNECTION LIMIT = {ctx.Settings.CreateDatabaseMetadata.ConnectionLimit};
                                     """;
      ctx.CliContext.Output.WriteLine($"    Creating database: {ctx.SetupModel.Database}");
      ctx.Log.AddDatabaseCreationStatement(createDatabaseStatement);
      _postgresRepository.ExecuteAsRootAdmin(ConnectionStringName, createDatabaseStatement);
      return;
    }

    var msg = $"Database {ctx.SetupModel.Database} already exists - skipping creation";
    ctx.CliContext.Output.WriteLine($"    {msg}");
    ctx.Log.AddDatabaseCreationStatement(msg);
  }

  private void CreateSchema(DbSetupContext ctx)
  {
    var existingSchemas = _postgresRepository.GetSchemaNames(ConnectionStringName, ctx.SetupModel.Database);
    if (!existingSchemas.Contains(ctx.SetupModel.Schema.ToLower()))
    {
      var createSchemaStatement = $"CREATE SCHEMA {ctx.SetupModel.Schema};";

      ctx.CliContext.Output.WriteLine($"    Creating schema: {ctx.SetupModel.Schema}");
      ctx.Log.AddSchemaCreationStatement(createSchemaStatement);
      _postgresRepository.ExecuteAsDbScopedAdmin(ConnectionStringName, ctx.SetupModel.Database, createSchemaStatement);
      return;
    }

    var msg = $"Schema {ctx.SetupModel.Schema} already exists - skipping creation";
    ctx.CliContext.Output.WriteLine($"    {msg}");
    ctx.Log.AddSchemaCreationStatement(msg);
  }

  private void GrantUsageOnSchema(DbSetupContext ctx)
  {
    var usages = new StringBuilder();

    usages.AppendLine($"GRANT CREATE, USAGE ON SCHEMA {ctx.SetupModel.Schema} TO {ctx.SetupModel.OwningUser.Name};");
    foreach (var user in ctx.SetupModel.ReadWriteUsers)
      usages.AppendLine($"GRANT USAGE ON SCHEMA {ctx.SetupModel.Schema} TO {user.Name};");
    foreach (var user in ctx.SetupModel.ReadonlyUsers)
      usages.AppendLine($"GRANT USAGE ON SCHEMA {ctx.SetupModel.Schema} TO {user.Name};");

    var usagesSql = usages.ToString().TrimEnd();
    ctx.Log.AddGrantUsageStatement(usagesSql);

    _postgresRepository.ExecuteAsDbScopedAdmin(ConnectionStringName, ctx.SetupModel.Database, usagesSql);
  }

  private void GrantDefaultPrivileges(DbSetupContext ctx)
  {
    var s = new StringBuilder();
    s.AppendLine(
      $"ALTER DEFAULT PRIVILEGES IN SCHEMA {ctx.SetupModel.Schema} GRANT ALL ON TABLES TO {ctx.SetupModel.OwningUser.Name};");
    s.AppendLine(
      $"ALTER DEFAULT PRIVILEGES IN SCHEMA {ctx.SetupModel.Schema} GRANT ALL ON SEQUENCES TO {ctx.SetupModel.OwningUser.Name};");
    s.AppendLine(
      $"ALTER DEFAULT PRIVILEGES IN SCHEMA {ctx.SetupModel.Schema} GRANT ALL ON FUNCTIONS TO {ctx.SetupModel.OwningUser.Name};");
    s.AppendLine();

    ctx.CliContext.Output.WriteLine($"    Granting default privileges to {ctx.SetupModel.OwningUser.Name}");
    foreach (var user in ctx.SetupModel.ReadWriteUsers)
    {
      s.AppendLine(
        $"ALTER DEFAULT PRIVILEGES IN SCHEMA {ctx.SetupModel.Schema} GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO {user.Name};");
      s.AppendLine(
        $"ALTER DEFAULT PRIVILEGES IN SCHEMA {ctx.SetupModel.Schema} GRANT USAGE, SELECT ON SEQUENCES TO {user.Name};");
      s.AppendLine(
        $"ALTER DEFAULT PRIVILEGES IN SCHEMA {ctx.SetupModel.Schema} GRANT EXECUTE ON FUNCTIONS TO {user.Name};");
      s.AppendLine();
      ctx.CliContext.Output.WriteLine($"    Granting default privileges to {user.Name}");
    }

    foreach (var user in ctx.SetupModel.ReadonlyUsers)
    {
      s.AppendLine(
        $"ALTER DEFAULT PRIVILEGES IN SCHEMA {ctx.SetupModel.Schema} GRANT SELECT ON TABLES TO {user.Name};");
      s.AppendLine();
      ctx.CliContext.Output.WriteLine($"    Granting default privileges to {user.Name}");
    }

    var defaultPrivilegesSql = s.ToString().TrimEnd();
    ctx.Log.AddGrantDefaultPrivilegesStatements(defaultPrivilegesSql);

    _postgresRepository.ExecuteAsOwningUser(ConnectionStringName, ctx.SetupModel.Database, ctx.SetupModel.Schema,
      ctx.SetupModel.OwningUser.Name, ctx.SetupModel.OwningUser.Password, defaultPrivilegesSql);
  }


  private DbSetupContext GetSetupContext(CliContext cliContext, QuickSetupSettings settings, string database,
    string schema)
  {
    var owner = User.From(settings.OwningUserTemplate.Name.ReplaceUserTokens(database, schema),
      settings.OwningUserTemplate.GeneratePassword
        ? PasswordFactory.GetNew()
        : settings.OwningUserTemplate.Password);

    var readWriteUsers = settings.ReadWriteUserTemplates
      .Select(x => User.From(x.Name.ReplaceUserTokens(database, schema),
        x.GeneratePassword ? PasswordFactory.GetNew() : x.Password))
      .ToList();

    var readOnlyUsers = settings.ReadonlyUserTemplates
      .Select(x => User.From(x.Name.ReplaceUserTokens(database, schema),
        x.GeneratePassword ? PasswordFactory.GetNew() : x.Password))
      .ToList();

    var setupModel = new SetupModel(database, schema, owner, readWriteUsers, readOnlyUsers);

    var connectionStrings = setupModel.GetUsers()
      .Select(u => new NpgsqlConnectionStringBuilder(settings.ConnectionStrings[ConnectionStringName])
      {
        Username = u.Name,
        Password = u.Password,
        Database = database,
        SearchPath = schema
      }.ToString());
    var connectionStringsText = string.Join("\n", connectionStrings);
    var auditLog = new PgSetupAuditLog(settings, cliContext);
    auditLog.AddConnectionStrings(connectionStringsText);

    return new DbSetupContext(cliContext, setupModel, settings, auditLog);
  }
}