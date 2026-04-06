using System;
using Microsoft.UI.Xaml;
using NativeCal.Services;

namespace NativeCal;

public partial class App : Application
{
    public static MainWindow MainAppWindow { get; private set; } = null!;
    public static DatabaseService Database { get; private set; } = null!;
    public static HolidayService HolidayService { get; private set; } = null!;

    /// <summary>
    /// The user-configured first day of the week (Sunday, Monday, or Saturday).
    /// Loaded from settings at startup and updated when the user changes the setting.
    /// Defaults to Sunday.
    /// </summary>
    public static DayOfWeek FirstDayOfWeek { get; set; } = DayOfWeek.Sunday;

    public App()
    {
        this.InitializeComponent();
    }

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        Database = new DatabaseService();
        await Database.InitializeAsync();
        HolidayService = new HolidayService();

        // Load first day of week setting before creating the window so views
        // can read App.FirstDayOfWeek immediately during construction.
        await LoadFirstDayOfWeekAsync();

        MainAppWindow = new MainWindow();
        MainAppWindow.Activate();
    }

    /// <summary>
    /// Reads the persisted "FirstDayOfWeek" setting (0 = Sunday, 1 = Monday, 6 = Saturday)
    /// and updates the static <see cref="FirstDayOfWeek"/> property.
    /// </summary>
    public static async System.Threading.Tasks.Task LoadFirstDayOfWeekAsync()
    {
        string value = await Database.GetSettingAsync("FirstDayOfWeek", "0");
        if (int.TryParse(value, out int dayValue))
        {
            FirstDayOfWeek = dayValue switch
            {
                1 => DayOfWeek.Monday,
                6 => DayOfWeek.Saturday,
                _ => DayOfWeek.Sunday
            };
        }
    }
}
