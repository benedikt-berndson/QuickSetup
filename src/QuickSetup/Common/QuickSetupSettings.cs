namespace QuickSetup.Common;

public sealed class QuickSetupSettings
{
  public string? MarkdownOutputFilePath { get; set; }
  public Dictionary<string, string> ConnectionStrings { get; set; } = null!;
  public Dictionary<string, List<string>> DatabaseSchemaDefinitions { get; set; } = null!;
  public string MachineUserNameMiddlePart { get; set; } = "_machine_user_";
  public string AppUserNameMiddlePart { get; set; } = "_app_user_";
  public string ReadonlyUserNameMiddlePart { get; set; } = "_readonly_user_";
}