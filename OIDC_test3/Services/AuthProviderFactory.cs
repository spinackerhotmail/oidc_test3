namespace OIDC_test3.Services;

public interface IAuthProviderFactory
{
    IAuthProvider GetProvider(string providerName);
    IEnumerable<string> GetAvailableProviders();
}

public class AuthProviderFactory : IAuthProviderFactory
{
    private readonly Dictionary<string, IAuthProvider> _providers;

    public AuthProviderFactory(IEnumerable<IAuthProvider> providers)
    {
        _providers = providers.ToDictionary(p => p.ProviderName, StringComparer.OrdinalIgnoreCase);
    }

    public IAuthProvider GetProvider(string providerName)
    {
        if (_providers.TryGetValue(providerName, out var provider))
            return provider;

        throw new ArgumentException($"Unknown auth provider: '{providerName}'. Available: {string.Join(", ", _providers.Keys)}");
    }

    public IEnumerable<string> GetAvailableProviders() => _providers.Keys;
}
