using System.Data.Common;
using Npgsql;
using QuickSetup.Common.Abstractions;

namespace QuickSetup.Common;

public sealed class DbConnectionFactory : IDbConnectionFactory
{
  private const string NotApplicable = "n/a";

  // key = (connectionStringName, username, password)
  private readonly Dictionary<(string, string, string), NpgsqlDataSource> _dataSources = new();
  private readonly ISettingsFactory _settingsFactory;

  public DbConnectionFactory(ISettingsFactory settingsFactory)
  {
    _settingsFactory = settingsFactory;
  }

  public DbConnection GetPostgresConnection(string name, string? username = null, string? password = null)
  {
    if (_dataSources.TryGetValue((name, NotApplicable, NotApplicable), out var ds)) return ds.OpenConnection();

    var settings = _settingsFactory.GetInstance();
    ds = new NpgsqlSlimDataSourceBuilder(settings.ConnectionStrings[name]).Build();
    _dataSources.Add((name, NotApplicable, NotApplicable), ds);

    return ds.OpenConnection();
  }
}