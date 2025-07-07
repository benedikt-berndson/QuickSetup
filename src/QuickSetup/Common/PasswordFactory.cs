using System.Security.Cryptography;

namespace QuickSetup.Common;

public static class PasswordFactory
{
  public static string GetNew()
  {
    var key = new byte[50];
    using var rng = RandomNumberGenerator.Create();
    rng.GetBytes(key);
    return Convert.ToBase64String(key).ToUpperInvariant();
  }
}