namespace QuickSetup.Common;

public static class PasswordFactory
{
  public static string GetNew() =>
    $"{Guid.NewGuid().ToString("N").ToUpperInvariant()}_{Guid.NewGuid().ToString("N").ToUpperInvariant()}";
}
