using System.Drawing;
using System.IO;
using System.Windows.Forms;
using ControllersOff.Views;

namespace ControllersOff;

public sealed class TrayIconHost : IDisposable
{
    private readonly NotifyIcon _notifyIcon;
    private readonly Func<MainWindow> _windowFactory;

    public TrayIconHost(Func<MainWindow> windowFactory)
    {
        _windowFactory = windowFactory;

        var menu = new ContextMenuStrip();
        menu.Items.Add(Loc.Instance["Open"], null, (_, _) => _windowFactory().ShowFlyout());
        menu.Items.Add(Loc.Instance["EnableAll"], null, (_, _) => App.RunDeviceCommand(["--enable-all"]));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(Loc.Instance["Exit"], null, (_, _) => System.Windows.Application.Current.Shutdown());

        _notifyIcon = new NotifyIcon
        {
            Icon = LoadIcon(),
            ContextMenuStrip = menu,
            Text = Loc.Instance["AppTitle"],
            Visible = true
        };

        _notifyIcon.MouseClick += (_, e) =>
        {
            if (e.Button == MouseButtons.Left)
            {
                _windowFactory().ToggleFlyout();
            }
        };
    }

    private static Icon LoadIcon()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Assets", "icon.ico");
        return File.Exists(path) ? new Icon(path) : SystemIcons.Application;
    }

    public void Dispose() => _notifyIcon.Dispose();
}
