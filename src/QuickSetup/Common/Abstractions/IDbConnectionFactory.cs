using System.Data.Common;

namespace QuickSetup.Common.Abstractions;

public interface IDbConnectionFactory
{
  DbConnection GetPostgresConnection(string name, string? username = null, string? password = null);
}