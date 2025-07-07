using DotMake.CommandLine;
using Npgsql;
using QuickSetup.Common;
using Tomlyn;

namespace QuickSetup.Commands;

[CliCommand(Description = "Initialize empty settings file", Parent = typeof(RootCommand),
  Alias = "init")]
public sealed class InitializeSettingsCommand : ICliRun
{
  [CliArgument(Description = "Name or path of the output file.")]
  public string Output { get; set; } = SettingsFactory.DefaultSettingsFile;

  [CliOption(Description = "Replace existing file", Alias = "r")]
  public bool Replace { get; set; }

  public void Run()
  {
    if (!Output.EndsWith("toml"))
      throw new ArgumentException("Command execution failed: Output file must be TOML file");

    if (File.Exists(Output) && !Replace)
      throw new InvalidOperationException(
        "Command execution failed: Output file already exist and the replace flag is not set");

    var settings = new QuickSetupSettings();
    var builder = new NpgsqlConnectionStringBuilder();
    builder.Host = "localhost";
    builder.Port = 5432;
    builder.Username = "postgres";
    builder.Password = "<PASSWORD>";
    builder.Database = "postgres";
    builder.SearchPath = "public";
    builder.IncludeErrorDetail = true;
    settings.ConnectionStrings = new Dictionary<string, string>
    {
      ["connection_name"] = builder.ToString()
    };

    settings.DatabaseSchemaDefinitions = new Dictionary<string, List<string>>
    {
      ["db01"] = ["schema01", "schema02", "schema03"],
      ["db02"] = ["schema01", "schema02", "schema03"]
    };
    var toml = Toml.FromModel(settings);
    File.WriteAllText(Path.GetFullPath(Output), toml);
  }
}