using ConsoleTables;
using Cottle;
using QuickSetup.Common;
using QuickSetup.Models.Input;

namespace QuickSetup.Commands.Postgres;

public sealed class PgSetupAuditLog
{
  public Dictionary<Value, Value> Log { get; } = new();

  public void AddSettingsTable(PgSetupContext ctx)
  {
    var st = new ConsoleTable("PROPERTY", "VALUE") { MaxWidth = 300 };
    st.AddRow(nameof(ctx.CommandSettings.ConnectionStringName), ctx.CommandSettings.ConnectionStringName.ToMdCode());
    st.AddRow(nameof(ctx.CommandSettings.DropDatabases), ctx.CommandSettings.DropDatabases.ToString().ToMdCode());
    st.AddRow(nameof(ctx.CommandSettings.DropSchemas), ctx.CommandSettings.DropSchemas.ToString().ToMdCode());

    var databasesAndSchemas = string.Join(
      ", ",
      ctx.UserSettings.DatabaseSchemaSetupOptions.Select(x => $"{x.Key}:{x.Value}")
    );
    st.AddRow("Databases/Schemas", databasesAndSchemas.ToMdCode());

    st.AddRow(
      $"{nameof(DatabaseSetupOptions)}.{nameof(DatabaseSetupOptions.Owner)}",
      ctx.UserSettings.DatabaseSetupOptions.Owner.ToMdCode()
    );
    st.AddRow(
      $"{nameof(DatabaseSetupOptions)}.{nameof(DatabaseSetupOptions.Encoding)}",
      ctx.UserSettings.DatabaseSetupOptions.Encoding.ToMdCode()
    );
    st.AddRow(
      $"{nameof(DatabaseSetupOptions)}.{nameof(DatabaseSetupOptions.Tablespace)}",
      ctx.UserSettings.DatabaseSetupOptions.Tablespace.ToMdCode()
    );
    st.AddRow(
      $"{nameof(DatabaseSetupOptions)}.{nameof(DatabaseSetupOptions.ConnectionLimit)}",
      ctx.UserSettings.DatabaseSetupOptions.ConnectionLimit.ToString().ToMdCode()
    );

    st.AddRow(
      nameof(UserSetupOptions.Owner),
      $"Name={ctx.UserSettings.UserSetupOptions.Owner.Username}, Password={ctx.UserSettings.UserSetupOptions.Owner.Password}".ToMdCode()
    );
    foreach (var credentials in ctx.UserSettings.UserSetupOptions.AppUsers)
      st.AddRow(
        $"AppUserTemplate: {credentials.Username}",
        $"Name={credentials.Username}, Password={credentials.Password}".ToMdCode()
      );

    foreach (var credentials in ctx.UserSettings.UserSetupOptions.ReadOnlyUsers)
      st.AddRow(
        $"ReadonlyUserTemplate: {credentials.Username}",
        $"Name={credentials.Username}, Password={credentials.Password}".ToMdCode()
      );

    st.AddRow("AuditLogOptions.Path", ctx.UserSettings.AuditLogOptions.Path.ToMdCode());

    var ut = new ConsoleTable("USER", "PASSWORD") { MaxWidth = 300 };
    foreach (var credentials in ctx.ProcessingModel.GetUsers())
      ut.AddRow(credentials.Username, credentials.Password.ToMdCode());

    Log.Add("settingsTable", st.ToMarkDownString());
    Log.Add("usersTable", ut.ToMarkDownString());
    Log.Add("currentDatabase", ctx.ProcessingModel.DatabaseName);
    Log.Add("currentSchema", ctx.ProcessingModel.SchemaName);
    // Log.Add("dropDatabaseStatementSkipped", false);
    Log.Add("dbObjectsOwner", ctx.ProcessingModel.Owner.Username);
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
