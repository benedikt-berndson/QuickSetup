using Spectre.Console.Cli;

namespace QuickSetup;

public sealed class QuickSetupTypeResolver(IServiceProvider provider) : ITypeResolver, IDisposable
{
  private readonly IServiceProvider _provider = provider ?? throw new ArgumentNullException(nameof(provider));

  public object Resolve(Type? type)
  {
    return (type == null ? null : _provider.GetService(type)) ?? throw new InvalidOperationException();
  }

  public void Dispose()
  {
    if (_provider is IDisposable disposable)
    {
      disposable.Dispose();
    }
  }
}
