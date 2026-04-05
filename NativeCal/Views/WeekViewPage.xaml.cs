using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Xaml.Shapes;
using NativeCal.Helpers;
using NativeCal.Models;
using NativeCal.ViewModels;
using ColorHelper = NativeCal.Helpers.ColorHelper;

namespace NativeCal.Views;

public sealed partial class WeekViewPage : Page
{
    public WeekViewModel ViewModel { get; } = new WeekViewModel();

    private const double HourHeight = 60.0;
    private const int HoursInDay = 24;
    private const int DaysInWeek = 7;

    // Day header UI elements (columns 1-7 of the header grid)
    private readonly StackPanel[] _dayHeaderPanels = new StackPanel[DaysInWeek];
    private readonly TextBlock[] _dayNameLabels = new TextBlock[DaysInWeek];
    private readonly TextBlock[] _dayNumberLabels = new TextBlock[DaysInWeek];
    private readonly Border[] _todayCircles = new Border[DaysInWeek];

    // All-day event panels (columns 1-7 of the all-day bar)
    private readonly StackPanel[] _allDayPanels = new StackPanel[DaysInWeek];

    // Canvases for timed events in each day column
    private readonly Canvas[] _dayCanvases = new Canvas[DaysInWeek];

    // Vertical column separators and current-time indicator
    private Line _nowIndicator = null!;

    private bool _isBuilt;

    public WeekViewPage()
    {
        this.InitializeComponent();
    }

    /// <summary>
    /// Called by MainWindow to load data for the specified date.
    /// Replaces OnNavigatedTo since we no longer use Frame navigation.
    /// </summary>
    public void LoadData(DateTime targetDate)
    {
        if (!_isBuilt)
        {
            BuildTimeGrid();
            BuildDayHeaders();
            BuildAllDayPanels();
            _isBuilt = true;
        }

        _ = LoadWeekAsync(targetDate);
    }

    /// <summary>
    /// Gets the current effective theme for theme-aware resource lookups.
    /// </summary>
    private ElementTheme GetCurrentTheme()
    {
        if (DayHeadersGrid is FrameworkElement fe)
            return ThemeResourceHelper.GetEffectiveTheme(fe);
        return ElementTheme.Default;
    }

    // ── Build: Day column headers ──────────────────────────────────────

    /// <summary>
    /// Creates the 7 day header columns showing abbreviated day name and date number.
    /// Today's date number is rendered inside an accent-colored circle.
    /// </summary>
    private void BuildDayHeaders()
    {
        var theme = GetCurrentTheme();

        for (int i = 0; i < DaysInWeek; i++)
        {
            int gridCol = i + 1; // columns 1-7 (0 is the time gutter)

            // Day name (e.g. "Mon")
            var dayName = new TextBlock
            {
                FontSize = 12,
                HorizontalAlignment = HorizontalAlignment.Center,
                Foreground = ThemeResourceHelper.GetBrush("TextFillColorSecondaryBrush", theme),
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxLines = 1
            };
            _dayNameLabels[i] = dayName;

            // Day number inside a today-highlight circle
            var dayNumber = new TextBlock
            {
                FontSize = 20,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxLines = 1
            };
            _dayNumberLabels[i] = dayNumber;

            var todayCircle = new Border
            {
                Width = 36,
                Height = 36,
                CornerRadius = new CornerRadius(18),
                HorizontalAlignment = HorizontalAlignment.Center,
                Child = dayNumber
            };
            _todayCircles[i] = todayCircle;

            var headerPanel = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                Spacing = 2
            };
            headerPanel.Children.Add(dayName);
            headerPanel.Children.Add(todayCircle);

            _dayHeaderPanels[i] = headerPanel;
            Grid.SetColumn(headerPanel, gridCol);
            DayHeadersGrid.Children.Add(headerPanel);
        }
    }

    // ── Build: All-day event bar ───────────────────────────────────────

    /// <summary>
    /// Creates 7 StackPanels (one per day) in the all-day bar for rendering all-day event chips.
    /// </summary>
    private void BuildAllDayPanels()
    {
        for (int i = 0; i < DaysInWeek; i++)
        {
            int gridCol = i + 1;

            var panel = new StackPanel
            {
                Spacing = 2,
                Padding = new Thickness(2, 0, 2, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            _allDayPanels[i] = panel;

            Grid.SetColumn(panel, gridCol);
            AllDayBarGrid.Children.Add(panel);
        }
    }

    // ── Build: Scrollable time grid ────────────────────────────────────

    /// <summary>
    /// Constructs the 24-hour time grid with:
    ///   - Hour labels in column 0
    ///   - Horizontal gridlines spanning all day columns
    ///   - A Canvas per day column for absolutely-positioned event blocks
    ///   - A red "now" indicator line
    /// </summary>
    private void BuildTimeGrid()
    {
        var theme = GetCurrentTheme();
        double totalHeight = HoursInDay * HourHeight;
        TimeGrid.Height = totalHeight;

        List<string> hourLabels = DateTimeHelper.GetHourLabels();

        // Hour labels + horizontal gridlines
        for (int h = 0; h < HoursInDay; h++)
        {
            double top = h * HourHeight;

            // Hour label
            var label = new TextBlock
            {
                Text = hourLabels[h],
                FontSize = 11,
                Foreground = ThemeResourceHelper.GetBrush("TextFillColorSecondaryBrush", theme),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, top - 7, 8, 0) // offset upward so label sits at the gridline
            };
            Grid.SetColumn(label, 0);
            TimeGrid.Children.Add(label);

            // Horizontal gridline spanning columns 1-7
            var gridLine = new Line
            {
                X1 = 0,
                X2 = 1,
                Stretch = Stretch.Fill,
                Stroke = ThemeResourceHelper.GetBrush("DividerStrokeColorDefaultBrush", theme),
                StrokeThickness = 0.5,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, top, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            Grid.SetColumn(gridLine, 1);
            Grid.SetColumnSpan(gridLine, DaysInWeek);
            TimeGrid.Children.Add(gridLine);
        }

        // Vertical column separators between days
        for (int i = 0; i < DaysInWeek; i++)
        {
            int gridCol = i + 1;

            // Vertical separator line at the left edge of each column
            var vLine = new Line
            {
                Y1 = 0,
                Y2 = totalHeight,
                Stroke = ThemeResourceHelper.GetBrush("DividerStrokeColorDefaultBrush", theme),
                StrokeThickness = 0.5,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top
            };
            Grid.SetColumn(vLine, gridCol);
            TimeGrid.Children.Add(vLine);

            // Canvas for timed events
            var canvas = new Canvas
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Top,
                Height = totalHeight
            };
            _dayCanvases[i] = canvas;
            Grid.SetColumn(canvas, gridCol);
            TimeGrid.Children.Add(canvas);
        }

        // Current-time red indicator line (spans all 7 day columns)
        _nowIndicator = new Line
        {
            X1 = 0,
            X2 = 1,
            Stretch = Stretch.Fill,
            Stroke = new SolidColorBrush(Microsoft.UI.Colors.Red),
            StrokeThickness = 1.5,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Top,
            Visibility = Visibility.Collapsed
        };
        Grid.SetColumn(_nowIndicator, 1);
        Grid.SetColumnSpan(_nowIndicator, DaysInWeek);
        TimeGrid.Children.Add(_nowIndicator);
    }

    // ── Data loading ───────────────────────────────────────────────────

    private async Task LoadWeekAsync(DateTime date)
    {
        await ViewModel.LoadWeekCommand.ExecuteAsync(date);
        UpdateWeekDisplay();
        ScrollToCurrentHour();
    }

    // ── UI update ──────────────────────────────────────────────────────

    /// <summary>
    /// Refreshes all visual elements: day headers, all-day bar, and timed event blocks.
    /// </summary>
    private void UpdateWeekDisplay()
    {
        ObservableCollection<WeekViewModel.DayColumn> columns = ViewModel.DayColumns;
        var theme = GetCurrentTheme();

        for (int i = 0; i < DaysInWeek; i++)
        {
            if (i >= columns.Count)
                break;

            WeekViewModel.DayColumn col = columns[i];

            // ── Day headers ──
            _dayNameLabels[i].Text = col.DayName.ToUpperInvariant();
            _dayNumberLabels[i].Text = col.DayNumber.ToString(CultureInfo.InvariantCulture);

            if (col.IsToday)
            {
                _todayCircles[i].Background = ThemeResourceHelper.GetBrush("AccentFillColorDefaultBrush", theme);
                _dayNumberLabels[i].Foreground = new SolidColorBrush(Microsoft.UI.Colors.White);
            }
            else
            {
                _todayCircles[i].Background = null;
                _dayNumberLabels[i].Foreground = ThemeResourceHelper.GetBrush("TextFillColorPrimaryBrush", theme);
            }

            // ── All-day events ──
            _allDayPanels[i].Children.Clear();
            foreach (CalendarEventViewModel allDayEvt in col.AllDayEvents)
            {
                _allDayPanels[i].Children.Add(CreateAllDayChip(allDayEvt));
            }

            // ── Timed events ──
            _dayCanvases[i].Children.Clear();
            foreach (CalendarEventViewModel evt in col.Events)
            {
                var block = CreateTimedEventBlock(evt);
                _dayCanvases[i].Children.Add(block);
            }
        }

        // ── Now indicator ──
        UpdateNowIndicator();
    }

    /// <summary>
    /// Creates a small colored chip for an all-day event in the all-day bar.
    /// </summary>
    private static Border CreateAllDayChip(CalendarEventViewModel evt)
    {
        string colorHex = evt.ColorHex ?? ColorHelper.CalendarColors[0];
        SolidColorBrush bgBrush;
        try { bgBrush = ColorHelper.ToBrush(colorHex); }
        catch { bgBrush = ColorHelper.ToBrush(ColorHelper.CalendarColors[0]); }

        var titleText = new TextBlock
        {
            Text = evt.Title,
            FontSize = 11,
            TextTrimming = TextTrimming.CharacterEllipsis,
            Foreground = new SolidColorBrush(Microsoft.UI.Colors.White),
            MaxLines = 1
        };

        var chip = new Border
        {
            CornerRadius = new CornerRadius(4),
            Background = bgBrush,
            Padding = new Thickness(6, 2, 6, 2),
            Child = titleText,
            Tag = evt
        };

        chip.PointerPressed += EventBlock_PointerPressed;
        return chip;
    }

    /// <summary>
    /// Creates a positioned event block for the time-grid Canvas.
    /// The block's Top offset and Height are derived from the event's start time and duration.
    /// </summary>
    private Border CreateTimedEventBlock(CalendarEventViewModel evt)
    {
        string colorHex = evt.ColorHex ?? ColorHelper.CalendarColors[0];
        SolidColorBrush bgBrush;
        try { bgBrush = ColorHelper.ToBrush(colorHex); }
        catch { bgBrush = ColorHelper.ToBrush(ColorHelper.CalendarColors[0]); }

        // Calculate position and size
        double topOffset = (evt.StartTime.Hour * 60 + evt.StartTime.Minute) * (HourHeight / 60.0);
        double durationMinutes = (evt.EndTime - evt.StartTime).TotalMinutes;
        double blockHeight = Math.Max(durationMinutes * (HourHeight / 60.0), 20.0);

        var titleText = new TextBlock
        {
            Text = evt.Title,
            FontSize = 12,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Microsoft.UI.Colors.White),
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxLines = 1
        };

        var timeText = new TextBlock
        {
            Text = evt.TimeDisplay,
            FontSize = 10,
            Foreground = new SolidColorBrush(Microsoft.UI.Colors.White),
            Opacity = 0.85,
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxLines = 1
        };

        var content = new StackPanel
        {
            Spacing = 1
        };
        content.Children.Add(titleText);

        // Only show time text if the block is tall enough
        if (blockHeight >= 36)
        {
            content.Children.Add(timeText);
        }

        var block = new Border
        {
            CornerRadius = new CornerRadius(4),
            Background = bgBrush,
            Padding = new Thickness(6, 4, 6, 4),
            Margin = new Thickness(2, 0, 4, 0),
            Height = blockHeight,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Tag = evt
        };

        // Stretch the block to fill the canvas column width
        block.Width = double.NaN; // auto

        // We use the SizeChanged event on the canvas to set the block width
        // For now, set a binding-like approach: use the Loaded event
        block.Child = content;

        Canvas.SetTop(block, topOffset);
        Canvas.SetLeft(block, 0);

        // We need to stretch blocks to fill the canvas. We do this by listening
        // to the canvas SizeChanged and updating block widths.
        block.Loaded += (s, e) =>
        {
            if (s is Border b && b.Parent is Canvas parentCanvas)
            {
                b.Width = Math.Max(parentCanvas.ActualWidth - 6, 20);
            }
        };

        block.PointerPressed += EventBlock_PointerPressed;
        return block;
    }

    /// <summary>
    /// Positions the red "now" line at the current time of day, visible only if
    /// the current week includes today.
    /// </summary>
    private void UpdateNowIndicator()
    {
        DateTime now = DateTime.Now;
        DateTime weekEnd = ViewModel.WeekStart.AddDays(7);

        if (now.Date >= ViewModel.WeekStart.Date && now.Date < weekEnd.Date)
        {
            double nowOffset = (now.Hour * 60 + now.Minute) * (HourHeight / 60.0);
            _nowIndicator.Margin = new Thickness(0, nowOffset, 0, 0);
            _nowIndicator.Visibility = Visibility.Visible;
        }
        else
        {
            _nowIndicator.Visibility = Visibility.Collapsed;
        }
    }

    /// <summary>
    /// Scrolls the time grid so that the current hour (or 8 AM if before 8 AM) is visible.
    /// </summary>
    private void ScrollToCurrentHour()
    {
        int targetHour = Math.Max(DateTime.Now.Hour - 1, 0);

        // Prefer showing at least 8 AM context if it's early morning
        if (targetHour < 7)
            targetHour = 7;

        double offset = targetHour * HourHeight;
        TimeScrollViewer.ChangeView(null, offset, null, disableAnimation: false);
    }

    // ── Event interaction ──────────────────────────────────────────────

    /// <summary>
    /// Shows a detail dialog when an event block or all-day chip is clicked.
    /// </summary>
    private static async void EventBlock_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        e.Handled = true;

        if (sender is not Border block || block.Tag is not CalendarEventViewModel evt)
            return;

        var dialog = new ContentDialog
        {
            Title = evt.Title,
            Content = BuildEventDetailContent(evt, block),
            CloseButtonText = "Close",
            XamlRoot = block.XamlRoot
        };

        await dialog.ShowAsync();
    }

    /// <summary>
    /// Constructs a read-only detail panel for the event popup.
    /// </summary>
    private static StackPanel BuildEventDetailContent(CalendarEventViewModel evt, FrameworkElement contextElement)
    {
        var theme = ThemeResourceHelper.GetEffectiveTheme(contextElement);
        var panel = new StackPanel { Spacing = 6 };

        // Time range
        panel.Children.Add(new TextBlock
        {
            Text = evt.TimeDisplay,
            Style = (Style)Application.Current.Resources["BodyStrongTextBlockStyle"]
        });

        // Date
        panel.Children.Add(new TextBlock
        {
            Text = evt.DateDisplay,
            Foreground = ThemeResourceHelper.GetBrush("TextFillColorSecondaryBrush", theme)
        });

        // Location
        if (!string.IsNullOrWhiteSpace(evt.Location))
        {
            var locationRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
            locationRow.Children.Add(new FontIcon
            {
                Glyph = "\uE707",
                FontSize = 14,
                Foreground = ThemeResourceHelper.GetBrush("TextFillColorSecondaryBrush", theme)
            });
            locationRow.Children.Add(new TextBlock { Text = evt.Location });
            panel.Children.Add(locationRow);
        }

        // Description
        if (!string.IsNullOrWhiteSpace(evt.Description))
        {
            panel.Children.Add(new TextBlock
            {
                Text = evt.Description,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 4, 0, 0),
                Foreground = ThemeResourceHelper.GetBrush("TextFillColorSecondaryBrush", theme)
            });
        }

        return panel;
    }
}
