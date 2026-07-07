using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using AliasCockpit.Core.Secrets;

namespace AliasCockpit.Infrastructure.Security;

public sealed class WindowsCredentialManagerSecretStore(string applicationName = "AliasCockpit") : ISecretStore
{
    private const int CredentialTypeGeneric = 1;
    private const int CredentialPersistLocalMachine = 2;
    private const int ErrorNotFound = 1168;
    private const int MaxCredentialBlobBytes = 2560;

    private readonly string _applicationName = SanitizeApplicationName(applicationName);

    public Task SetSecretAsync(string key, string secret, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureWindows();
        SecretKey.Validate(key);
        ArgumentNullException.ThrowIfNull(secret);

        var targetName = BuildTargetName(key);
        var secretBytes = Encoding.UTF8.GetBytes(secret);
        if (secretBytes.Length > MaxCredentialBlobBytes)
        {
            throw new ArgumentException($"Secret is too large for Windows Credential Manager. Maximum is {MaxCredentialBlobBytes} UTF-8 bytes.", nameof(secret));
        }

        var blob = Marshal.AllocCoTaskMem(secretBytes.Length);
        try
        {
            Marshal.Copy(secretBytes, 0, blob, secretBytes.Length);

            var credential = new NativeCredential
            {
                Type = CredentialTypeGeneric,
                TargetName = targetName,
                CredentialBlobSize = (uint)secretBytes.Length,
                CredentialBlob = blob,
                Persist = CredentialPersistLocalMachine,
                UserName = _applicationName,
            };

            if (!CredWrite(ref credential, 0))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Failed to write secret to Windows Credential Manager.");
            }
        }
        finally
        {
            ZeroMemory(blob, secretBytes.Length);
            Marshal.FreeCoTaskMem(blob);
            CryptographicOperationsFallback.ZeroMemory(secretBytes);
        }

        return Task.CompletedTask;
    }

    public Task<string?> GetSecretAsync(string key, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureWindows();
        SecretKey.Validate(key);

        var targetName = BuildTargetName(key);
        if (!CredRead(targetName, CredentialTypeGeneric, 0, out var credentialPointer))
        {
            var error = Marshal.GetLastWin32Error();
            if (error == ErrorNotFound)
            {
                return Task.FromResult<string?>(null);
            }

            throw new Win32Exception(error, "Failed to read secret from Windows Credential Manager.");
        }

        try
        {
            var credential = Marshal.PtrToStructure<NativeCredential>(credentialPointer);
            if (credential.CredentialBlob == IntPtr.Zero || credential.CredentialBlobSize == 0)
            {
                return Task.FromResult<string?>(string.Empty);
            }

            var bytes = new byte[credential.CredentialBlobSize];
            Marshal.Copy(credential.CredentialBlob, bytes, 0, bytes.Length);
            try
            {
                return Task.FromResult<string?>(Encoding.UTF8.GetString(bytes));
            }
            finally
            {
                CryptographicOperationsFallback.ZeroMemory(bytes);
            }
        }
        finally
        {
            CredFree(credentialPointer);
        }
    }

    public Task DeleteSecretAsync(string key, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureWindows();
        SecretKey.Validate(key);

        var targetName = BuildTargetName(key);
        if (!CredDelete(targetName, CredentialTypeGeneric, 0))
        {
            var error = Marshal.GetLastWin32Error();
            if (error != ErrorNotFound)
            {
                throw new Win32Exception(error, "Failed to delete secret from Windows Credential Manager.");
            }
        }

        return Task.CompletedTask;
    }

    private string BuildTargetName(string key)
    {
        return $"{_applicationName}/{key}";
    }

    private static string SanitizeApplicationName(string applicationName)
    {
        if (string.IsNullOrWhiteSpace(applicationName))
        {
            throw new ArgumentException("Application name is required.", nameof(applicationName));
        }

        return applicationName.Trim().Replace('\\', '/');
    }

    private static void EnsureWindows()
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("Windows Credential Manager is only available on Windows.");
        }
    }

    private static void ZeroMemory(IntPtr pointer, int length)
    {
        if (pointer == IntPtr.Zero || length <= 0)
        {
            return;
        }

        var zeros = new byte[length];
        Marshal.Copy(zeros, 0, pointer, length);
    }

    [DllImport("Advapi32.dll", EntryPoint = "CredWriteW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredWrite(ref NativeCredential userCredential, uint flags);

    [DllImport("Advapi32.dll", EntryPoint = "CredReadW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredRead(string target, int type, int reservedFlag, out IntPtr credentialPointer);

    [DllImport("Advapi32.dll", EntryPoint = "CredDeleteW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredDelete(string target, int type, int flags);

    [DllImport("Advapi32.dll", EntryPoint = "CredFree", SetLastError = false)]
    private static extern void CredFree(IntPtr buffer);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NativeCredential
    {
        public uint Flags;
        public uint Type;
        public string TargetName;
        public string? Comment;
        public System.Runtime.InteropServices.ComTypes.FILETIME LastWritten;
        public uint CredentialBlobSize;
        public IntPtr CredentialBlob;
        public uint Persist;
        public uint AttributeCount;
        public IntPtr Attributes;
        public string? TargetAlias;
        public string? UserName;
    }
}

internal static class CryptographicOperationsFallback
{
    public static void ZeroMemory(byte[] bytes)
    {
        if (bytes.Length == 0)
        {
            return;
        }

        System.Security.Cryptography.CryptographicOperations.ZeroMemory(bytes);
    }
}
