using System.Runtime.InteropServices;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;

namespace AliasCockpit.App;

public static class Program
{
    [DllImport("Microsoft.WindowsAppRuntime.dll", ExactSpelling = true)]
    private static extern int WindowsAppRuntime_EnsureIsLoaded();

    [STAThread]
    public static void Main(string[] args)
    {
        InitializeWindowsAppRuntime();

        WinRT.ComWrappersSupport.InitializeComWrappers();
        Application.Start(_ =>
        {
            var context = new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread());
            SynchronizationContext.SetSynchronizationContext(context);
            new App();
        });
    }

    private static void InitializeWindowsAppRuntime()
    {
        Environment.SetEnvironmentVariable("MICROSOFT_WINDOWSAPPRUNTIME_BASE_DIRECTORY", AppContext.BaseDirectory);

        var hr = WindowsAppRuntime_EnsureIsLoaded();
        if (hr < 0)
        {
            Marshal.ThrowExceptionForHR(hr);
        }
    }
}
