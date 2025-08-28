using System.Text;
using Cottle;
using Npgsql;
using QuickSetup.Common;
using QuickSetup.Common.Abstractions;
using QuickSetup.Models;
using QuickSetup.Models.Input;
using QuickSetup.Models.Processing;
using Spectre.Console;
using Spectre.Console.Cli;

namespace QuickSetup.Commands.Postgres;

// [CliCommand(Description = "Setup new databases, users, schemas and default privileges", Parent = typeof(RootCommand))]
public sealed class PgSetupCommand(ISettingsProvider settingsProvider, IPostgresRepository postgresRepository)
  : Command<PgSetupCommandSettings>
{
  public override int Execute(CommandContext context, PgSetupCommandSettings commandSettings)
  {
    AnsiConsole.MarkupLine("[bold]STARTING DATABASE SETUP[/]");
    AnsiConsole.MarkupLineInterpolated($"Reading settings file: {commandSettings.UserSettingsFile}");

    settingsProvider.Init(commandSettings.UserSettingsFile);
    var settings = settingsProvider.GetUserSettings();

    var root = new Tree("\nSTART ITERATION STEP");
    foreach (
      var ctx in settings.DatabaseSchemaSetupOptions.Select(entry =>
        GetSetupContext(commandSettings, settings, entry.Key, entry.Value)
      )
    )
    {
      try
      {
        root.AddNode($"RUNNING SETUP FOR {ctx.ProcessingModel.DatabaseName}.{ctx.ProcessingModel.SchemaName}");
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
    if (ctx.CommandSettings is { DropDatabases: false, DropSchemas: false })
    {
      n.AddNode("Nothing to do");
      ctx.Log.AddDropDatabaseStatementSkipped();
      return;
    }

    var existingDatabases = postgresRepository.GetDatabaseNames(ctx.CommandSettings.ConnectionStringName);
    var dbExists = existingDatabases.Contains(ctx.ProcessingModel.DatabaseName);
    if (ctx.CommandSettings.DropSchemas && dbExists)
    {
      n.AddNode($"Dropping schema: {ctx.ProcessingModel.SchemaName}");
      var dropSchemaStatement = $"DROP SCHEMA IF EXISTS {ctx.ProcessingModel.SchemaName} CASCADE";
      ctx.Log.AddDropSchemaStatement(dropSchemaStatement);
      postgresRepository.ExecuteAsDbScopedAdmin(
        ctx.CommandSettings.ConnectionStringName,
        ctx.ProcessingModel.DatabaseName,
        dropSchemaStatement
      );
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
      n.AddNode($"Dropping database: {ctx.ProcessingModel.DatabaseName}");
      var dropDatabaseStatement = $"DROP DATABASE IF EXISTS {ctx.ProcessingModel.DatabaseName}";
      ctx.Log.AddDropDatabaseStatement(dropDatabaseStatement);
      postgresRepository.ExecuteAsRootAdmin(ctx.CommandSettings.ConnectionStringName, dropDatabaseStatement);
    }
    else
    {
      var skipDropDatabaseMsg =
        $"Skipping dropping database. --drop-databases={ctx.CommandSettings.DropDatabases} && dbExists={dbExists}";
      n.AddNode(skipDropDatabaseMsg);
      ctx.Log.AddDropDatabaseStatement(skipDropDatabaseMsg);
    }

    var dropUserStatements = string.Join(
      ";\n",
      ctx.ProcessingModel.GetUsers().Select(x => $"DROP USER IF EXISTS {x.Username}")
    );
    n.AddNode($"Dropping users: {string.Join(", ", ctx.ProcessingModel.GetUsers().Select(x => x.Username))}");
    ctx.Log.AddDropUsersStatement(dropUserStatements);
    postgresRepository.ExecuteAsRootAdmin(ctx.CommandSettings.ConnectionStringName, dropUserStatements);
  }

  private void CreateUsers(PgSetupContext ctx, TreeNode n)
  {
    var createStatements = string.Join(
      ";\n",
      ctx.ProcessingModel.GetUsers().Select(x => $"CREATE USER {x.Username} WITH ENCRYPTED PASSWORD '{x.Password}'")
    );

    n.AddNode($"Create users: {string.Join(", ", ctx.ProcessingModel.GetUsers().Select(x => x.Username))}");
    ctx.Log.AddUserCreationStatements(createStatements);
    postgresRepository.ExecuteAsRootAdmin(ctx.CommandSettings.ConnectionStringName, createStatements);
  }

  private static void WriteAuditLog(PgSetupContext ctx, TreeNode n, bool setupFailed = false)
  {
    var path = Path.Join(Directory.GetCurrentDirectory(), "Commands", "Postgres", "setup_log_template.md");
    var template = File.ReadAllText(path);
    var config = new DocumentConfiguration { NoOptimize = true, Trimmer = DocumentConfiguration.TrimNothing };
    var document = Document.CreateDefault(template, config).DocumentOrThrow;

    var renderContext = Context.CreateBuiltin(ctx.Log.Log);
    var rendered = document.Render(renderContext);

    var auditLogOutputDir = string.IsNullOrEmpty(ctx.UserSettings.AuditLogOptions.Path)
      ? Path.GetDirectoryName(Path.GetFullPath(ctx.CommandSettings.UserSettingsFile))
      : ctx.UserSettings.AuditLogOptions.Path;

    var error = setupFailed ? "ERROR_" : "";
    var filename =
      $"{error}{DateTimeOffset.Now:yyyy-MM-dd_HH-mm-ss}_audit_log__{ctx.ProcessingModel.DatabaseName}__{ctx.ProcessingModel.SchemaName}.md";
    var file = Path.IsPathFullyQualified(auditLogOutputDir!)
      ? Path.Join(auditLogOutputDir, filename)
      : Path.Combine(Directory.GetCurrentDirectory(), Path.GetFullPath(ctx.UserSettings.AuditLogOptions.Path), filename);

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
    var existingDatabases = postgresRepository.GetDatabaseNames(ctx.CommandSettings.ConnectionStringName);
    if (!existingDatabases.Contains(ctx.ProcessingModel.DatabaseName))
    {
      var createDatabaseStatement = $"""
        CREATE DATABASE {ctx.ProcessingModel.DatabaseName}
        WITH
        OWNER {ctx.UserSettings.DatabaseSetupOptions.Owner}
        ENCODING = {ctx.UserSettings.DatabaseSetupOptions.Encoding}
        TABLESPACE = {ctx.UserSettings.DatabaseSetupOptions.Tablespace}
        CONNECTION LIMIT = {ctx.UserSettings.DatabaseSetupOptions.ConnectionLimit};
        """;
      n.AddNode($"Creating database: {ctx.ProcessingModel.DatabaseName}");
      ctx.Log.AddDatabaseCreationStatement(createDatabaseStatement);
      postgresRepository.ExecuteAsRootAdmin(ctx.CommandSettings.ConnectionStringName, createDatabaseStatement);
      return;
    }

    var msg = $"Database {ctx.ProcessingModel.DatabaseName} already exists - skipping creation";
    n.AddNode(msg);
    ctx.Log.AddDatabaseCreationStatement(msg);
  }

  private void CreateSchema(PgSetupContext ctx, TreeNode n)
  {
    var existingSchemas = postgresRepository.GetSchemaNames(
      ctx.CommandSettings.ConnectionStringName,
      ctx.ProcessingModel.DatabaseName
    );
    if (!existingSchemas.Contains(ctx.ProcessingModel.SchemaName.ToLower()))
    {
      var createSchemaStatement = $"CREATE SCHEMA {ctx.ProcessingModel.SchemaName};";

      n.AddNode($"Creating schema: {ctx.ProcessingModel.SchemaName}");
      ctx.Log.AddSchemaCreationStatement(createSchemaStatement);
      postgresRepository.ExecuteAsDbScopedAdmin(
        ctx.CommandSettings.ConnectionStringName,
        ctx.ProcessingModel.DatabaseName,
        createSchemaStatement
      );
      return;
    }

    var msg = $"Schema {ctx.ProcessingModel.SchemaName} already exists - skipping creation";
    n.AddNode(msg);
    ctx.Log.AddSchemaCreationStatement(msg);
  }

  private void GrantUsageOnSchema(PgSetupContext ctx)
  {
    var usages = new StringBuilder();

    usages.AppendLine(
      $"GRANT CREATE, USAGE ON SCHEMA {ctx.ProcessingModel.SchemaName} TO {ctx.ProcessingModel.Owner.Username};"
    );
    foreach (var credentials in ctx.ProcessingModel.AppUsers)
      usages.AppendLine($"GRANT USAGE ON SCHEMA {ctx.ProcessingModel.SchemaName} TO {credentials.Username};");
    foreach (var user in ctx.ProcessingModel.ReadonlyUsers)
      usages.AppendLine($"GRANT USAGE ON SCHEMA {ctx.ProcessingModel.SchemaName} TO {user.Username};");

    var usagesSql = usages.ToString().TrimEnd();
    ctx.Log.AddGrantUsageStatement(usagesSql);

    postgresRepository.ExecuteAsDbScopedAdmin(
      ctx.CommandSettings.ConnectionStringName,
      ctx.ProcessingModel.DatabaseName,
      usagesSql
    );
  }

  private void GrantDefaultPrivileges(PgSetupContext ctx, TreeNode n)
  {
    var s = new StringBuilder();
    s.AppendLine(
      $"ALTER DEFAULT PRIVILEGES IN SCHEMA {ctx.ProcessingModel.SchemaName} GRANT ALL ON TABLES TO {ctx.ProcessingModel.Owner.Username};"
    );
    s.AppendLine(
      $"ALTER DEFAULT PRIVILEGES IN SCHEMA {ctx.ProcessingModel.SchemaName} GRANT ALL ON SEQUENCES TO {ctx.ProcessingModel.Owner.Username};"
    );
    s.AppendLine(
      $"ALTER DEFAULT PRIVILEGES IN SCHEMA {ctx.ProcessingModel.SchemaName} GRANT ALL ON FUNCTIONS TO {ctx.ProcessingModel.Owner.Username};"
    );
    s.AppendLine();

    n.AddNode($"Granting default privileges (ALL) to {ctx.ProcessingModel.Owner.Username}");
    foreach (var credentials in ctx.ProcessingModel.AppUsers)
    {
      s.AppendLine(
        $"ALTER DEFAULT PRIVILEGES IN SCHEMA {ctx.ProcessingModel.SchemaName} GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO {credentials.Username};"
      );
      s.AppendLine(
        $"ALTER DEFAULT PRIVILEGES IN SCHEMA {ctx.ProcessingModel.SchemaName} GRANT USAGE, SELECT ON SEQUENCES TO {credentials.Username};"
      );
      s.AppendLine(
        $"ALTER DEFAULT PRIVILEGES IN SCHEMA {ctx.ProcessingModel.SchemaName} GRANT EXECUTE ON FUNCTIONS TO {credentials.Username};"
      );
      s.AppendLine();
      n.AddNode($"Granting default privileges (SELECT, INSERT, UPDATE, DELETE) to {credentials.Username}");
    }

    foreach (var credentials in ctx.ProcessingModel.ReadonlyUsers)
    {
      s.AppendLine(
        $"ALTER DEFAULT PRIVILEGES IN SCHEMA {ctx.ProcessingModel.SchemaName} GRANT SELECT ON TABLES TO {credentials.Username};"
      );
      s.AppendLine();
      n.AddNode($"Granting default privileges (SELECT) to {credentials.Username}");
    }

    var defaultPrivilegesSql = s.ToString().TrimEnd();
    ctx.Log.AddGrantDefaultPrivilegesStatements(defaultPrivilegesSql);

    postgresRepository.ExecuteAsOwningUser(
      ctx.CommandSettings.ConnectionStringName,
      ctx.ProcessingModel.DatabaseName,
      ctx.ProcessingModel.SchemaName,
      ctx.ProcessingModel.Owner.Username,
      ctx.ProcessingModel.Owner.Password,
      defaultPrivilegesSql
    );
  }

  private static PgSetupContext GetSetupContext(
    PgSetupCommandSettings commandSettings,
    UserSettings userSettings,
    string database,
    string schema
  )
  {
    var owner = new Credentials(
      userSettings.UserSetupOptions.Owner.Username.ReplaceUserTokens(database, schema),
      userSettings.UserSetupOptions.Owner.GeneratePassword
        ? PasswordFactory.GetNew()
        : userSettings.UserSetupOptions.Owner.Password
    );

    var readWriteUsers = userSettings
      .UserSetupOptions.AppUsers.Select(x => new Credentials(
        x.Username.ReplaceUserTokens(database, schema),
        x.GeneratePassword ? PasswordFactory.GetNew() : x.Password
      ))
      .ToList();

    var readOnlyUsers = userSettings
      .UserSetupOptions.ReadOnlyUsers.Select(x => new Credentials(
        x.Username.ReplaceUserTokens(database, schema),
        x.GeneratePassword ? PasswordFactory.GetNew() : x.Password
      ))
      .ToList();

    var setupModel = new DatabaseSetupProcessingModel(database, schema, owner, readWriteUsers, readOnlyUsers);

    var connectionStrings = setupModel
      .GetUsers()
      .Select(u =>
        new NpgsqlConnectionStringBuilder(userSettings.AdminConnectionStrings[commandSettings.ConnectionStringName])
        {
          Username = u.Username,
          Password = u.Password,
          Database = database,
          SearchPath = schema,
        }.ToString()
      );
    var connectionStringsText = string.Join("\n", connectionStrings);
    var auditLog = new PgSetupAuditLog();
    auditLog.AddConnectionStrings(connectionStringsText);

    return new PgSetupContext(setupModel, commandSettings, userSettings, auditLog);
  }
}
