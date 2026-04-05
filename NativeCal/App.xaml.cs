using Microsoft.UI.Xaml;
using NativeCal.Services;

namespace NativeCal;

public partial class App : Application
{
    public static MainWindow MainAppWindow { get; private set; } = null!;
    public static DatabaseService Database { get; private set; } = null!;

    public App()
    {
        this.InitializeComponent();
    }

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        Database = new DatabaseService();
        await Database.InitializeAsync();

        MainAppWindow = new MainWindow();
        MainAppWindow.Activate();
    }
}
