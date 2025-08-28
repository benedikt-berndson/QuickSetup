using System.Text;
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
  public const string CommandName = "pg-setup";

  public override int Execute(CommandContext commandContext, PgSetupCommandSettings commandSettings)
  {
    AnsiConsole.MarkupLine("[bold]STARTING DATABASE SETUP[/]");
    AnsiConsole.MarkupLineInterpolated($"Reading settings file: {commandSettings.UserSettingsFile}");

    var userSettings = settingsProvider.GetUserSettings(commandSettings.UserSettingsFile);

    // TODO: Deleting database objects must be a one time operation and currently breaks the single-user setup because the second context tries to delete the user. Exclude from audit log.
    var root = new Tree("\nSTART ITERATION STEP");
    foreach (var ctx in GetSetupContexts(commandSettings, userSettings))
    {
      try
      {
        root.AddNode($"RUNNING SETUP FOR {ctx.DatabaseName}.{ctx.SchemaName}");
        ctx.Log.AddBasicInfo(ctx);

        var n1 = root.AddNode("Running deletion of database objects");
        DeleteDatabaseObjects(ctx, n1);

        var n2 = root.AddNode("Running user creation");
        CreateUsers(ctx, n2);

        var n3 = root.AddNode("Running database creation");
        CreateDatabase(ctx, n3);

        var n4 = root.AddNode("Running schema creation");
        CreateSchema(ctx, n4);

        var n5 = root.AddNode("Running extension creation");
        CreateExtensions(ctx, n5);

        root.AddNode("Grant usages on schema");
        GrantUsageOnSchema(ctx);

        var n6 = root.AddNode("Grant default privileges");
        GrantDefaultPrivileges(ctx, n6);

        var n7 = root.AddNode("Write audit log");
        ctx.Log.WriteFile(n7);

        AnsiConsole.Write(root);
        root = new Tree("\nSTART ITERATION STEP");
      }
      catch (Exception e)
      {
        ctx.Log.AddException(e);
        var n = root.AddNode("[red]Writing partial audit log after error[/]");
        ctx.Log.WriteFile(n, true);

        AnsiConsole.Write(root);
        AnsiConsole.WriteException(e);
        return 1;
      }
    }

    return 0;
  }

  private void DeleteDatabaseObjects(PgSetupContext ctx, TreeNode n)
  {
    if (ctx is { DropDatabases: false, DropSchemas: false })
    {
      n.AddNode("Nothing to do");
      ctx.Log.AddDropDatabaseStatementSkipped();
      return;
    }

    var existingDatabases = postgresRepository.GetDatabaseNames(ctx);
    var dbExists = existingDatabases.Contains(ctx.DatabaseName);
    if (ctx.DropSchemas && dbExists)
    {
      n.AddNode($"Dropping schema: {ctx.SchemaName}");
      var dropSchemaStatement = $"DROP SCHEMA IF EXISTS {ctx.SchemaName} CASCADE";
      ctx.Log.AddDropSchemaStatement(dropSchemaStatement);
      postgresRepository.ExecuteAsDbScopedAdmin(ctx, dropSchemaStatement);
    }
    else
    {
      var skipDropSchemaMsg = $"Skipping dropping schema. --drop-schemas={ctx.DropSchemas} && dbExists={dbExists}";
      n.AddNode(skipDropSchemaMsg);
      ctx.Log.AddDropSchemaStatement(skipDropSchemaMsg);
    }

    if (ctx.DropDatabases && dbExists)
    {
      n.AddNode($"Dropping database: {ctx.DatabaseName}");
      var dropDatabaseStatement = $"DROP DATABASE IF EXISTS {ctx.DatabaseName}";
      ctx.Log.AddDropDatabaseStatement(dropDatabaseStatement);
      postgresRepository.ExecuteAsRootAdmin(ctx, dropDatabaseStatement);
    }
    else
    {
      var skipDropDatabaseMsg = $"Skipping dropping database. --drop-databases={ctx.DropDatabases} && dbExists={dbExists}";
      n.AddNode(skipDropDatabaseMsg);
      ctx.Log.AddDropDatabaseStatement(skipDropDatabaseMsg);
    }

    var dropUserStatements = string.Join(";\n", ctx.GetAllUsers().Select(x => $"DROP USER IF EXISTS {x.Username}"));
    n.AddNode($"Dropping users: {string.Join(", ", ctx.GetAllUsers().Select(x => x.Username))}");
    ctx.Log.AddDropUsersStatement(dropUserStatements);
    postgresRepository.ExecuteAsRootAdmin(ctx, dropUserStatements);
  }

  private void CreateUsers(PgSetupContext ctx, TreeNode n)
  {
    var createStatements = string.Join(
      ";\n",
      ctx.GetAllUsers().Select(x => $"CREATE USER {x.Username} WITH ENCRYPTED PASSWORD '{x.Password}'")
    );

    n.AddNode($"Create users: {string.Join(", ", ctx.GetAllUsers().Select(x => x.Username))}");
    ctx.Log.AddUserCreationStatements(createStatements);
    postgresRepository.ExecuteAsRootAdmin(ctx, createStatements);
  }

  private void CreateDatabase(PgSetupContext ctx, TreeNode n)
  {
    // CREATE DATABASE
    var existingDatabases = postgresRepository.GetDatabaseNames(ctx);
    if (!existingDatabases.Contains(ctx.DatabaseName))
    {
      var createDatabaseStatement = $"""
        CREATE DATABASE {ctx.DatabaseName}
        WITH
        OWNER {ctx.DatabaseSetupModel.Owner}
        ENCODING = {ctx.DatabaseSetupModel.Encoding}
        TABLESPACE = {ctx.DatabaseSetupModel.Tablespace}
        CONNECTION LIMIT = {ctx.DatabaseSetupModel.ConnectionLimit};
        """;
      n.AddNode($"Creating database: {ctx.DatabaseName}");
      ctx.Log.AddDatabaseCreationStatement(createDatabaseStatement);
      postgresRepository.ExecuteAsRootAdmin(ctx, createDatabaseStatement);
      return;
    }

    var msg = $"Database {ctx.DatabaseName} already exists - skipping creation";
    n.AddNode(msg);
    ctx.Log.AddDatabaseCreationStatement(msg);
  }

  private void CreateSchema(PgSetupContext ctx, TreeNode n)
  {
    var existingSchemas = postgresRepository.GetSchemaNames(ctx);
    if (!existingSchemas.Contains(ctx.SchemaName.ToLower()))
    {
      var createSchemaStatement = $"CREATE SCHEMA {ctx.SchemaName};";

      n.AddNode($"Creating schema: {ctx.SchemaName}");
      ctx.Log.AddSchemaCreationStatement(createSchemaStatement);
      postgresRepository.ExecuteAsDbScopedAdmin(ctx, createSchemaStatement);
      return;
    }

    var msg = $"Schema {ctx.SchemaName} already exists - skipping creation";
    n.AddNode(msg);
    ctx.Log.AddSchemaCreationStatement(msg);
  }

  private void CreateExtensions(PgSetupContext ctx, TreeNode n)
  {
    var statements = ctx.ExtensionsPerSchema.Select(x => $"CREATE EXTENSION IF NOT EXISTS \"{x}\";").ToList();
    foreach (var statement in statements)
    {
      postgresRepository.ExecuteAsSchemaScopedAdmin(ctx, statement);
    }

    n.AddNode($"Creating extensions: {string.Join(", ", ctx.ExtensionsPerSchema)}");
    ctx.Log.AddExtensionCreationStatement(string.Join("\n", statements));
  }

  private void GrantUsageOnSchema(PgSetupContext ctx)
  {
    var usages = new StringBuilder();

    usages.AppendLine($"GRANT CREATE, USAGE ON SCHEMA {ctx.SchemaName} TO {ctx.Owner.Username};");
    foreach (var credentials in ctx.AppUserCredentialsModels)
      usages.AppendLine($"GRANT USAGE ON SCHEMA {ctx.SchemaName} TO {credentials.Username};");
    foreach (var user in ctx.ReadonlyUserCredentialsModels)
      usages.AppendLine($"GRANT USAGE ON SCHEMA {ctx.SchemaName} TO {user.Username};");

    var usagesSql = usages.ToString().TrimEnd();
    ctx.Log.AddGrantUsageStatement(usagesSql);

    postgresRepository.ExecuteAsDbScopedAdmin(ctx, usagesSql);
  }

  private void GrantDefaultPrivileges(PgSetupContext ctx, TreeNode n)
  {
    var s = new StringBuilder();
    s.AppendLine($"ALTER DEFAULT PRIVILEGES IN SCHEMA {ctx.SchemaName} GRANT ALL ON TABLES TO {ctx.Owner.Username};");
    s.AppendLine($"ALTER DEFAULT PRIVILEGES IN SCHEMA {ctx.SchemaName} GRANT ALL ON SEQUENCES TO {ctx.Owner.Username};");
    s.AppendLine($"ALTER DEFAULT PRIVILEGES IN SCHEMA {ctx.SchemaName} GRANT ALL ON FUNCTIONS TO {ctx.Owner.Username};");
    s.AppendLine();

    n.AddNode($"Granting default privileges (ALL) to {ctx.Owner.Username}");
    foreach (var credentials in ctx.AppUserCredentialsModels)
    {
      s.AppendLine(
        $"ALTER DEFAULT PRIVILEGES IN SCHEMA {ctx.SchemaName} GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO {credentials.Username};"
      );
      s.AppendLine(
        $"ALTER DEFAULT PRIVILEGES IN SCHEMA {ctx.SchemaName} GRANT USAGE, SELECT ON SEQUENCES TO {credentials.Username};"
      );
      s.AppendLine(
        $"ALTER DEFAULT PRIVILEGES IN SCHEMA {ctx.SchemaName} GRANT EXECUTE ON FUNCTIONS TO {credentials.Username};"
      );
      s.AppendLine();
      n.AddNode($"Granting default privileges (SELECT, INSERT, UPDATE, DELETE) to {credentials.Username}");
    }

    foreach (var credentials in ctx.ReadonlyUserCredentialsModels)
    {
      s.AppendLine($"ALTER DEFAULT PRIVILEGES IN SCHEMA {ctx.SchemaName} GRANT SELECT ON TABLES TO {credentials.Username};");
      s.AppendLine();
      n.AddNode($"Granting default privileges (SELECT) to {credentials.Username}");
    }

    var defaultPrivilegesSql = s.ToString().TrimEnd();
    ctx.Log.AddGrantDefaultPrivilegesStatements(defaultPrivilegesSql);

    postgresRepository.ExecuteAsOwningUser(ctx, defaultPrivilegesSql);
  }

  private static IEnumerable<PgSetupContext> GetSetupContexts(
    PgSetupCommandSettings commandSettings,
    UserSettings userSettings
  )
  {
    var singleUserOverride = userSettings.UserSetupOptions.SingleUserOverride;
    if (singleUserOverride.IsActive && string.IsNullOrEmpty(singleUserOverride.UsernameSchemaNameReplacement))
    {
      throw new InvalidOperationException("SingleUserOverride.UsernameSchemaNameReplacement is empty");
    }

    foreach (var entry in userSettings.DatabaseSchemaSetupOptions)
    {
      var (superOwnerPassword, superAppUserPassword, superReadOnlyPassword) = GetSingleUserOverridePasswords(userSettings);

      foreach (var schemaName in entry.Value)
      {
        if (singleUserOverride.IsActive)
        {
          yield return GetSetupContext(
            commandSettings,
            userSettings,
            entry.Key,
            schemaName,
            singleUserOverride.UsernameSchemaNameReplacement,
            superOwnerPassword,
            superAppUserPassword,
            superReadOnlyPassword
          );
        }
        else
        {
          yield return GetSetupContext(commandSettings, userSettings, entry.Key, schemaName, schemaName);
        }
      }
    }
  }

  private static (
    string? superOwnerPassword,
    string? superAppUserPassword,
    string? superReadOnlyPassword
  ) GetSingleUserOverridePasswords(UserSettings userSettings)
  {
    var singleUserOverride = userSettings.UserSetupOptions.SingleUserOverride;
    if (!singleUserOverride.IsActive)
    {
      return (null, null, null);
    }

    var superOwnerPassword = string.IsNullOrEmpty(singleUserOverride.OwnerPassword)
      ? PasswordFactory.GetNew()
      : singleUserOverride.OwnerPassword;
    var superAppUserPassword = string.IsNullOrEmpty(singleUserOverride.AppUsersPassword)
      ? PasswordFactory.GetNew()
      : singleUserOverride.AppUsersPassword;
    var superReadOnlyPassword = string.IsNullOrEmpty(singleUserOverride.ReadOnlyUsersPassword)
      ? PasswordFactory.GetNew()
      : singleUserOverride.ReadOnlyUsersPassword;

    return (superOwnerPassword, superAppUserPassword, superReadOnlyPassword);
  }

  private static PgSetupContext GetSetupContext(
    PgSetupCommandSettings commandSettings,
    UserSettings userSettings,
    string databaseName,
    string schemaName,
    string usernameSchemaNameReplacement,
    string? superOwnerPassword = null,
    string? superAppUserPassword = null,
    string? superReadOnlyPassword = null
  )
  {
    var owner = new CredentialsModel(
      userSettings.UserSetupOptions.Owner.Username.ReplaceUserTokens(databaseName, usernameSchemaNameReplacement),
      superOwnerPassword
        ?? (
          userSettings.UserSetupOptions.Owner.GeneratePassword
            ? PasswordFactory.GetNew()
            : userSettings.UserSetupOptions.Owner.Password
        )
    );

    var appUser = userSettings
      .UserSetupOptions.AppUsers.Select(x => new CredentialsModel(
        x.Username.ReplaceUserTokens(databaseName, usernameSchemaNameReplacement),
        superAppUserPassword ?? (x.GeneratePassword ? PasswordFactory.GetNew() : x.Password)
      ))
      .ToList();

    var readOnlyUsers = userSettings
      .UserSetupOptions.ReadOnlyUsers.Select(x => new CredentialsModel(
        x.Username.ReplaceUserTokens(databaseName, usernameSchemaNameReplacement),
        superReadOnlyPassword ?? (x.GeneratePassword ? PasswordFactory.GetNew() : x.Password)
      ))
      .ToList();

    var setupContext = new PgSetupContext(
      userSettings.AdminConnectionStrings[commandSettings.ConnectionStringName],
      databaseName,
      schemaName,
      commandSettings.DropDatabases,
      commandSettings.DropSchemas,
      new DatabaseSetupModel(
        userSettings.DatabaseSetupOptions.Owner,
        userSettings.DatabaseSetupOptions.Encoding,
        userSettings.DatabaseSetupOptions.Tablespace,
        userSettings.DatabaseSetupOptions.ConnectionLimit
      ),
      userSettings.ExtensionsPerSchema,
      owner,
      appUser,
      readOnlyUsers,
      new PgSetupAuditLog(commandSettings, userSettings, databaseName, schemaName)
    );

    return setupContext;
  }
}
