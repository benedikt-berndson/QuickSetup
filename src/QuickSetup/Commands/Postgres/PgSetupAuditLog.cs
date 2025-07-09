using ConsoleTables;
using Cottle;
using DotMake.CommandLine;
using QuickSetup.Common;
using QuickSetup.Models;

namespace QuickSetup.Commands.Postgres;

public sealed class PgSetupAuditLog(QuickSetupSettings settings, CliContext cliContext)
{
  private readonly Dictionary<Value, Value> _log = new();

  public Dictionary<Value, Value> Log => _log;

  public void AddSettingsTable(PgSetupCommand command, DbSetupContext ctx)
  {
    var st = new ConsoleTable("PROPERTY", "VALUE")
    {
      MaxWidth = 300
    };
    st.AddRow(nameof(command.ConnectionStringName), command.ConnectionStringName.ToMdCode());
    st.AddRow(nameof(command.DropDatabases), command.DropDatabases.ToString().ToMdCode());
    st.AddRow(nameof(command.DropSchemas), command.DropSchemas.ToString().ToMdCode());
    ;
    var databasesAndSchemas = string.Join(", ", settings.DatabaseToSchemaMap.Select(x => $"{x.Key}:{x.Value}"));
    st.AddRow("Databases/Schemas", databasesAndSchemas.ToMdCode());

    st.AddRow($"{nameof(settings.CreateDatabaseMetadata)}.{nameof(settings.CreateDatabaseMetadata.Owner)}",
      settings.CreateDatabaseMetadata.Owner.ToMdCode());
    st.AddRow($"{nameof(settings.CreateDatabaseMetadata)}.{nameof(settings.CreateDatabaseMetadata.Encoding)}",
      settings.CreateDatabaseMetadata.Encoding.ToMdCode());
    st.AddRow($"{nameof(settings.CreateDatabaseMetadata)}.{nameof(settings.CreateDatabaseMetadata.Tablespace)}",
      settings.CreateDatabaseMetadata.Tablespace.ToMdCode());
    st.AddRow($"{nameof(settings.CreateDatabaseMetadata)}.{nameof(settings.CreateDatabaseMetadata.ConnectionLimit)}",
      settings.CreateDatabaseMetadata.ConnectionLimit.ToString().ToMdCode());

    st.AddRow(nameof(settings.OwningUserTemplate),
      $"Name={settings.OwningUserTemplate.Name}, Password={settings.OwningUserTemplate.Password}".ToMdCode());
    foreach (var u in settings.ReadWriteUserTemplates)
      st.AddRow(nameof(settings.ReadWriteUserTemplates), $"Name={u.Name}, Password={u.Password}".ToMdCode());

    foreach (var u in settings.ReadonlyUserTemplates)
      st.AddRow(nameof(settings.ReadWriteUserTemplates), $"Name={u.Name}, Password={u.Password}".ToMdCode());

    st.AddRow(nameof(settings.AuditLogPath), settings.AuditLogPath.ToMdCode());


    var ut = new ConsoleTable("USER", "PASSWORD")
    {
      MaxWidth = 300
    };
    foreach (var u in ctx.SetupModel.GetUsers())
      ut.AddRow(u.Name, u.Password.ToMdCode());
    
    _log.Add("settingsTable", st.ToMarkDownString());
    _log.Add("usersTable", ut.ToMarkDownString());
    _log.Add("currentDatabase", ctx.SetupModel.Database);
    _log.Add("currentSchema", ctx.SetupModel.Schema);
    _log.Add("dropDatabaseStatementSkipped", false);
    _log.Add("dbObjectsOwner", ctx.SetupModel.OwningUser.Name);
  }

  public void AddConnectionStrings(string text)
    => _log.Add("connectionStrings", text);
  
  public void AddDropSchemaStatement(string text)
    => _log.Add("dropSchemaStatement", text);

  public void AddDropDatabaseStatementSkipped()
    => _log.Add("dropDatabaseStatementSkipped", true);

  public void AddDropDatabaseStatement(string text)
    => _log.Add("dropDatabaseStatement", text);

  public void AddDropUsersStatement(string text)
    => _log.Add("dropUserStatement", text);

  public void AddUserCreationStatements(string text)
    => _log.Add("userCreationStatements", text);

  public void AddDatabaseCreationStatement(string text)
    => _log.Add("databaseCreationStatements", text);

  public void AddSchemaCreationStatement(string text)
    => _log.Add("schemaCreationStatements", text);

  public void AddGrantUsageStatement(string text)
    => _log.Add("grantUsageStatements", text);

  public void AddGrantDefaultPrivilegesStatements(string text)
    => _log.Add("grantDefaultPrivilegesStatements", text);

  public void AddException(Exception e)
    => _log.Add("exception", e.ToString());
}