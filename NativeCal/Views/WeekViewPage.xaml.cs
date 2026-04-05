using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
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
    private Canvas _spanningEventCanvas = null!;

    // Vertical column separators and current-time indicator
    private Line _nowIndicator = null!;

    private bool _isBuilt;
    private EventInteractionState? _activeInteraction;
    private bool _suppressEventTap;

    private sealed class EventInteractionState
    {
        public required CalendarEventViewModel Event { get; init; }
        public required Border EventBorder { get; init; }
        public required UIElement HandleElement { get; init; }
        public required DateTime OriginalStart { get; init; }
        public required DateTime OriginalEnd { get; init; }
        public required double OriginalHeight { get; init; }
        public Transform? OriginalTransform { get; init; }
        public required Windows.Foundation.Point StartPoint { get; init; }
        public required int OriginalBorderZIndex { get; init; }
        public required int OriginalCanvasZIndex { get; init; }
        public Canvas? ParentCanvas { get; init; }
        public bool IsResizing { get; init; }
        public bool HasMoved { get; set; }
    }

    private sealed class EventHandleTag
    {
        public required Border EventBorder { get; init; }
        public bool IsResizeHandle { get; init; }
    }

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

        _spanningEventCanvas = new Canvas
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Top,
            Height = totalHeight
        };
        Grid.SetColumn(_spanningEventCanvas, 1);
        Grid.SetColumnSpan(_spanningEventCanvas, DaysInWeek);
        TimeGrid.Children.Add(_spanningEventCanvas);

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
        var spanningTimedEvents = GetUniqueTimedEvents(columns)
            .Where(IsSpanningTimedEvent)
            .OrderBy(e => e.StartTime)
            .ThenBy(e => e.EndTime)
            .ToList();

        _spanningEventCanvas?.Children.Clear();

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

            var placements = EventLayoutHelper.CalculateOverlapPlacements(col.Events.Where(e => !IsSpanningTimedEvent(e)));
            double canvasWidth = _dayCanvases[i].ActualWidth;
            if (canvasWidth <= 0)
            {
                canvasWidth = TimeGrid.ActualWidth / (DaysInWeek + 1);
                if (canvasWidth <= 0)
                    canvasWidth = 140;
            }

            foreach (var placement in placements)
            {
                double topOffset = (placement.Event.StartTime.Hour * 60 + placement.Event.StartTime.Minute) * (HourHeight / 60.0);
                double durationMinutes = (placement.Event.EndTime - placement.Event.StartTime).TotalMinutes;
                double blockHeight = Math.Max(durationMinutes * (HourHeight / 60.0), 20.0);
                double columnWidth = Math.Max((canvasWidth - 6) / placement.TotalColumns, 24);
                double left = placement.ColumnIndex * columnWidth;

                var block = CreateTimedEventBlock(placement.Event, columnWidth - 4, blockHeight);
                Canvas.SetLeft(block, left);
                Canvas.SetTop(block, topOffset);
                _dayCanvases[i].Children.Add(block);
            }
        }

        RenderSpanningTimedEvents(spanningTimedEvents);

        // ── Now indicator ──
        UpdateNowIndicator();
    }

    /// <summary>
    /// Creates a small colored chip for an all-day event in the all-day bar.
    /// </summary>
    private Border CreateAllDayChip(CalendarEventViewModel evt)
    {
        string colorHex = MainWindow.CurrentInstance?.GetEventDisplayColorHex(evt.CalendarId, evt.ColorHex)
            ?? App.MainAppWindow?.GetEventDisplayColorHex(evt.CalendarId, evt.ColorHex)
            ?? ColorHelper.CalendarColors[0];
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

        chip.Tapped += EventBlock_Tapped;
        return chip;
    }

    private void RenderSpanningTimedEvents(IEnumerable<CalendarEventViewModel> spanningEvents)
    {
        if (_spanningEventCanvas is null)
            return;

        _spanningEventCanvas.Children.Clear();

        foreach (var evt in spanningEvents)
        {
            if (!TryGetSpanningEventBounds(evt, out double left, out double width, out double top))
                continue;

            var block = CreateSpanningTimedEventBlock(evt, width);
            Canvas.SetLeft(block, left);
            Canvas.SetTop(block, top);
            _spanningEventCanvas.Children.Add(block);
        }
    }

    private Border CreateSpanningTimedEventBlock(CalendarEventViewModel evt, double width)
    {
        string colorHex = MainWindow.CurrentInstance?.GetEventDisplayColorHex(evt.CalendarId, evt.ColorHex)
            ?? App.MainAppWindow?.GetEventDisplayColorHex(evt.CalendarId, evt.ColorHex)
            ?? ColorHelper.CalendarColors[0];
        SolidColorBrush bgBrush;
        try { bgBrush = ColorHelper.ToBrush(colorHex); }
        catch { bgBrush = ColorHelper.ToBrush(ColorHelper.CalendarColors[0]); }

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
            Text = TimedEventSpanHelper.FormatSpanTimeRange(evt.StartTime, evt.EndTime, CultureInfo.CurrentCulture),
            FontSize = 11,
            Foreground = new SolidColorBrush(Microsoft.UI.Colors.White),
            Opacity = 0.8,
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxLines = 1,
            VerticalAlignment = VerticalAlignment.Center
        };

        var content = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6
        };
        content.Children.Add(titleText);
        content.Children.Add(timeText);

        var resizeHandle = new Border
        {
            Height = 8,
            Background = new SolidColorBrush(Windows.UI.Color.FromArgb(80, 255, 255, 255)),
            CornerRadius = new CornerRadius(4),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Bottom,
            Visibility = evt.IsReadOnly ? Visibility.Collapsed : Visibility.Visible
        };

        var layout = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition { Height = new GridLength(1, GridUnitType.Star) },
                new RowDefinition { Height = GridLength.Auto }
            }
        };
        Grid.SetRow(content, 0);
        Grid.SetRow(resizeHandle, 1);
        layout.Children.Add(content);
        layout.Children.Add(resizeHandle);

        var block = new Border
        {
            CornerRadius = new CornerRadius(8),
            Background = bgBrush,
            Padding = new Thickness(10, 4, 10, 4),
            Height = 30,
            Width = Math.Max(width, 48),
            Tag = evt,
            Child = layout
        };

        if (!evt.IsReadOnly)
        {
            block.PointerPressed += EventDragHandle_PointerPressed;
            block.PointerMoved += EventDragHandle_PointerMoved;
            block.PointerReleased += EventDragHandle_PointerReleased;
            block.PointerCaptureLost += EventInteraction_PointerCaptureLost;
        }

        resizeHandle.Tag = new EventHandleTag { EventBorder = block, IsResizeHandle = true };
        resizeHandle.PointerPressed += EventResizeHandle_PointerPressed;
        resizeHandle.PointerMoved += EventResizeHandle_PointerMoved;
        resizeHandle.PointerReleased += EventResizeHandle_PointerReleased;
        resizeHandle.PointerCaptureLost += EventInteraction_PointerCaptureLost;

        block.Tapped += EventBlock_Tapped;
        return block;
    }

    /// <summary>
    /// Creates a positioned event block for the time-grid Canvas.
    /// The block's Top offset and Height are derived from the event's start time and duration.
    /// </summary>
    private Border CreateTimedEventBlock(CalendarEventViewModel evt, double width, double blockHeight)
    {
        string colorHex = MainWindow.CurrentInstance?.GetEventDisplayColorHex(evt.CalendarId, evt.ColorHex)
            ?? App.MainAppWindow?.GetEventDisplayColorHex(evt.CalendarId, evt.ColorHex)
            ?? ColorHelper.CalendarColors[0];
        SolidColorBrush bgBrush;
        try { bgBrush = ColorHelper.ToBrush(colorHex); }
        catch { bgBrush = ColorHelper.ToBrush(ColorHelper.CalendarColors[0]); }

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

        var dragHandle = new Border
        {
            Height = 8,
            Background = new SolidColorBrush(Windows.UI.Color.FromArgb(40, 255, 255, 255)),
            CornerRadius = new CornerRadius(4),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Visibility = evt.IsReadOnly ? Visibility.Collapsed : Visibility.Visible,
            IsHitTestVisible = false
        };

        var resizeHandle = new Border
        {
            Height = 10,
            Background = new SolidColorBrush(Windows.UI.Color.FromArgb(80, 255, 255, 255)),
            CornerRadius = new CornerRadius(5),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Visibility = evt.IsReadOnly || evt.IsAllDay ? Visibility.Collapsed : Visibility.Visible
        };

        var content = new StackPanel
        {
            Spacing = 1
        };
        content.Children.Add(titleText);

        if (blockHeight >= 36)
        {
            content.Children.Add(timeText);
        }

        var layout = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = new GridLength(1, GridUnitType.Star) },
                new RowDefinition { Height = GridLength.Auto }
            }
        };
        Grid.SetRow(dragHandle, 0);
        Grid.SetRow(content, 1);
        Grid.SetRow(resizeHandle, 2);
        layout.Children.Add(dragHandle);
        layout.Children.Add(content);
        layout.Children.Add(resizeHandle);

        var block = new Border
        {
            CornerRadius = new CornerRadius(4),
            Background = bgBrush,
            Padding = new Thickness(6, 4, 6, 4),
            Margin = new Thickness(2, 0, 2, 0),
            Height = blockHeight,
            Width = Math.Max(width, 20),
            HorizontalAlignment = HorizontalAlignment.Left,
            Tag = evt,
            Child = layout
        };

        if (!evt.IsReadOnly)
        {
            block.PointerPressed += EventDragHandle_PointerPressed;
            block.PointerMoved += EventDragHandle_PointerMoved;
            block.PointerReleased += EventDragHandle_PointerReleased;
            block.PointerCaptureLost += EventInteraction_PointerCaptureLost;
        }

        resizeHandle.Tag = new EventHandleTag { EventBorder = block, IsResizeHandle = true };
        resizeHandle.PointerPressed += EventResizeHandle_PointerPressed;
        resizeHandle.PointerMoved += EventResizeHandle_PointerMoved;
        resizeHandle.PointerReleased += EventResizeHandle_PointerReleased;
        resizeHandle.PointerCaptureLost += EventInteraction_PointerCaptureLost;

        block.Tapped += EventBlock_Tapped;
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

    private void EventDragHandle_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (sender is not Border border || border.Tag is not CalendarEventViewModel evt || evt.IsReadOnly)
            return;

        if (IsResizeHandleSource(e.OriginalSource as DependencyObject, border))
            return;

        var parentCanvas = FindParentCanvas(border);

        _activeInteraction = new EventInteractionState
        {
            Event = evt,
            EventBorder = border,
            HandleElement = border,
            OriginalStart = evt.StartTime,
            OriginalEnd = evt.EndTime,
            OriginalHeight = border.Height,
            OriginalTransform = border.RenderTransform,
            StartPoint = e.GetCurrentPoint(TimeGrid).Position,
            OriginalBorderZIndex = Canvas.GetZIndex(border),
            OriginalCanvasZIndex = parentCanvas is not null ? Canvas.GetZIndex(parentCanvas) : 0,
            ParentCanvas = parentCanvas,
            IsResizing = false
        };

        Canvas.SetZIndex(border, 1000);
        if (parentCanvas is not null)
            Canvas.SetZIndex(parentCanvas, 1000);

        border.CapturePointer(e.Pointer);
        e.Handled = true;
    }

    private void EventDragHandle_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (_activeInteraction is null || _activeInteraction.IsResizing)
            return;

        var currentPoint = e.GetCurrentPoint(TimeGrid).Position;
        var transform = new TranslateTransform
        {
            X = currentPoint.X - _activeInteraction.StartPoint.X,
            Y = currentPoint.Y - _activeInteraction.StartPoint.Y
        };
        _activeInteraction.EventBorder.RenderTransform = transform;
        _activeInteraction.EventBorder.Opacity = 0.85;
        _activeInteraction.HasMoved = Math.Abs(transform.X) >= 1 || Math.Abs(transform.Y) >= 1;
        e.Handled = true;
    }

    private async void EventDragHandle_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (_activeInteraction is null || _activeInteraction.IsResizing || sender is not FrameworkElement handle)
            return;

        var state = _activeInteraction;
        _activeInteraction = null;
        handle.ReleasePointerCaptures();
        await CompleteTimedInteractionAsync(state, e.GetCurrentPoint(TimeGrid).Position, false);
        e.Handled = true;
    }

    private void EventResizeHandle_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (sender is not FrameworkElement handle || handle.Tag is not EventHandleTag tag || tag.EventBorder.Tag is not CalendarEventViewModel evt || evt.IsReadOnly)
            return;

        var parentCanvas = FindParentCanvas(tag.EventBorder);

        _activeInteraction = new EventInteractionState
        {
            Event = evt,
            EventBorder = tag.EventBorder,
            HandleElement = handle,
            OriginalStart = evt.StartTime,
            OriginalEnd = evt.EndTime,
            OriginalHeight = tag.EventBorder.Height,
            OriginalTransform = tag.EventBorder.RenderTransform,
            StartPoint = e.GetCurrentPoint(TimeGrid).Position,
            OriginalBorderZIndex = Canvas.GetZIndex(tag.EventBorder),
            OriginalCanvasZIndex = parentCanvas is not null ? Canvas.GetZIndex(parentCanvas) : 0,
            ParentCanvas = parentCanvas,
            IsResizing = true
        };

        Canvas.SetZIndex(tag.EventBorder, 1000);
        if (parentCanvas is not null)
            Canvas.SetZIndex(parentCanvas, 1000);

        handle.CapturePointer(e.Pointer);
        e.Handled = true;
    }

    private void EventResizeHandle_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (_activeInteraction is null || !_activeInteraction.IsResizing)
            return;

        var currentPoint = e.GetCurrentPoint(TimeGrid).Position;
        double deltaY = currentPoint.Y - _activeInteraction.StartPoint.Y;
        _activeInteraction.EventBorder.Height = Math.Max(_activeInteraction.OriginalHeight + deltaY, HourHeight / 4);
        _activeInteraction.EventBorder.Opacity = 0.85;
        _activeInteraction.HasMoved = Math.Abs(deltaY) >= 1;
        e.Handled = true;
    }

    private async void EventResizeHandle_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (_activeInteraction is null || !_activeInteraction.IsResizing || sender is not FrameworkElement handle)
            return;

        var state = _activeInteraction;
        _activeInteraction = null;
        handle.ReleasePointerCaptures();
        await CompleteTimedInteractionAsync(state, e.GetCurrentPoint(TimeGrid).Position, true);
        e.Handled = true;
    }

    private void EventInteraction_PointerCaptureLost(object sender, PointerRoutedEventArgs e)
    {
        if (_activeInteraction is null)
            return;

        ResetInteractionVisual(_activeInteraction);
        _activeInteraction = null;
    }

    private async Task CompleteTimedInteractionAsync(EventInteractionState state, Windows.Foundation.Point pointerPosition, bool isResize)
    {
        try
        {
            if (!state.HasMoved)
                return;

            DateTime dateTime;
            bool hasDateTime = isResize
                ? TryGetResizeDateTime(pointerPosition, state, out dateTime)
                : TryGetDateTimeFromWeekPoint(pointerPosition, out dateTime);

            if (!hasDateTime)
                return;

            CalendarEvent updated = isResize
                ? CalendarEventMutationHelper.ResizeTimedEvent(state.Event.ToModel(), dateTime)
                : CalendarEventMutationHelper.MoveTimedEvent(state.Event.ToModel(), dateTime);

            _suppressEventTap = true;
            await App.Database.SaveEventAsync(updated);
            App.MainAppWindow?.RefreshCurrentViewData();
        }
        finally
        {
            ResetInteractionVisual(state);
        }
    }

    private void ResetInteractionVisual(EventInteractionState state)
    {
        state.EventBorder.Opacity = 1.0;
        state.EventBorder.Height = state.OriginalHeight;
        state.EventBorder.RenderTransform = state.OriginalTransform;
        Canvas.SetZIndex(state.EventBorder, state.OriginalBorderZIndex);
        if (state.ParentCanvas is not null)
            Canvas.SetZIndex(state.ParentCanvas, state.OriginalCanvasZIndex);
    }

    private bool TryGetDateTimeFromWeekPoint(Windows.Foundation.Point point, out DateTime result)
    {
        int dayIndex = -1;
        for (int i = 0; i < _dayCanvases.Length; i++)
        {
            if (_dayCanvases[i] is not FrameworkElement canvas || canvas.ActualWidth <= 0)
                continue;

            var origin = canvas.TransformToVisual(TimeGrid).TransformPoint(new Windows.Foundation.Point(0, 0));
            if (point.X >= origin.X && point.X <= origin.X + canvas.ActualWidth)
            {
                dayIndex = i;
                break;
            }
        }

        if (dayIndex < 0)
        {
            result = default;
            return false;
        }

        result = GetSnappedDateTime(ViewModel.WeekStart.AddDays(dayIndex).Date, point.Y);
        return true;
    }

    private static List<CalendarEventViewModel> GetUniqueTimedEvents(IEnumerable<WeekViewModel.DayColumn> columns)
    {
        return columns
            .SelectMany(c => c.Events)
            .GroupBy(e => new { e.Id, e.CalendarId, e.StartTime, e.EndTime, e.Title })
            .Select(g => g.First())
            .ToList();
    }

    private static bool IsSpanningTimedEvent(CalendarEventViewModel evt)
    {
        return !evt.IsAllDay && TimedEventSpanHelper.SpansMultipleDays(evt.StartTime, evt.EndTime);
    }

    private bool TryGetSpanningEventBounds(CalendarEventViewModel evt, out double left, out double width, out double top)
    {
        left = 0;
        width = 0;
        top = 0;

        DateTime weekStartDate = ViewModel.WeekStart.Date;
        DateTime weekEndDate = weekStartDate.AddDays(DaysInWeek - 1);
        DateTime startDate = evt.StartTime.Date < weekStartDate ? weekStartDate : evt.StartTime.Date;
        DateTime endDate = TimedEventSpanHelper.GetInclusiveEndDate(evt.StartTime, evt.EndTime);
        if (endDate > weekEndDate)
            endDate = weekEndDate;

        if (endDate < weekStartDate || startDate > weekEndDate)
            return false;

        int startIndex = (int)(startDate - weekStartDate).TotalDays;
        int endIndex = (int)(endDate - weekStartDate).TotalDays;

        double startX = GetCanvasLeft(startIndex);
        double endRight = GetCanvasLeft(endIndex) + GetCanvasWidth(endIndex);
        left = startX + 2;
        width = Math.Max(endRight - startX - 4, 48);

        DateTime visibleStart = TimedEventSpanHelper.GetVisibleSegmentStart(evt.StartTime, startDate);
        top = (visibleStart.Hour * 60 + visibleStart.Minute) * (HourHeight / 60.0);
        return true;
    }

    private double GetCanvasLeft(int dayIndex)
    {
        if (_spanningEventCanvas is not null && _dayCanvases[dayIndex].ActualWidth > 0)
        {
            return _dayCanvases[dayIndex]
                .TransformToVisual(_spanningEventCanvas)
                .TransformPoint(new Windows.Foundation.Point(0, 0)).X;
        }

        return dayIndex * GetCanvasWidth(dayIndex);
    }

    private double GetCanvasWidth(int dayIndex)
    {
        if (_dayCanvases[dayIndex].ActualWidth > 0)
            return _dayCanvases[dayIndex].ActualWidth;

        double availableWidth = Math.Max(TimeGrid.ActualWidth - 50, 0);
        return availableWidth > 0 ? availableWidth / DaysInWeek : 140;
    }

    private bool TryGetResizeDateTime(Windows.Foundation.Point point, EventInteractionState state, out DateTime result)
    {
        DateTime proposedDateTime;

        if (TimedEventSpanHelper.SpansMultipleDays(state.OriginalStart, state.OriginalEnd))
        {
            if (!TryGetDateTimeFromWeekPoint(point, out proposedDateTime))
            {
                result = default;
                return false;
            }
        }
        else
        {
            proposedDateTime = GetSnappedDateTime(state.OriginalEnd.Date, point.Y);
        }

        result = TimedEventSpanHelper.ResolveResizeTargetDateTime(state.OriginalStart, state.OriginalEnd, proposedDateTime);
        return true;
    }

    private static DateTime GetSnappedDateTime(DateTime day, double y)
    {
        double clampedY = Math.Clamp(y, 0, HoursInDay * HourHeight - (HourHeight / 4));
        double minutes = Math.Round(clampedY / (HourHeight / 60.0 * CalendarEventMutationHelper.DefaultIncrementMinutes)) * CalendarEventMutationHelper.DefaultIncrementMinutes;
        return day.Date.AddMinutes(minutes);
    }

    /// <summary>
    /// Shows a detail dialog when an event block or all-day chip is clicked.
    /// </summary>
    private async void EventBlock_Tapped(object sender, TappedRoutedEventArgs e)
    {
        e.Handled = true;

        if (_suppressEventTap)
        {
            _suppressEventTap = false;
            return;
        }

        if (sender is not Border block || block.Tag is not CalendarEventViewModel evt)
            return;

        var result = await EventDialog.ShowManageDialog(block.XamlRoot, evt.ToModel(), block);

        if (result.Action == EventDialog.EventAction.Saved && result.Event is not null)
        {
            await App.Database.SaveEventAsync(result.Event);
            App.MainAppWindow?.RefreshCurrentViewData();
        }
        else if (result.Action == EventDialog.EventAction.Deleted)
        {
            await App.Database.DeleteEventAsync(evt.Id);
            App.MainAppWindow?.RefreshCurrentViewData();
        }
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

    private static Canvas? FindParentCanvas(DependencyObject? element)
    {
        DependencyObject? current = element;
        while (current is not null)
        {
            if (current is Canvas canvas)
                return canvas;

            current = VisualTreeHelper.GetParent(current);
        }

        return null;
    }

    private static bool IsResizeHandleSource(DependencyObject? source, Border eventBorder)
    {
        DependencyObject? current = source;
        while (current is not null)
        {
            if (current is FrameworkElement element && element.Tag is EventHandleTag tag && tag.IsResizeHandle && ReferenceEquals(tag.EventBorder, eventBorder))
                return true;

            if (ReferenceEquals(current, eventBorder))
                break;

            current = VisualTreeHelper.GetParent(current);
        }

        return false;
    }
}
