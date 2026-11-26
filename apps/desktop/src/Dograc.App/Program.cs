using Velopack;

namespace Dograc.App;

public static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        // Velopack must run as early as possible to handle install/update/uninstall hooks.
        VelopackApp.Build().Run();

        var app = new App();
        app.InitializeComponent();
        app.Run();
    }
}
