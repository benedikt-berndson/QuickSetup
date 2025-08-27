using ConsoleTables;
using Cottle;
using QuickSetup.Common;

namespace QuickSetup.Commands.Postgres;

public sealed class PgSetupAuditLog()
{
  public Dictionary<Value, Value> Log { get; } = new();

  public void AddSettingsTable(PgSetupContext ctx)
  {
    var st = new ConsoleTable("PROPERTY", "VALUE") { MaxWidth = 300 };
    st.AddRow(nameof(ctx.CommandSettings.ConnectionStringName), ctx.CommandSettings.ConnectionStringName.ToMdCode());
    st.AddRow(nameof(ctx.CommandSettings.DropDatabases), ctx.CommandSettings.DropDatabases.ToString().ToMdCode());
    st.AddRow(nameof(ctx.CommandSettings.DropSchemas), ctx.CommandSettings.DropSchemas.ToString().ToMdCode());
    ;
    var databasesAndSchemas = string.Join(", ", ctx.SettingsFile.DatabaseToSchemaMap.Select(x => $"{x.Key}:{x.Value}"));
    st.AddRow("Databases/Schemas", databasesAndSchemas.ToMdCode());

    st.AddRow(
      $"{nameof(ctx.SettingsFile.CreateDatabaseMetadata)}.{nameof(ctx.SettingsFile.CreateDatabaseMetadata.Owner)}",
      ctx.SettingsFile.CreateDatabaseMetadata.Owner.ToMdCode()
    );
    st.AddRow(
      $"{nameof(ctx.SettingsFile.CreateDatabaseMetadata)}.{nameof(ctx.SettingsFile.CreateDatabaseMetadata.Encoding)}",
      ctx.SettingsFile.CreateDatabaseMetadata.Encoding.ToMdCode()
    );
    st.AddRow(
      $"{nameof(ctx.SettingsFile.CreateDatabaseMetadata)}.{nameof(ctx.SettingsFile.CreateDatabaseMetadata.Tablespace)}",
      ctx.SettingsFile.CreateDatabaseMetadata.Tablespace.ToMdCode()
    );
    st.AddRow(
      $"{nameof(ctx.SettingsFile.CreateDatabaseMetadata)}.{nameof(ctx.SettingsFile.CreateDatabaseMetadata.ConnectionLimit)}",
      ctx.SettingsFile.CreateDatabaseMetadata.ConnectionLimit.ToString().ToMdCode()
    );

    st.AddRow(
      nameof(ctx.SettingsFile.OwningUserTemplate),
      $"Name={ctx.SettingsFile.OwningUserTemplate.Name}, Password={ctx.SettingsFile.OwningUserTemplate.Password}".ToMdCode()
    );
    foreach (var u in ctx.SettingsFile.ReadWriteUserTemplates)
      st.AddRow(nameof(ctx.SettingsFile.ReadWriteUserTemplates), $"Name={u.Name}, Password={u.Password}".ToMdCode());

    foreach (var u in ctx.SettingsFile.ReadonlyUserTemplates)
      st.AddRow(nameof(ctx.SettingsFile.ReadWriteUserTemplates), $"Name={u.Name}, Password={u.Password}".ToMdCode());

    st.AddRow(nameof(ctx.SettingsFile.AuditLogPath), ctx.SettingsFile.AuditLogPath.ToMdCode());

    var ut = new ConsoleTable("USER", "PASSWORD") { MaxWidth = 300 };
    foreach (var u in ctx.SetupModel.GetUsers())
      ut.AddRow(u.Name, u.Password.ToMdCode());

    Log.Add("settingsTable", st.ToMarkDownString());
    Log.Add("usersTable", ut.ToMarkDownString());
    Log.Add("currentDatabase", ctx.SetupModel.Database);
    Log.Add("currentSchema", ctx.SetupModel.Schema);
    // Log.Add("dropDatabaseStatementSkipped", false);
    Log.Add("dbObjectsOwner", ctx.SetupModel.OwningUser.Name);
  }

  public void AddConnectionStrings(string text) => Log.Add("connectionStrings", text);

  public void AddDropSchemaStatement(string text) => Log.Add("dropSchemaStatement", text);

  public void AddDropDatabaseStatementSkipped() => Log.Add("dropDatabaseStatementSkipped", true);

  public void AddDropDatabaseStatement(string text) => Log.Add("dropDatabaseStatement", text);

  public void AddDropUsersStatement(string text) => Log.Add("dropUserStatement", text);

  public void AddUserCreationStatements(string text) => Log.Add("userCreationStatements", text);

  public void AddDatabaseCreationStatement(string text) => Log.Add("databaseCreationStatements", text);

  public void AddSchemaCreationStatement(string text) => Log.Add("schemaCreationStatements", text);

  public void AddGrantUsageStatement(string text) => Log.Add("grantUsageStatements", text);

  public void AddGrantDefaultPrivilegesStatements(string text) => Log.Add("grantDefaultPrivilegesStatements", text);

  public void AddException(Exception e) => Log.Add("exception", e.ToString());
}
