namespace AliasCockpit.Core.Providers;

public sealed class ProviderRegistry
{
    private readonly IReadOnlyDictionary<string, IProviderAdapter> _adapters;

    public ProviderRegistry(IEnumerable<IProviderAdapter> adapters)
    {
        ArgumentNullException.ThrowIfNull(adapters);

        var byType = new Dictionary<string, IProviderAdapter>(StringComparer.Ordinal);
        foreach (var adapter in adapters)
        {
            var providerType = ProviderTypes.Normalize(adapter.Profile.ProviderType);
            if (!byType.TryAdd(providerType, adapter))
            {
                throw new ArgumentException($"Provider adapter '{providerType}' is registered more than once.", nameof(adapters));
            }
        }

        _adapters = byType;
    }

    public IReadOnlyList<ProviderProfile> Profiles => _adapters.Values.Select(adapter => adapter.Profile).ToList();

    public bool TryGet(string providerType, out IProviderAdapter adapter)
    {
        return _adapters.TryGetValue(ProviderTypes.Normalize(providerType), out adapter!);
    }

    public IProviderAdapter GetRequired(string providerType)
    {
        if (TryGet(providerType, out var adapter))
        {
            return adapter;
        }

        throw new KeyNotFoundException($"Provider adapter '{providerType}' is not registered.");
    }
}
