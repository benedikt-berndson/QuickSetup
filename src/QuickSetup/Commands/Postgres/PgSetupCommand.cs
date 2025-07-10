using System.Text;
using Cottle;
using Npgsql;
using QuickSetup.Common;
using QuickSetup.Common.Abstractions;
using QuickSetup.Models;
using Spectre.Console;
using Spectre.Console.Cli;

namespace QuickSetup.Commands.Postgres;

// [CliCommand(Description = "Setup new databases, users, schemas and default privileges", Parent = typeof(RootCommand))]
public sealed class PgSetupCommand : Command<PgSetupCommandSettings>
{
  private readonly ISettingsProvider _settingsProvider;
  private readonly IPostgresRepository _postgresRepository;

  public PgSetupCommand(ISettingsProvider settingsProvider, IPostgresRepository postgresRepository)
  {
    _settingsProvider = settingsProvider;
    _postgresRepository = postgresRepository;
  }

  public override int Execute(CommandContext context, PgSetupCommandSettings cs)
  {
    AnsiConsole.MarkupLine("[bold]STARTING DATABASE SETUP[/]");
    AnsiConsole.MarkupLineInterpolated($"Reading settings file: {cs.SettingsFile}");


    _settingsProvider.Init(cs.SettingsFile);
    var settings = _settingsProvider.GetSettings();

    var root = new Tree("\nSTART ITERATION STEP");
    foreach (var ctx in settings.DatabaseToSchemaMap.Select(entry =>
      GetSetupContext(cs, settings, entry.Key, entry.Value)))
    {
      try
      {
        root.AddNode($"RUNNING SETUP FOR {ctx.SetupModel.Database}.{ctx.SetupModel.Schema}");
        ctx.Log.AddSettingsTable(ctx);

        var n1 = root.AddNode("Running deletion of database objects");
        DeleteDatabaseObjects(ctx, n1);

        var n2 = root.AddNode("Running user creation");
        CreateUsers(ctx, n2);

        var n3 = root.AddNode("Running database creation");
        CreateDatabase(ctx, n3);

        var n4 = root.AddNode("Running schema creation");
        CreateSchema(ctx, n4);

        root.AddNode("Grant usages on schema");
        GrantUsageOnSchema(ctx);

        var n6 = root.AddNode("Grant default privileges");
        GrantDefaultPrivileges(ctx, n6);

        var n7 = root.AddNode("Write audit log");
        WriteAuditLog(ctx, n7);

        AnsiConsole.Write(root);
        root = new Tree("\nSTART ITERATION STEP");
      }
      catch (Exception e)
      {
        ctx.Log.AddException(e);
        var n = root.AddNode("[red]Writing partial audit log after error[/]");
        WriteAuditLog(ctx, n);
        AnsiConsole.Write(root);
        AnsiConsole.WriteException(e);
        return 1;
      }
    }

    return 0;
  }

  private void DeleteDatabaseObjects(PgSetupContext ctx, TreeNode n)
  {
    if (ctx.CommandSettings is
    {
      DropDatabases: false,
      DropSchemas: false
    })
    {
      n.AddNode("Nothing to do");
      ctx.Log.AddDropDatabaseStatementSkipped();
      return;
    }

    var existingDatabases = _postgresRepository.GetDatabaseNames(ctx.CommandSettings.ConnectionStringName);
    var dbExists = existingDatabases.Contains(ctx.SetupModel.Database);
    if (ctx.CommandSettings.DropSchemas && dbExists)
    {
      n.AddNode($"Dropping schema: {ctx.SetupModel.Schema}");
      var dropSchemaStatement = $"DROP SCHEMA IF EXISTS {ctx.SetupModel.Schema} CASCADE";
      ctx.Log.AddDropSchemaStatement(dropSchemaStatement);
      _postgresRepository.ExecuteAsDbScopedAdmin(ctx.CommandSettings.ConnectionStringName, ctx.SetupModel.Database,
        dropSchemaStatement);
    }
    else
    {
      var skipDropSchemaMsg =
        $"Skipping dropping schema. --drop-schemas={ctx.CommandSettings.DropSchemas} && dbExists={dbExists}";
      n.AddNode(skipDropSchemaMsg);
      ctx.Log.AddDropSchemaStatement(skipDropSchemaMsg);
    }

    if (ctx.CommandSettings.DropDatabases && dbExists)
    {
      n.AddNode($"Dropping database: {ctx.SetupModel.Database}");
      var dropDatabaseStatement = $"DROP DATABASE IF EXISTS {ctx.SetupModel.Database}";
      ctx.Log.AddDropDatabaseStatement(dropDatabaseStatement);
      _postgresRepository.ExecuteAsRootAdmin(ctx.CommandSettings.ConnectionStringName, dropDatabaseStatement);
    }
    else
    {
      var skipDropDatabaseMsg =
        $"Skipping dropping database. --drop-databases={ctx.CommandSettings.DropDatabases} && dbExists={dbExists}";
      n.AddNode(skipDropDatabaseMsg);
      ctx.Log.AddDropDatabaseStatement(skipDropDatabaseMsg);
    }

    var dropUserStatements = string.Join(";\n", ctx.SetupModel.GetUsers()
      .Select(x => $"DROP USER IF EXISTS {x.Name}"));
    n.AddNode($"Dropping users: {string.Join(", ", ctx.SetupModel.GetUsers().Select(x => x.Name))}");
    ctx.Log.AddDropUsersStatement(dropUserStatements);
    _postgresRepository.ExecuteAsRootAdmin(ctx.CommandSettings.ConnectionStringName, dropUserStatements);
  }

  private void CreateUsers(PgSetupContext ctx, TreeNode n)
  {
    var createStatements = string.Join(";\n", ctx.SetupModel.GetUsers()
      .Select(x => $"CREATE USER {x.Name} WITH ENCRYPTED PASSWORD '{x.Password}'"));

    n.AddNode($"Create users: {string.Join(", ", ctx.SetupModel.GetUsers().Select(x => x.Name))}");
    ctx.Log.AddUserCreationStatements(createStatements);
    _postgresRepository.ExecuteAsRootAdmin(ctx.CommandSettings.ConnectionStringName, createStatements);
  }

  private static void WriteAuditLog(PgSetupContext ctx, TreeNode n)
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

    var auditLogOutputDir = string.IsNullOrEmpty(ctx.SettingsFile.AuditLogPath)
      ? Path.GetDirectoryName(Path.GetFullPath(ctx.CommandSettings.SettingsFile))
      : ctx.SettingsFile.AuditLogPath;

    var filename =
      $"{DateTimeOffset.Now:yyyy-MM-dd_HH-mm-ss}_audit_log__{ctx.SetupModel.Database}__{ctx.SetupModel.Schema}.md";
    var file = Path.IsPathFullyQualified(auditLogOutputDir!)
      ? Path.Join(auditLogOutputDir, filename)
      : Path.Combine(Directory.GetCurrentDirectory(), Path.GetFullPath(ctx.SettingsFile.AuditLogPath), filename);

    var dir = Path.GetDirectoryName(file);
    if (!Directory.Exists(file))
    {
      Directory.CreateDirectory(dir!);
    }

    n.AddNode($"Writing audit log to: {file}");
    File.WriteAllText(file, rendered);
  }

  private void CreateDatabase(PgSetupContext ctx, TreeNode n)
  {
    // CREATE DATABASE
    var existingDatabases = _postgresRepository.GetDatabaseNames(ctx.CommandSettings.ConnectionStringName);
    if (!existingDatabases.Contains(ctx.SetupModel.Database))
    {
      var createDatabaseStatement = $"""
                                     CREATE DATABASE {ctx.SetupModel.Database}
                                     WITH
                                     OWNER {ctx.SettingsFile.CreateDatabaseMetadata.Owner}
                                     ENCODING = {ctx.SettingsFile.CreateDatabaseMetadata.Encoding}
                                     TABLESPACE = {ctx.SettingsFile.CreateDatabaseMetadata.Tablespace}
                                     CONNECTION LIMIT = {ctx.SettingsFile.CreateDatabaseMetadata.ConnectionLimit};
                                     """;
      n.AddNode($"Creating database: {ctx.SetupModel.Database}");
      ctx.Log.AddDatabaseCreationStatement(createDatabaseStatement);
      _postgresRepository.ExecuteAsRootAdmin(ctx.CommandSettings.ConnectionStringName, createDatabaseStatement);
      return;
    }

    var msg = $"Database {ctx.SetupModel.Database} already exists - skipping creation";
    n.AddNode(msg);
    ctx.Log.AddDatabaseCreationStatement(msg);
  }

  private void CreateSchema(PgSetupContext ctx, TreeNode n)
  {
    var existingSchemas =
      _postgresRepository.GetSchemaNames(ctx.CommandSettings.ConnectionStringName, ctx.SetupModel.Database);
    if (!existingSchemas.Contains(ctx.SetupModel.Schema.ToLower()))
    {
      var createSchemaStatement = $"CREATE SCHEMA {ctx.SetupModel.Schema};";

      n.AddNode($"Creating schema: {ctx.SetupModel.Schema}");
      ctx.Log.AddSchemaCreationStatement(createSchemaStatement);
      _postgresRepository.ExecuteAsDbScopedAdmin(ctx.CommandSettings.ConnectionStringName, ctx.SetupModel.Database,
        createSchemaStatement);
      return;
    }

    var msg = $"Schema {ctx.SetupModel.Schema} already exists - skipping creation";
    n.AddNode(msg);
    ctx.Log.AddSchemaCreationStatement(msg);
  }

  private void GrantUsageOnSchema(PgSetupContext ctx)
  {
    var usages = new StringBuilder();

    usages.AppendLine($"GRANT CREATE, USAGE ON SCHEMA {ctx.SetupModel.Schema} TO {ctx.SetupModel.OwningUser.Name};");
    foreach (var user in ctx.SetupModel.ReadWriteUsers)
      usages.AppendLine($"GRANT USAGE ON SCHEMA {ctx.SetupModel.Schema} TO {user.Name};");
    foreach (var user in ctx.SetupModel.ReadonlyUsers)
      usages.AppendLine($"GRANT USAGE ON SCHEMA {ctx.SetupModel.Schema} TO {user.Name};");

    var usagesSql = usages.ToString().TrimEnd();
    ctx.Log.AddGrantUsageStatement(usagesSql);

    _postgresRepository.ExecuteAsDbScopedAdmin(ctx.CommandSettings.ConnectionStringName, ctx.SetupModel.Database,
      usagesSql);
  }

  private void GrantDefaultPrivileges(PgSetupContext ctx, TreeNode n)
  {
    var s = new StringBuilder();
    s.AppendLine(
      $"ALTER DEFAULT PRIVILEGES IN SCHEMA {ctx.SetupModel.Schema} GRANT ALL ON TABLES TO {ctx.SetupModel.OwningUser.Name};");
    s.AppendLine(
      $"ALTER DEFAULT PRIVILEGES IN SCHEMA {ctx.SetupModel.Schema} GRANT ALL ON SEQUENCES TO {ctx.SetupModel.OwningUser.Name};");
    s.AppendLine(
      $"ALTER DEFAULT PRIVILEGES IN SCHEMA {ctx.SetupModel.Schema} GRANT ALL ON FUNCTIONS TO {ctx.SetupModel.OwningUser.Name};");
    s.AppendLine();

    n.AddNode($"Granting default privileges (ALL) to {ctx.SetupModel.OwningUser.Name}");
    foreach (var user in ctx.SetupModel.ReadWriteUsers)
    {
      s.AppendLine(
        $"ALTER DEFAULT PRIVILEGES IN SCHEMA {ctx.SetupModel.Schema} GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO {user.Name};");
      s.AppendLine(
        $"ALTER DEFAULT PRIVILEGES IN SCHEMA {ctx.SetupModel.Schema} GRANT USAGE, SELECT ON SEQUENCES TO {user.Name};");
      s.AppendLine(
        $"ALTER DEFAULT PRIVILEGES IN SCHEMA {ctx.SetupModel.Schema} GRANT EXECUTE ON FUNCTIONS TO {user.Name};");
      s.AppendLine();
      n.AddNode($"Granting default privileges (SELECT, INSERT, UPDATE, DELETE) to {user.Name}");
    }

    foreach (var user in ctx.SetupModel.ReadonlyUsers)
    {
      s.AppendLine(
        $"ALTER DEFAULT PRIVILEGES IN SCHEMA {ctx.SetupModel.Schema} GRANT SELECT ON TABLES TO {user.Name};");
      s.AppendLine();
      n.AddNode($"Granting default privileges (SELECT) to {user.Name}");
    }

    var defaultPrivilegesSql = s.ToString().TrimEnd();
    ctx.Log.AddGrantDefaultPrivilegesStatements(defaultPrivilegesSql);

    _postgresRepository.ExecuteAsOwningUser(ctx.CommandSettings.ConnectionStringName, ctx.SetupModel.Database,
      ctx.SetupModel.Schema, ctx.SetupModel.OwningUser.Name, ctx.SetupModel.OwningUser.Password, defaultPrivilegesSql);
  }


  private PgSetupContext GetSetupContext(PgSetupCommandSettings commandSettings, SettingsFileModel settingsFile,
    string database,
    string schema)
  {
    var owner = User.From(settingsFile.OwningUserTemplate.Name.ReplaceUserTokens(database, schema),
      settingsFile.OwningUserTemplate.GeneratePassword
        ? PasswordFactory.GetNew()
        : settingsFile.OwningUserTemplate.Password);

    var readWriteUsers = settingsFile.ReadWriteUserTemplates
      .Select(x => User.From(x.Name.ReplaceUserTokens(database, schema),
        x.GeneratePassword ? PasswordFactory.GetNew() : x.Password))
      .ToList();

    var readOnlyUsers = settingsFile.ReadonlyUserTemplates
      .Select(x => User.From(x.Name.ReplaceUserTokens(database, schema),
        x.GeneratePassword ? PasswordFactory.GetNew() : x.Password))
      .ToList();

    var setupModel = new SetupModel(database, schema, owner, readWriteUsers, readOnlyUsers);

    var connectionStrings = setupModel.GetUsers()
      .Select(u =>
        new NpgsqlConnectionStringBuilder(settingsFile.ConnectionStrings[commandSettings.ConnectionStringName])
        {
          Username = u.Name,
          Password = u.Password,
          Database = database,
          SearchPath = schema
        }.ToString());
    var connectionStringsText = string.Join("\n", connectionStrings);
    var auditLog = new PgSetupAuditLog();
    auditLog.AddConnectionStrings(connectionStringsText);

    return new PgSetupContext(setupModel, commandSettings, settingsFile, auditLog);
  }
}