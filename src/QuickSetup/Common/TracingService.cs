using System.Text;
using ConsoleTables;
using QuickSetup.Commands.Postgres;
using QuickSetup.Common.Abstractions;

namespace QuickSetup.Common;

public sealed class TracingService(ISettingsFactory settingsFactory) : ITracingService
{
  private static readonly StringBuilder Global = new();

  public void WriteStartLog(PostgresSetupCommand command)
  {
    var s = settingsFactory.GetInstance();
    var sb = new StringBuilder();
    sb.AppendLine("# DATABASE SETUP COMMAND EXECUTION AUDIT LOG");
    sb.AppendLine("## USED SETTINGS");
    sb.AppendLine();

    var table = new ConsoleTable("NAME", "VALUE");
    table.AddRow(nameof(command.ConnectionStringName), command.ConnectionStringName);
    table.AddRow(nameof(command.DropDatabases), command.DropDatabases);
    table.AddRow(nameof(command.DropSchemas), command.DropSchemas);
    table.AddRow(nameof(command.DropUsers), command.DropUsers);
    foreach (var pair in s.DatabaseSchemaDefinitions)
    {
      table.AddRow($"Database: {pair.Key}", string.Join(", ", pair.Value));
    }

    table.AddRow(nameof(s.MachineUserNameMiddlePart), s.MachineUserNameMiddlePart);
    table.AddRow(nameof(s.AppUserNameMiddlePart), s.AppUserNameMiddlePart);
    table.AddRow(nameof(s.ReadonlyUserNameMiddlePart), s.ReadonlyUserNameMiddlePart);
    table.AddRow(nameof(s.MarkdownOutputFilePath), GetMarkdownLogOutputPath());
    table.MaxWidth = 300;
    sb.Append(table.ToMarkDownString());
    sb.AppendLine();
    sb.AppendLine("## COMMAND EXECUTION LOG");
    sb.AppendLine();

    Console.Write(sb.ToString());
    Global.Append(sb);
  }

  public void WriteDatabaseObjectRemovalStartLog()
  {
    var sb = new StringBuilder();
    sb.AppendLine("### DATABASE OBJECT REMOVAL");
    sb.AppendLine();
    Console.Write(sb.ToString());
    Global.Append(sb);
  }

  public void WriteCodeBlockMarker()
  {
    Global.AppendLine("```").AppendLine();
    Console.WriteLine();
  }

  public void WriteLine(string text)
  {
    Global.AppendLine(text);
    Console.WriteLine(text);
  }

  public void WriteEndLog()
  {
    File.WriteAllText(GetMarkdownLogOutputPath(), Global.ToString());
  }

  private string GetMarkdownLogOutputPath()
  {
    var s = settingsFactory.GetInstance();
    return string.IsNullOrEmpty(s.MarkdownOutputFilePath)
      ? "setup_log.md"
      : s.MarkdownOutputFilePath;
  }
}