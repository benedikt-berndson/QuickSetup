using System.Text;
using QuickSetup.Common;
using QuickSetup.Common.Abstractions;
using QuickSetup.Models;
using QuickSetup.Models.Processing;
using QuickSetup.Models.UserSettings;
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

    DropDatabaseObjects(commandSettings, userSettings);
    var handleUsers = HandleUsers(commandSettings, userSettings);

    var root = new Tree("\nSTART ITERATION STEP");
    foreach (var ctx in GetSetupContexts(commandSettings, userSettings))
    {
      try
      {
        root.AddNode($"RUNNING SETUP FOR {ctx.DatabaseName}.{ctx.SchemaName}");
        ctx.Log.AddBasicInfo(ctx);

        var n2 = root.AddNode("Running user creation");
        handleUsers(ctx, n2);

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

        AnsiConsole.Write(root);
        ctx.Log.WriteFile();

        root = new Tree("\nSTART ITERATION STEP");
      }
      catch (Exception e)
      {
        ctx.Log.AddException(e);
        ctx.Log.WriteFile(true);

        AnsiConsole.Write(root);
        AnsiConsole.WriteException(e);
        return 1;
      }
    }

    return 0;
  }

  private void DropDatabaseObjects(PgSetupCommandSettings commandSettings, UserSettings ctx)
  {
    if (commandSettings is { DropDatabases: false, DropSchemas: false })
    {
      AnsiConsole.MarkupLine(
        "[bold]Skipping dropping database and schema. Both --drop-databases and --drop-schemas are false.[/]"
      );
      return;
    }

    var adminConnectionString = ctx.AdminConnectionStrings[commandSettings.ConnectionStringName];
    var existingDatabases = postgresRepository.GetDatabaseNames(adminConnectionString);

    foreach (var (databaseName, schemaNames) in ctx.DatabaseSchemaSetupOptions)
    {
      var dbExists = existingDatabases.Contains(databaseName);
      if (commandSettings.DropSchemas)
      {
        foreach (var schemaName in schemaNames)
        {
          if (!dbExists)
            continue;

          AnsiConsole.MarkupLineInterpolated($"Dropping schema: {databaseName}/{schemaName}");
          var dropSchemaStatement = $"DROP SCHEMA IF EXISTS {schemaName} CASCADE";
          postgresRepository.ExecuteAsDbScopedAdmin(adminConnectionString, databaseName, dropSchemaStatement);
        }
      }

      if (commandSettings.DropDatabases && dbExists)
      {
        AnsiConsole.MarkupLineInterpolated($"Dropping database: {databaseName}");
        var dropDatabaseStatement = $"DROP DATABASE IF EXISTS {databaseName}";
        postgresRepository.ExecuteAsRootAdmin(adminConnectionString, dropDatabaseStatement);
      }
    }
  }

  /// <summary>
  /// Create an Action to update users because we need to cache already dropped users for the "single-user for all schemas within one database" feature.
  /// Otherwise, we would drop the same user multiple times, which also fails because objects depend on this user.
  /// </summary>
  /// <returns></returns>
  private Action<PgSetupContext, TreeNode> HandleUsers(PgSetupCommandSettings commandSettings, UserSettings userSettings)
  {
    var processedUserCredentials = new HashSet<CredentialsModel>();
    var allUsers = postgresRepository.GetAllUsers(userSettings.AdminConnectionStrings[commandSettings.ConnectionStringName]);

    return (ctx, n) =>
    {
      var relevantCredentialModels = ctx.GetAllUsers().Where(x => !processedUserCredentials.Contains(x)).ToArray();

      // Executing 'illegal' statements without a username will fail.
      if (relevantCredentialModels.Length == 0)
        return;

      // DROP
      var droppableUsernames = relevantCredentialModels
        .Where(x => allUsers.Contains(x.Username))
        .Select(x => x.Username)
        .ToArray();
      if (droppableUsernames.Length != 0)
      {
        var dropUserStatements = string.Join(";\n", droppableUsernames.Select(uname => $"DROP USER IF EXISTS {uname}"));
        n.AddNode($"Dropping users: {string.Join(", ", droppableUsernames)}");
        ctx.Log.AddDropUsersStatement(dropUserStatements);
        postgresRepository.ExecuteAsRootAdmin(ctx, dropUserStatements);
      }
      processedUserCredentials.UnionWith(relevantCredentialModels);

      // CREATE
      var createStatements = string.Join(
        ";\n",
        relevantCredentialModels.Select(x => $"CREATE USER {x.Username} WITH ENCRYPTED PASSWORD '{x.Password}'")
      );

      n.AddNode($"Create users: {string.Join(", ", relevantCredentialModels.Select(x => x.Username))}");
      ctx.Log.AddUserCreationStatements(createStatements);
      postgresRepository.ExecuteAsRootAdmin(ctx, createStatements);
    };
  }

  private void CreateDatabase(PgSetupContext ctx, TreeNode n)
  {
    // CREATE DATABASE
    var existingDatabases = postgresRepository.GetDatabaseNames(ctx.AdminConnectionString);
    if (existingDatabases.Contains(ctx.DatabaseName))
    {
      var msg = $"Skip creating database {ctx.DatabaseName} - database already exists.";
      n.AddNode(msg);
      ctx.Log.AddDatabaseCreationStatement(msg);

      return;
    }

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
