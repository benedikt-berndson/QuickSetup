using System.Text.Json.Serialization;
using ConsoleTables;
using Cottle;
using Npgsql;
using QuickSetup.Common;
using QuickSetup.Models.Input;
using Spectre.Console;

namespace QuickSetup.Commands.Postgres;

public sealed class PgSetupAuditLog
{
  private readonly string _outputDir;

  // private readonly PgSetupContext _setupContext;
  private readonly string _databaseName;
  private readonly string _schemaName;
  private readonly Dictionary<Value, Value> _log;

  public PgSetupAuditLog(
    PgSetupCommandSettings commandSettings,
    UserSettings userSettings,
    string databaseName,
    string schemaName
  )
  {
    // 1. AuditLogOutputDirectory is null/empty => use the same directory as the user-settings file
    // 2. AuditLogOutputDirectory is not null/empty => use the specified directory. Although this could also just be a relative path.
    var mayBeFullPathOrRelativePath =
      (
        string.IsNullOrEmpty(userSettings.AuditLogOutputDirectory)
          ? Path.GetDirectoryName(Path.GetFullPath(commandSettings.UserSettingsFile))
          : userSettings.AuditLogOutputDirectory
      ) ?? throw new InvalidOperationException("Could not determine AuditLog output directory");

    // Combine takes the last fully qualified path.
    _outputDir = Path.IsPathFullyQualified(mayBeFullPathOrRelativePath)
      ? mayBeFullPathOrRelativePath
      : Path.Combine(Directory.GetCurrentDirectory(), Path.GetFullPath(mayBeFullPathOrRelativePath));

    // _setupContext = setupContext;
    _log = new Dictionary<Value, Value>();
    _databaseName = databaseName;
    _schemaName = schemaName;

    AddCommandline(commandSettings);
    AddSettings(userSettings);
  }

  private void AddCommandline(PgSetupCommandSettings commandSettings)
  {
    // pg-setup -n local --drop-databases --drop-schemas --settings-file "C:/Temp/QuickSetupTest/example_settings/settings.json"
    var dropDatabases = commandSettings.DropDatabases ? "--drop-databases" : "";
    var dropSchemas = commandSettings.DropSchemas ? "--drop-schemas" : "";
    var commandline =
      $"QuickSetup {PgSetupCommand.CommandName} -n {commandSettings.ConnectionStringName} {dropDatabases} {dropSchemas} -s {commandSettings.UserSettingsFile}";

    _log.Add("commandLine", commandline);
  }

  private void AddSettings(UserSettings userSettings)
  {
    var userSettingsJson = System.Text.Json.JsonSerializer.Serialize(
      userSettings,
      new System.Text.Json.JsonSerializerOptions
      {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
      }
    );
    _log.Add("settingsJson", userSettingsJson);
  }

  public void AddBasicInfo(PgSetupContext ctx)
  {
    var connectionStrings = ctx.GetAllUsers()
      .Select(u =>
        new NpgsqlConnectionStringBuilder(ctx.AdminConnectionString)
        {
          Username = u.Username,
          Password = u.Password,
          Database = ctx.DatabaseName,
          SearchPath = ctx.SchemaName,
        }.ToString()
      );
    _log.Add("connectionStrings", string.Join("\n", connectionStrings));

    var ut = new ConsoleTable("USER", "PASSWORD") { MaxWidth = 300 };
    foreach (var credentials in ctx.GetAllUsers())
      ut.AddRow(credentials.Username, credentials.Password.ToMdCode());

    _log.Add("usersTable", ut.ToMarkDownString());

    _log.Add("currentDatabase", ctx.DatabaseName);
    _log.Add("currentSchema", ctx.SchemaName);
    _log.Add("dbObjectsOwner", ctx.Owner.Username);
  }

  public void AddDropSchemaStatement(string text) => _log.Add("dropSchemaStatement", text);

  public void AddDropDatabaseStatementSkipped() => _log.Add("dropDatabaseStatementSkipped", true);

  public void AddDropDatabaseStatement(string text) => _log.Add("dropDatabaseStatement", text);

  public void AddDropUsersStatement(string text) => _log.Add("dropUserStatement", text);

  public void AddUserCreationStatements(string text) => _log.Add("userCreationStatements", text);

  public void AddDatabaseCreationStatement(string text) => _log.Add("databaseCreationStatements", text);

  public void AddSchemaCreationStatement(string text) => _log.Add("schemaCreationStatements", text);

  public void AddExtensionCreationStatement(string text) => _log.Add("extensionCreationStatements", text);

  public void AddGrantUsageStatement(string text) => _log.Add("grantUsageStatements", text);

  public void AddGrantDefaultPrivilegesStatements(string text) => _log.Add("grantDefaultPrivilegesStatements", text);

  public void AddException(Exception e) => _log.Add("exception", e.ToString());

  public void WriteFile(TreeNode n, bool setupFailed = false)
  {
    var path = Path.Join(Directory.GetCurrentDirectory(), "Commands", "Postgres", "audit_log_template.md");
    var template = File.ReadAllText(path);
    var config = new DocumentConfiguration { NoOptimize = true, Trimmer = DocumentConfiguration.TrimNothing };
    var document = Document.CreateDefault(template, config).DocumentOrThrow;

    var renderContext = Context.CreateBuiltin(_log);
    var rendered = document.Render(renderContext);

    var error = setupFailed ? "ERROR_" : "";
    var filename = $"{error}{DateTimeOffset.Now:yyyy-MM-dd_HH-mm-ss}_audit_log__{_databaseName}__{_schemaName}.md";
    var file = Path.Join(_outputDir, filename);

    var dir = Path.GetDirectoryName(file);
    if (!Directory.Exists(file))
    {
      Directory.CreateDirectory(dir!);
    }

    n.AddNode($"Writing audit log to: {file}");
    File.WriteAllText(file, rendered);
  }
}
