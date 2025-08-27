using Dapper;
using Npgsql;
using QuickSetup.Common.Abstractions;

namespace QuickSetup.Commands.Postgres;

public sealed class PostgresRepository : IPostgresRepository
{
  private const string Na = "n/a";

  // key = (connectionStringName, database, username, password)
  private readonly Dictionary<(string, string, string, string), NpgsqlDataSource> _dataSources = new();
  private readonly Dictionary<string, NpgsqlDataSource> _dataSources2 = new();
  private readonly ISettingsProvider _settingsProvider;

  public PostgresRepository(ISettingsProvider settingsProvider)
  {
    _settingsProvider = settingsProvider;
  }

  public void ExecuteAsRootAdmin(string connectionName, string sql)
  {
    using var connection = GetConnection(connectionName);
    using var command = new NpgsqlCommand(sql, connection);
    command.ExecuteNonQuery();
  }

  public void ExecuteAsDbScopedAdmin(string connectionName, string database, string sql)
  {
    using var connection = GetConnection(connectionName, database);
    using var command = new NpgsqlCommand(sql, connection);
    command.ExecuteNonQuery();
  }

  public void ExecuteAsSchemaScopedAdmin(string connectionName, string database, string schema, string sql)
  {
    using var connection = GetConnection(connectionName, database, schema);
    ;
    using var command = new NpgsqlCommand(sql, connection);
    command.ExecuteNonQuery();
  }

  public void ExecuteAsOwningUser(
    string connectionName,
    string database,
    string schema,
    string username,
    string password,
    string sql
  )
  {
    using var connection = GetConnection(connectionName, database, schema, username, password);
    using var command = new NpgsqlCommand(sql, connection);
    command.ExecuteNonQuery();
  }

  public HashSet<string> GetDatabaseNames(string connectionName)
  {
    using var adminConnection = GetConnection(connectionName);
    return adminConnection.Query<string>("SELECT datname FROM pg_database").ToHashSet();
  }

  public HashSet<string> GetSchemaNames(string connectionName, string database)
  {
    const string schemaQuery = """
      SELECT schema_name
      FROM information_schema.schemata
      WHERE schema_name NOT IN ('information_schema', 'pg_catalog', 'pg_toast')
        AND schema_name NOT LIKE 'pg_temp_%'
        AND schema_name NOT LIKE 'pg_toast_temp_%'
      ORDER BY schema_name;
      """;

    using var connection = GetConnection(connectionName, database);
    return connection.Query<string>(schemaQuery).Select(x => x.ToLower()).ToHashSet();
  }

  private NpgsqlConnection GetConnection(
    string name,
    string? database = null,
    string? schema = null,
    string? username = null,
    string? password = null
  )
  {
    var settings = _settingsProvider.GetSettings();
    var b = new NpgsqlConnectionStringBuilder(settings.ConnectionStrings[name]) { Pooling = false };
    // Use defaults of admin connection string, unless overwritten
    if (database != null)
      b.Database = database;
    if (schema != null)
      b.SearchPath = schema;
    if (username != null)
      b.Username = username;
    if (password != null)
      b.Password = password;

    var connectionString = b.ToString();

    if (_dataSources2.TryGetValue(connectionString, out var dataSource))
      return dataSource.OpenConnection();

    dataSource = new NpgsqlDataSourceBuilder(connectionString).Build();
    _dataSources2.Add(connectionString, dataSource);

    return dataSource.OpenConnection();
  }
}
