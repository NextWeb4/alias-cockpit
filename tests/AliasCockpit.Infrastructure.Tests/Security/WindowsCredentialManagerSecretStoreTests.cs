using AliasCockpit.Infrastructure.Security;

namespace AliasCockpit.Infrastructure.Tests.Security;

public sealed class WindowsCredentialManagerSecretStoreTests
{
    [WindowsOnlyFact]
    public async Task SecretRoundTripUsesWindowsCredentialManager()
    {
        var store = new WindowsCredentialManagerSecretStore("AliasCockpit.Tests");
        var key = $"provider-token/{Guid.NewGuid():D}";

        try
        {
            await store.SetSecretAsync(key, "test-secret-value");

            Assert.Equal("test-secret-value", await store.GetSecretAsync(key));

            await store.DeleteSecretAsync(key);

            Assert.Null(await store.GetSecretAsync(key));
        }
        finally
        {
            await store.DeleteSecretAsync(key);
        }
    }
}

public sealed class WindowsOnlyFactAttribute : FactAttribute
{
    public WindowsOnlyFactAttribute()
    {
        if (!OperatingSystem.IsWindows())
        {
            Skip = "Windows Credential Manager is only available on Windows.";
        }
    }
}
