using System.Windows;
using Velopack;

namespace Dograc.App;

public static class Program
{
    private const string SingleInstanceMutexName = @"Local\dograc-desktop-single-instance";

    [STAThread]
    public static void Main(string[] args)
    {
        // Velopack must run as early as possible to handle install/update/uninstall hooks.
        VelopackApp.Build().Run();

        using var instanceMutex = new Mutex(initiallyOwned: true, SingleInstanceMutexName, out var createdNew);
        if (!createdNew)
        {
            MessageBox.Show(
                "dograc Desktop이 이미 실행 중입니다.\n작업 표시줄에서 기존 dograc 창을 확인해 주세요.\n\n(오류 코드: ENV-004)",
                "dograc 이미 실행 중",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        var app = new App();
        app.InitializeComponent();
        app.Run();
    }
}
