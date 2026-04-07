using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using NativeCal.Helpers;
using NativeCal.Models;
using NativeCal.Views;
using Windows.Graphics;
using WinRT.Interop;
using ColorHelper = NativeCal.Helpers.ColorHelper;

namespace NativeCal;

public sealed partial class MainWindow : Window
{
    public static MainWindow? CurrentInstance { get; private set; }

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr hwnd);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    private static extern IntPtr CallWindowProc(IntPtr lpPrevWndFunc, IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    private const int GWLP_WNDPROC = -4;
    private const uint WM_GETMINMAXINFO = 0x0024;

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int x; public int y; }

    [StructLayout(LayoutKind.Sequential)]
    private struct MINMAXINFO
    {
        public POINT ptReserved;
        public POINT ptMaxSize;
        public POINT ptMaxPosition;
        public POINT ptMinTrackSize;
        public POINT ptMaxTrackSize;
    }

    // Must be stored as field to prevent GC collection of the delegate
    private delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
    private WndProcDelegate? _wndProcDelegate;
    private IntPtr _oldWndProc;

    private DateTime _currentDate = DateTime.Today;

    public DateTime CurrentDate => _currentDate;

    // Page cache — each page type is created once and reused.
    private readonly Dictionary<string, Page> _pageCache = new();

    // Root element cache — extracted from pages to bypass Page's infinite-width measurement.
    private readonly Dictionary<string, UIElement> _rootElements = new();

    private Dictionary<int, CalendarInfo> _calendarLookup = new();

    // Track current view tag
    private string _currentViewTag = "month";

    public MainWindow()
    {
        CurrentInstance = this;
        this.InitializeComponent();

        // Extend content into the title bar for seamless Mica integration
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        Title = "NativeCal";
        ApplyWindowIcon();

        // Size and center the window
        SizeAndCenterWindow(1100, 750);

        // Set minimum window size (420x400 DIPs)
        SetMinWindowSize(420, 400);

        // Set caption button spacer to match actual system caption button width
        UpdateCaptionButtonWidth();

        // Sync the PaneColumn width with the NavigationView pane state
        UpdatePaneColumnWidth();

        // Apply saved theme BEFORE navigating so pages get the correct theme brushes
        ApplySavedThemeSync();

        // Listen for theme changes on the root element so we can refresh all pages
        if (Content is FrameworkElement rootElement)
        {
            rootElement.ActualThemeChanged += RootElement_ActualThemeChanged;
        }

        // Navigate to the default view
        NavigateToView("month");

        // Select the first menu item ("Month")
        NavView.SelectedItem = NavView.MenuItems[0];

        // Update the header to reflect today's month
        UpdateHeaderTitle(FormatHeaderForView("month", _currentDate));

        // Load sidebar calendar list
        _ = LoadCalendarListAsync();
    }

    // ── Content area sizing ───────────────────────────────────────────
    // ContentHost is a plain Grid. Its single * column guarantees children
    // are measured with the finite column width — no ContentPresenter involved.
    // No additional size-fixing is needed.

    private void ContentAreaGrid_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateResponsiveLayout(e.NewSize.Width);
    }

    private void UpdateCaptionButtonWidth()
    {
        var hwnd = WindowNative.GetWindowHandle(this);
        uint dpi = GetDpiForWindow(hwnd);
        double scale = dpi / 96.0;

        // Windows 11 caption buttons are ~138 DIPs wide (3 buttons × 46 DIPs).
        // Scale to physical, then use as the spacer.
        double captionWidthDips = 140;
        if (scale > 1.0)
        {
            // Already in DIPs — DPI-aware apps get correct measurement
            CaptionButtonColumn.Width = new GridLength(captionWidthDips);
        }
        else
        {
            CaptionButtonColumn.Width = new GridLength(captionWidthDips);
        }
    }

    // ── Pane column width sync ──────────────────────────────────────────

    private void NavView_PaneOpening(NavigationView sender, object args)
    {
        PaneColumn.Width = new GridLength(GetPaneColumnWidth());
        PaneFooterPanel.Visibility = Visibility.Visible;
        UpdatePaneFooterLayout();
    }

    private void NavView_PaneClosing(NavigationView sender, NavigationViewPaneClosingEventArgs args)
    {
        PaneColumn.Width = new GridLength(sender.CompactPaneLength);
        PaneFooterPanel.Visibility = Visibility.Collapsed;
        SidebarCalendar.Visibility = Visibility.Collapsed;
    }

    private void UpdatePaneColumnWidth()
    {
        PaneColumn.Width = new GridLength(GetPaneColumnWidth());
        UpdatePaneFooterLayout();
    }

    private double GetPaneColumnWidth()
    {
        if (!NavView.IsPaneOpen)
        {
            return NavView.CompactPaneLength;
        }

        return UseOverlayPaneLayout() ? NavView.CompactPaneLength : NavView.OpenPaneLength;
    }

    private bool UseOverlayPaneLayout()
    {
        double windowWidth = RootGrid.ActualWidth;
        if (windowWidth <= 0)
        {
            windowWidth = PaneColumn.ActualWidth + ContentAreaGrid.ActualWidth;
        }

        return windowWidth > 0 && windowWidth < 680;
    }

    private void UpdatePaneFooterLayout()
    {
        if (!NavView.IsPaneOpen)
        {
            SidebarCalendar.Visibility = Visibility.Collapsed;
            return;
        }

        double windowWidth = RootGrid.ActualWidth;
        if (windowWidth <= 0)
        {
            windowWidth = PaneColumn.ActualWidth + ContentAreaGrid.ActualWidth;
        }

        // The mini calendar becomes unreadable on the smallest supported widths,
        // so collapse it and keep the calendar visibility list usable instead.
        bool hideMiniCalendar = UseOverlayPaneLayout();
        SidebarCalendar.Visibility = hideMiniCalendar ? Visibility.Collapsed : Visibility.Visible;
    }

    private void SizeAndCenterWindow(int width, int height)
    {
        var hwnd = WindowNative.GetWindowHandle(this);
        var windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
        var appWindow = AppWindow.GetFromWindowId(windowId);

        // Get DPI scale factor for proper sizing
        uint dpi = GetDpiForWindow(hwnd);
        double scale = dpi / 96.0;

        // Scale the requested DIPs to physical pixels
        int scaledWidth = (int)(width * scale);
        int scaledHeight = (int)(height * scale);

        // Get the display area for the monitor this window is on
        var displayArea = DisplayArea.GetFromWindowId(windowId, DisplayAreaFallback.Primary);
        var workArea = displayArea.WorkArea;

        // Clamp to work area if needed
        scaledWidth = Math.Min(scaledWidth, workArea.Width);
        scaledHeight = Math.Min(scaledHeight, workArea.Height);

        // Calculate center position
        int x = (workArea.Width - scaledWidth) / 2 + workArea.X;
        int y = (workArea.Height - scaledHeight) / 2 + workArea.Y;

        appWindow.MoveAndResize(new RectInt32(x, y, scaledWidth, scaledHeight));
    }

    private void ApplyWindowIcon()
    {
        var hwnd = WindowNative.GetWindowHandle(this);
        var windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
        var appWindow = AppWindow.GetFromWindowId(windowId);
        string iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "Native-Cal-windows.ico");

        if (File.Exists(iconPath))
        {
            appWindow.SetIcon(iconPath);
        }
    }

    private void SetMinWindowSize(int minWidthDips, int minHeightDips)
    {
        var hwnd = WindowNative.GetWindowHandle(this);
        uint dpi = GetDpiForWindow(hwnd);
        double scale = dpi / 96.0;
        int minWidthPx = (int)(minWidthDips * scale);
        int minHeightPx = (int)(minHeightDips * scale);

        _wndProcDelegate = (hWnd, msg, wParam, lParam) =>
        {
            if (msg == WM_GETMINMAXINFO)
            {
                var mmi = Marshal.PtrToStructure<MINMAXINFO>(lParam);
                mmi.ptMinTrackSize.x = minWidthPx;
                mmi.ptMinTrackSize.y = minHeightPx;
                Marshal.StructureToPtr(mmi, lParam, false);
                return IntPtr.Zero;
            }
            return CallWindowProc(_oldWndProc, hWnd, msg, wParam, lParam);
        };

        _oldWndProc = GetWindowLongPtr(hwnd, GWLP_WNDPROC);
        SetWindowLongPtr(hwnd, GWLP_WNDPROC, Marshal.GetFunctionPointerForDelegate(_wndProcDelegate));
    }

    // ── Navigation view selection ───────────────────────────────────────

    private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItemContainer is not NavigationViewItem item)
            return;

        var tag = item.Tag as string ?? string.Empty;
        NavigateToView(tag);
    }

    /// <summary>
    /// Navigates to the specified view by creating/caching page instances
    /// and adding their root element directly as a child of ContentHost (a plain Grid).
    /// A Grid child in a * column is always measured with finite width — this fixes
    /// the infinite-width measurement bug that plagued Frame and ContentControl.
    /// </summary>
    public void NavigateToView(string tag)
    {
        // Notify the current page that we're navigating away (e.g., stop timers)
        if (_currentViewTag == "day" && _pageCache.TryGetValue("day", out Page? currentDayPage) && currentDayPage is DayViewPage dayPage)
        {
            dayPage.OnNavigatingAway();
        }

        _currentViewTag = tag;

        // Show/hide navigation controls (hide on settings page)
        UpdateNavControlsVisibility(tag);

        // Get or create the page instance
        Page page = GetOrCreatePage(tag);

        // CRITICAL: Extract the Page's root UIElement and host it directly in
        // ContentHost (a plain Grid). A Grid child in a * column receives the
        // finite column width during measure — unlike ContentControl/Frame which
        // have a ContentPresenter that passes infinite width to its child.
        if (!_rootElements.ContainsKey(tag) && page.Content is UIElement rootEl)
        {
            page.Content = null;
            _rootElements[tag] = rootEl;
        }

        // Swap the child: remove whatever is currently in ContentHost, add the new one.
        ContentHost.Children.Clear();
        if (_rootElements.TryGetValue(tag, out UIElement? element))
        {
            ContentHost.Children.Add(element);
        }
        else
        {
            // Fallback: host the page itself (shouldn't normally happen)
            ContentHost.Children.Add(page);
        }

        // Load data into the page
        LoadPageData(tag, page, _currentDate);

        UpdateHeaderTitle(FormatHeaderForView(tag, _currentDate));

        // Update responsive layout for the new view
        if (ContentAreaGrid.ActualWidth > 0)
            UpdateResponsiveLayout(ContentAreaGrid.ActualWidth);
    }

    /// <summary>
    /// Creates or retrieves a cached page instance for the given view tag.
    /// </summary>
    private Page GetOrCreatePage(string tag)
    {
        if (_pageCache.TryGetValue(tag, out Page? cached))
            return cached;

        Page page = tag switch
        {
            "month" => new MonthViewPage(),
            "week" => new WeekViewPage(),
            "day" => new DayViewPage(),
            "agenda" => new AgendaViewPage(),
            "settings" => new SettingsPage(),
            _ => new MonthViewPage()
        };

        _pageCache[tag] = page;
        return page;
    }

    /// <summary>
    /// Calls the appropriate LoadData method on the page to load/refresh its content.
    /// </summary>
    private static void LoadPageData(string tag, Page page, DateTime date)
    {
        switch (tag)
        {
            case "month" when page is MonthViewPage monthPage:
                monthPage.LoadData(date);
                break;
            case "week" when page is WeekViewPage weekPage:
                weekPage.LoadData(date);
                break;
            case "day" when page is DayViewPage dayPage:
                dayPage.LoadData(date);
                break;
            case "agenda" when page is AgendaViewPage agendaPage:
                agendaPage.LoadData();
                break;
            case "settings" when page is SettingsPage settingsPage:
                settingsPage.LoadData();
                break;
        }
    }

    // ── Header date navigation ──────────────────────────────────────────

    private void NavigateBack_Click(object sender, RoutedEventArgs e)
    {
        ShiftDate(-1);
    }

    private void NavigateForward_Click(object sender, RoutedEventArgs e)
    {
        ShiftDate(1);
    }

    private void TodayButton_Click(object sender, RoutedEventArgs e)
    {
        _currentDate = DateTime.Today;
        RefreshCurrentView();
    }

    private void ShiftDate(int direction)
    {
        _currentDate = _currentViewTag switch
        {
            "month" => _currentDate.AddMonths(direction),
            "week" => _currentDate.AddDays(7 * direction),
            "day" => _currentDate.AddDays(direction),
            "agenda" => _currentDate.AddMonths(direction),
            _ => _currentDate
        };

        RefreshCurrentView();
    }

    private void RefreshCurrentView()
    {
        NavigateToView(_currentViewTag);

        // Sync the sidebar mini-calendar to the new date
        SidebarCalendar.SetDisplayDate(new DateTimeOffset(_currentDate));

        _ = LoadCalendarListAsync();
    }

    public void RefreshCurrentViewData()
    {
        RefreshCurrentView();
    }

    public string GetEventDisplayColorHex(int calendarId, string? eventColorHex)
    {
        return CalendarDisplayHelper.ResolveEventColorHex(calendarId, eventColorHex, _calendarLookup);
    }

    public string GetCalendarDisplayName(int calendarId)
    {
        return CalendarDisplayHelper.ResolveCalendarName(calendarId, _calendarLookup);
    }

    public void ReloadCalendarMetadata()
    {
        _ = LoadCalendarListAsync();
    }

    // ── Public navigation for child pages ───────────────────────────────

    /// <summary>
    /// Navigates to the day view for a specific date. Called from MonthViewPage
    /// when a day cell is clicked.
    /// </summary>
    public void NavigateToDayView(DateTime date)
    {
        _currentDate = date;
        _currentViewTag = "day";

        // Update the NavView selection to "Day"
        foreach (var menuItem in NavView.MenuItems)
        {
            if (menuItem is NavigationViewItem navItem && navItem.Tag as string == "day")
            {
                NavView.SelectedItem = navItem;
                break;
            }
        }

        // Navigate (this will also update header)
        NavigateToView("day");
    }

    // ── New Event ───────────────────────────────────────────────────────

    private async void NewEventButton_Click(object sender, RoutedEventArgs e)
    {
        // Promote date-only navigation state into a sensible time-aware draft so
        // the toolbar action opens at a useful hour instead of 12:30 AM.
        DateTime draftStart = DateTimeHelper.GetDefaultEventStart(_currentDate, DateTime.Now);
        var result = await EventDialog.ShowCreateDialog(Content.XamlRoot, draftStart);
        if (result != null)
        {
            await App.Database.SaveEventAsync(result);
            RefreshCurrentView();
        }
    }

    // ── Sidebar calendar date pick ──────────────────────────────────────

    private void SidebarCalendar_SelectedDatesChanged(CalendarView sender, CalendarViewSelectedDatesChangedEventArgs args)
    {
        if (args.AddedDates.Count > 0)
        {
            _currentDate = args.AddedDates[0].DateTime;
            RefreshCurrentView();
        }
    }

    // ── Calendar list in pane footer ────────────────────────────────────

    private async Task LoadCalendarListAsync()
    {
        List<CalendarInfo> calendars;
        try
        {
            calendars = await App.Database.GetCalendarsAsync();
        }
        catch
        {
            // Database may not be ready yet during first launch; silently skip.
            return;
        }

        _calendarLookup = calendars.ToDictionary(c => c.Id);

        CalendarListPanel.Children.Clear();

        foreach (var cal in calendars)
        {
            var colorIndicator = new Border
            {
                Width = 12,
                Height = 12,
                CornerRadius = new CornerRadius(6),
                Background = ColorHelper.ToBrush(cal.ColorHex),
                Margin = new Thickness(0, 0, 8, 0),
                VerticalAlignment = VerticalAlignment.Center
            };

            var nameBlock = new TextBlock
            {
                Text = cal.Name,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxLines = 1
            };

            // Use a Grid instead of a horizontal StackPanel so long calendar names
            // can shrink and ellipsize inside the navigation pane.
            var row = new Grid
            {
                Margin = new Thickness(0, 2, 0, 2),
                ColumnDefinitions =
                {
                    new ColumnDefinition { Width = GridLength.Auto },
                    new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MinWidth = 0 }
                },
                ColumnSpacing = 8
            };

            Grid.SetColumn(colorIndicator, 0);
            Grid.SetColumn(nameBlock, 1);
            row.Children.Add(colorIndicator);
            row.Children.Add(nameBlock);

            var checkBox = new CheckBox
            {
                Content = row,
                IsChecked = cal.IsVisible,
                Tag = cal.Id,
                Margin = new Thickness(0, 1, 0, 1)
            };

            checkBox.Checked += CalendarToggle_Changed;
            checkBox.Unchecked += CalendarToggle_Changed;

            CalendarListPanel.Children.Add(checkBox);
        }
    }

    private async void CalendarToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (sender is not CheckBox cb || cb.Tag is not int calendarId)
            return;

        var calendars = await App.Database.GetCalendarsAsync();
        var target = calendars.Find(c => c.Id == calendarId);

        if (target is null)
            return;

        target.IsVisible = cb.IsChecked == true;
        await App.Database.SaveCalendarAsync(target);
        _calendarLookup[calendarId] = target;

        // Refresh the current view so hidden/shown calendars take effect
        RefreshCurrentView();
    }

    // ── Header title formatting ─────────────────────────────────────────

    public void UpdateHeaderTitle(string title)
    {
        HeaderTitle.Text = title;
    }

    private static string FormatHeaderForView(string viewTag, DateTime date)
    {
        return viewTag switch
        {
            "month" => date.ToString("MMMM yyyy"),
            // Align to the configured first day of week for the "Week of" label.
            "week" => $"Week of {DateTimeHelper.GetWeekStart(date, App.FirstDayOfWeek):MMM d, yyyy}",
            "day" => date.ToString("dddd, MMMM d, yyyy"),
            "agenda" => date.ToString("MMMM yyyy"),
            "settings" => "Settings",
            _ => date.ToString("MMMM yyyy")
        };
    }

    // ── Theme restoration ───────────────────────────────────────────────

    /// <summary>
    /// Applies the saved theme synchronously BEFORE pages are created.
    /// This ensures that pages built in the constructor get the correct theme brushes.
    /// </summary>
    private void ApplySavedThemeSync()
    {
        try
        {
            // Use synchronous wait since this runs before any UI is shown
            string themeValue = Task.Run(async () =>
                await App.Database.GetSettingAsync("Theme", "0")).GetAwaiter().GetResult();

            if (int.TryParse(themeValue, out int themeIndex))
            {
                var requestedTheme = themeIndex switch
                {
                    1 => ElementTheme.Light,
                    2 => ElementTheme.Dark,
                    _ => ElementTheme.Default
                };

                // Track the theme BEFORE setting it so page constructors resolve correctly
                ThemeResourceHelper.SetAppTheme(requestedTheme);

                if (Content is FrameworkElement rootElement)
                {
                    rootElement.RequestedTheme = requestedTheme;
                }
            }
        }
        catch
        {
            // Database may not be ready during first launch
        }
    }

    // ── Theme change handling ───────────────────────────────────────────

    /// <summary>
    /// Called when the root element's ActualTheme changes (e.g., user switches
    /// Light/Dark in settings, or system theme changes while set to Default).
    /// Clears all cached pages and re-navigates so that code-behind brush lookups
    /// use the correct theme dictionaries.
    /// </summary>
    private void RootElement_ActualThemeChanged(FrameworkElement sender, object args)
    {
        OnThemeChanged();
    }

    /// <summary>
    /// Clears all cached pages and root elements, reloads the calendar list,
    /// and re-navigates to the current view. This forces all code-behind
    /// resource lookups to use the updated theme.
    /// </summary>
    public void OnThemeChanged()
    {
        // Stop any running timers on cached pages before clearing
        if (_pageCache.TryGetValue("day", out Page? dayPageObj) && dayPageObj is DayViewPage cachedDayPage)
        {
            cachedDayPage.OnNavigatingAway();
        }

        // Clear all cached pages and extracted root elements
        _pageCache.Clear();
        _rootElements.Clear();

        // Re-navigate to current view (creates fresh pages with correct theme)
        NavigateToView(_currentViewTag);

        // Reload calendar list (sidebar) with correct theme
        _ = LoadCalendarListAsync();
    }

    // ── Responsive layout ───────────────────────────────────────────────

    private void UpdateNavControlsVisibility(string viewTag)
    {
        bool isSettings = viewTag == "settings";
        var vis = isSettings ? Visibility.Collapsed : Visibility.Visible;

        BackButton.Visibility = vis;
        ForwardButton.Visibility = vis;
        TodayButton.Visibility = vis;
        NewEventButton.Visibility = vis;
    }

    private void UpdateResponsiveLayout(double width)
    {
        // Don't touch nav controls when on settings (they're already hidden)
        if (_currentViewTag == "settings") return;

        bool compactHeader = width < 760;
        bool narrowHeader = width < 620;
        bool veryNarrowHeader = width < 500;

        // Collapse labels before the shell reaches clipping pressure.
        NewEventText.Visibility = compactHeader ? Visibility.Collapsed : Visibility.Visible;
        NewEventContentPanel.Spacing = compactHeader ? 0 : 8;

        // Keep the Today action reachable on narrow windows by collapsing only
        // the label instead of removing the button entirely.
        TodayButtonText.Visibility = narrowHeader ? Visibility.Collapsed : Visibility.Visible;

        // Tighten the toolbar chrome as width shrinks so the title column retains
        // enough room to stay readable instead of competing with fixed button paddings.
        HeaderBarGrid.Padding = veryNarrowHeader
            ? new Thickness(8, 8, 0, 6)
            : compactHeader
                ? new Thickness(10, 10, 0, 7)
                : new Thickness(12, 12, 0, 8);

        TodayButton.Margin = compactHeader ? new Thickness(0, 0, 4, 0) : new Thickness(0, 0, 8, 0);
        NewEventButton.Padding = compactHeader ? new Thickness(10, 8, 10, 8) : new Thickness(10, 8, 10, 8);
        NewEventButton.Width = compactHeader ? 40 : double.NaN;

        // Shrink caption button spacer at narrow widths (less wasted space)
        CaptionButtonColumn.Width = new GridLength(veryNarrowHeader ? 46 : compactHeader ? 92 : 140);

        UpdatePaneFooterLayout();
    }
}
