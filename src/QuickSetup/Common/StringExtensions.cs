namespace QuickSetup.Common;

public static class StringExtensions
{
  public static string ToMdCode(this string input)
  => $"`{input}`";
}