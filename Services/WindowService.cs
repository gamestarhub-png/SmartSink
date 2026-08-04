using SmartSink.Views;

namespace SmartSink.Services;

public sealed class WindowService
{
    private readonly MainWindow _window;

    public WindowService(MainWindow window) => _window = window;

    public void Show() => _window.ShowAndActivate();
    public void Hide() => _window.Hide();

    public void CloseForExit()
    {
        _window.MarkExplicitExit();
        _window.Close();
    }
}
