using QuickSetup.Commands.Postgres;

namespace QuickSetup.Common.Abstractions;

public interface ITracingService
{
  void WriteStartLog(PostgresSetupCommand command);
  void WriteDatabaseObjectRemovalStartLog();
  
  void WriteCodeBlockMarker();
  void WriteLine(string text);
}