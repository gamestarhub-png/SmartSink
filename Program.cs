using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;

namespace SmartSink;

public static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        WinRT.ComWrappersSupport.InitializeComWrappers();

        AppInstance current = AppInstance.GetCurrent();
        AppActivationArguments activation = current.GetActivatedEventArgs();
        AppInstance primary = AppInstance.FindOrRegisterForKey("SmartSink.PrimaryInstance");

        if (!primary.IsCurrent)
        {
            primary.RedirectActivationToAsync(activation).AsTask().GetAwaiter().GetResult();
            return;
        }

        Application.Start(initialization =>
        {
            var context = new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread());
            SynchronizationContext.SetSynchronizationContext(context);
            new App(activation);
        });
    }
}
