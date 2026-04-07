using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI;
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

public sealed partial class DayViewPage : Page
{
    private const double HourHeight = 60.0;
    private const double TimeGutterWidth = 64.0;
    private const double HourLabelTopOffset = 7.0;
    private const double InitialScrollPadding = 12.0;
    private const int TotalHours = 24;
    private Canvas _eventCanvas = null!;
    private readonly DispatcherTimer _timeIndicatorTimer;
    private EventInteractionState? _activeInteraction;
    private bool _suppressEventTap;

    private sealed class EventInteractionState
    {
        public required CalendarEventViewModel Event { get; init; }
        public required Border EventBorder { get; init; }
        public required UIElement HandleElement { get; init; }
        public required DateTime OriginalStart { get; init; }
        public required DateTime OriginalEnd { get; init; }
        public required double OriginalTop { get; init; }
        public required double OriginalHeight { get; init; }
        public required int OriginalZIndex { get; init; }
        public bool IsResizing { get; init; }
        public bool HasMoved { get; set; }
    }

    private sealed class EventHandleTag
    {
        public required Border EventBorder { get; init; }
        public bool IsResizeHandle { get; init; }
    }

    public DayViewModel ViewModel { get; } = new DayViewModel();

    public DayViewPage()
    {
        this.InitializeComponent();

        _timeIndicatorTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMinutes(1)
        };
        _timeIndicatorTimer.Tick += (_, _) => ShowCurrentTimeIndicator();
    }

    /// <summary>
    /// Gets the current effective theme for theme-aware resource lookups.
    /// </summary>
    private ElementTheme GetCurrentTheme()
    {
        if (TimeGrid is FrameworkElement fe)
            return ThemeResourceHelper.GetEffectiveTheme(fe);
        return ElementTheme.Default;
    }

    /// <summary>
    /// Called by MainWindow to load data for the specified date.
    /// Replaces OnNavigatedTo since we no longer use Frame navigation.
    /// </summary>
    public async void LoadData(DateTime targetDate)
    {
        // Stop any previously running timer
        _timeIndicatorTimer.Stop();

        ViewModel.CurrentDate = targetDate.Date;
        await ViewModel.LoadDayCommand.ExecuteAsync(targetDate.Date);

        BuildTimeGrid();
        UpdateDayDisplay();
        ShowCurrentTimeIndicator();
        ScrollToCurrentHour();

        _timeIndicatorTimer.Start();
    }

    /// <summary>
    /// Stops the timer when navigating away from the day view.
    /// Called by MainWindow before switching to another page.
    /// </summary>
    public void OnNavigatingAway()
    {
        _timeIndicatorTimer.Stop();
    }

    private void BuildTimeGrid()
    {
        var theme = GetCurrentTheme();

        TimeGrid.Children.Clear();
        TimeGrid.RowDefinitions.Clear();

        // Create 24 row definitions, each HourHeight tall
        for (int hour = 0; hour < TotalHours; hour++)
        {
            TimeGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(HourHeight) });
        }

        // Add hour labels in column 0 and horizontal separator lines in column 1.
        // Reuse the shared helper so the day view follows the active locale's
        // 12/24-hour preference just like the week view.
        List<string> hourLabels = DateTimeHelper.GetHourLabels();
        for (int hour = 0; hour < TotalHours; hour++)
        {
            // Hour label
            var hourLabel = new TextBlock
            {
                Text = hourLabels[hour],
                FontSize = 12,
                Foreground = ThemeResourceHelper.GetBrush("TextFillColorSecondaryBrush", theme),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top,
                Padding = new Thickness(0, 0, 8, 0),
                Margin = new Thickness(0, -HourLabelTopOffset, 0, 0) // Nudge up so the label sits on the line
            };
            Grid.SetRow(hourLabel, hour);
            Grid.SetColumn(hourLabel, 0);
            TimeGrid.Children.Add(hourLabel);

            // Horizontal line at the top of each hour row
            var line = new Border
            {
                Height = 1,
                Background = ThemeResourceHelper.GetBrush("DividerStrokeColorDefaultBrush", theme),
                VerticalAlignment = VerticalAlignment.Top,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Opacity = 0.5
            };
            Grid.SetRow(line, hour);
            Grid.SetColumn(line, 1);
            TimeGrid.Children.Add(line);

            // Clickable overlay for creating new events
            var clickTarget = new Border
            {
                Background = new SolidColorBrush(Colors.Transparent),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                Tag = hour
            };
            clickTarget.Tapped += TimeSlot_Click;
            Grid.SetRow(clickTarget, hour);
            Grid.SetColumn(clickTarget, 1);
            TimeGrid.Children.Add(clickTarget);
        }

        // Event canvas overlaid on column 1, spanning all rows
        _eventCanvas = new Canvas
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            IsHitTestVisible = false // Let taps pass through to click targets behind
        };
        Grid.SetRow(_eventCanvas, 0);
        Grid.SetRowSpan(_eventCanvas, TotalHours);
        Grid.SetColumn(_eventCanvas, 1);
        TimeGrid.Children.Add(_eventCanvas);
    }

    private void UpdateDayDisplay()
    {
        var theme = GetCurrentTheme();
        DateTime date = ViewModel.CurrentDate;
        bool isToday = DateTimeHelper.IsSameDay(date, DateTime.Today);

        // Header
        DayNumberText.Text = date.Day.ToString();
        DayNameText.Text = date.ToString("dddd", CultureInfo.CurrentCulture);
        FullDateText.Text = date.ToString("MMMM d, yyyy", CultureInfo.CurrentCulture);

        TodayCircle.Visibility = isToday ? Visibility.Visible : Visibility.Collapsed;
        DayNumberText.Foreground = isToday
            ? ThemeResourceHelper.GetBrush("TextOnAccentFillColorPrimaryBrush", theme)
            : ThemeResourceHelper.GetBrush("TextFillColorPrimaryBrush", theme);

        // All-day events
        AllDayPanel.Children.Clear();
        foreach (var evt in ViewModel.AllDayEvents)
        {
            var allDayBar = CreateAllDayEventBar(evt);
            AllDayPanel.Children.Add(allDayBar);
        }

        if (ViewModel.AllDayEvents.Count == 0)
        {
            AllDayPanel.Visibility = Visibility.Collapsed;
        }
        else
        {
            AllDayPanel.Visibility = Visibility.Visible;
        }

        // Timed events on canvas
        PlaceTimedEvents();
    }

    private Border CreateAllDayEventBar(CalendarEventViewModel evt)
    {
        string colorHex = MainWindow.CurrentInstance?.GetEventDisplayColorHex(evt.CalendarId, evt.ColorHex)
            ?? App.MainAppWindow?.GetEventDisplayColorHex(evt.CalendarId, evt.ColorHex)
            ?? ColorHelper.CalendarColors[0];
        var brush = ColorHelper.ToBrush(colorHex);
        var textBrush = ColorHelper.ToBrush(ColorContrastHelper.ResolveTextColorHex(colorHex));

        var titleBlock = new TextBlock
        {
            Text = evt.Title,
            FontSize = 13,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = textBrush,
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxLines = 1
        };

        var border = new Border
        {
            Background = brush,
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(8, 4, 8, 4),
            Margin = new Thickness(0, 1, 0, 1),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Tag = evt,
            IsHitTestVisible = true
        };
        border.Child = titleBlock;
        border.Tapped += EventBlock_Click;
        return border;
    }

    private void PlaceTimedEvents()
    {
        if (_eventCanvas == null)
            return;

        _eventCanvas.Children.Clear();

        var events = ViewModel.Events.ToList();
        if (events.Count == 0)
            return;

        // Detect overlapping events and assign columns
        var placements = EventLayoutHelper.CalculateOverlapPlacements(events);

        // We need the canvas to have rendered at least once to know its width.
        // Use ActualWidth of the TimeGrid's second column as a fallback.
        double canvasWidth = _eventCanvas.ActualWidth;
        if (canvasWidth <= 0)
        {
            // Estimate: total width minus the hour gutter.
            canvasWidth = TimeGrid.ActualWidth - TimeGutterWidth;
            if (canvasWidth <= 0)
                canvasWidth = 400; // reasonable default
        }

        DateTime dayStart = ViewModel.CurrentDate.Date;
        DateTime dayEnd = dayStart.AddDays(1);

        foreach (var placement in placements)
        {
            var evt = placement.Event;
            DateTime segmentStart = TimedEventSpanHelper.GetVisibleSegmentStart(evt.StartTime, dayStart);
            DateTime segmentEnd = TimedEventSpanHelper.GetVisibleSegmentEnd(evt.EndTime, dayEnd);
            if (segmentEnd <= segmentStart)
                continue;

            double top = (segmentStart.Hour * 60 + segmentStart.Minute) * (HourHeight / 60.0);
            double durationMinutes = (segmentEnd - segmentStart).TotalMinutes;
            double height = Math.Max(durationMinutes * (HourHeight / 60.0), 20);

            double columnWidth = (canvasWidth - 8) / placement.TotalColumns; // 8px right margin
            double left = placement.ColumnIndex * columnWidth + 4; // 4px left margin

            var block = CreateEventBlock(evt, columnWidth - 2, height, top);
            Canvas.SetLeft(block, left);
            Canvas.SetTop(block, top);
            _eventCanvas.Children.Add(block);
        }

        // Enable hit-testing on the canvas only when it contains event blocks.
        // When empty, keep it disabled so taps pass through to the time-slot
        // click targets beneath the canvas.
        _eventCanvas.IsHitTestVisible = _eventCanvas.Children.Count > 0;
    }

    private Border CreateEventBlock(CalendarEventViewModel evt, double width, double height, double top)
    {
        string colorHex = MainWindow.CurrentInstance?.GetEventDisplayColorHex(evt.CalendarId, evt.ColorHex)
            ?? App.MainAppWindow?.GetEventDisplayColorHex(evt.CalendarId, evt.ColorHex)
            ?? ColorHelper.CalendarColors[0];
        var color = ColorHelper.FromHex(colorHex);
        var bgBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(220, color.R, color.G, color.B));
        var textBrush = ColorHelper.ToBrush(ColorContrastHelper.ResolveTextColorHex(colorHex));

        bool narrowBlock = width < 84;

        var titleBlock = new TextBlock
        {
            Text = evt.Title,
            FontSize = narrowBlock ? 11 : 12,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = textBrush,
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxLines = 1
        };

        var timeBlock = new TextBlock
        {
            Text = evt.TimeDisplay,
            FontSize = narrowBlock ? 10 : 11,
            Foreground = textBrush,
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
            VerticalAlignment = VerticalAlignment.Bottom,
            Visibility = evt.IsReadOnly || evt.IsAllDay ? Visibility.Collapsed : Visibility.Visible
        };

        var stack = new StackPanel
        {
            Spacing = narrowBlock ? 0 : 1
        };
        stack.Children.Add(titleBlock);
        if (height > 30 && width >= 96)
        {
            stack.Children.Add(timeBlock);
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
        Grid.SetRow(stack, 1);
        Grid.SetRow(resizeHandle, 2);
        layout.Children.Add(dragHandle);
        layout.Children.Add(stack);
        layout.Children.Add(resizeHandle);

        var border = new Border
        {
            Background = bgBrush,
            CornerRadius = new CornerRadius(6),
            Padding = narrowBlock ? new Thickness(6, 4, 6, 4) : new Thickness(8, 4, 8, 4),
            Width = width,
            Height = height,
            Tag = evt,
            IsHitTestVisible = true,
            Child = layout
        };

        if (!evt.IsReadOnly)
        {
            border.PointerPressed += EventDragHandle_PointerPressed;
            border.PointerMoved += EventDragHandle_PointerMoved;
            border.PointerReleased += EventDragHandle_PointerReleased;
            border.PointerCaptureLost += EventInteraction_PointerCaptureLost;
        }

        resizeHandle.PointerPressed += EventResizeHandle_PointerPressed;
        resizeHandle.PointerMoved += EventResizeHandle_PointerMoved;
        resizeHandle.PointerReleased += EventResizeHandle_PointerReleased;
        resizeHandle.PointerCaptureLost += EventInteraction_PointerCaptureLost;
        resizeHandle.Tag = new EventHandleTag { EventBorder = border, IsResizeHandle = true };

        border.Tapped += EventBlock_Click;
        Canvas.SetTop(border, top);
        return border;
    }

    private void EventDragHandle_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (sender is not Border border || border.Tag is not CalendarEventViewModel evt || evt.IsReadOnly)
            return;

        if (IsResizeHandleSource(e.OriginalSource as DependencyObject, border))
            return;

        _activeInteraction = new EventInteractionState
        {
            Event = evt,
            EventBorder = border,
            HandleElement = border,
            OriginalStart = evt.StartTime,
            OriginalEnd = evt.EndTime,
            OriginalTop = Canvas.GetTop(border),
            OriginalHeight = border.Height,
            OriginalZIndex = Canvas.GetZIndex(border),
            IsResizing = false
        };

        Canvas.SetZIndex(border, 1000);
        border.CapturePointer(e.Pointer);
        e.Handled = true;
    }

    private void EventDragHandle_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (_activeInteraction is null || _activeInteraction.IsResizing)
            return;

        var point = e.GetCurrentPoint(_eventCanvas).Position;
        double snappedTop = SnapCanvasY(point.Y);
        _activeInteraction.EventBorder.Opacity = 0.85;
        Canvas.SetTop(_activeInteraction.EventBorder, snappedTop);
        _activeInteraction.HasMoved = Math.Abs(snappedTop - _activeInteraction.OriginalTop) >= 1;
        e.Handled = true;
    }

    private async void EventDragHandle_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (_activeInteraction is null || _activeInteraction.IsResizing || sender is not FrameworkElement handle)
            return;

        var state = _activeInteraction;
        _activeInteraction = null;
        handle.ReleasePointerCaptures();
        await CompleteTimedInteractionAsync(state, e.GetCurrentPoint(_eventCanvas).Position, false);
        e.Handled = true;
    }

    private void EventResizeHandle_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (sender is not FrameworkElement handle || handle.Tag is not EventHandleTag tag || tag.EventBorder.Tag is not CalendarEventViewModel evt || evt.IsReadOnly)
            return;

        _activeInteraction = new EventInteractionState
        {
            Event = evt,
            EventBorder = tag.EventBorder,
            HandleElement = handle,
            OriginalStart = evt.StartTime,
            OriginalEnd = evt.EndTime,
            OriginalTop = Canvas.GetTop(tag.EventBorder),
            OriginalHeight = tag.EventBorder.Height,
            OriginalZIndex = Canvas.GetZIndex(tag.EventBorder),
            IsResizing = true
        };

        Canvas.SetZIndex(tag.EventBorder, 1000);
        handle.CapturePointer(e.Pointer);
        e.Handled = true;
    }

    private void EventResizeHandle_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (_activeInteraction is null || !_activeInteraction.IsResizing)
            return;

        var point = e.GetCurrentPoint(_eventCanvas).Position;
        double snappedBottom = SnapCanvasY(point.Y);
        double newHeight = Math.Max(snappedBottom - _activeInteraction.OriginalTop, HourHeight / 4);
        _activeInteraction.EventBorder.Opacity = 0.85;
        _activeInteraction.EventBorder.Height = newHeight;
        _activeInteraction.HasMoved = Math.Abs(newHeight - _activeInteraction.OriginalHeight) >= 1;
        e.Handled = true;
    }

    private async void EventResizeHandle_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (_activeInteraction is null || !_activeInteraction.IsResizing || sender is not FrameworkElement handle)
            return;

        var state = _activeInteraction;
        _activeInteraction = null;
        handle.ReleasePointerCaptures();
        await CompleteTimedInteractionAsync(state, e.GetCurrentPoint(_eventCanvas).Position, true);
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

            // proposedDateTime already carries the correct date (the viewed day)
            // and the snapped time-of-day from the canvas Y position.
            DateTime proposedDateTime = GetDateTimeFromCanvasPoint(pointerPosition.Y);

            CalendarEvent updated = isResize
                // For resize in DayView, the canvas only produces times on
                // the currently viewed day. For multi-day events whose end
                // is on a later day, ResolveResizeEndForDayView detects
                // whether the user is extending (handle at bottom → map
                // time onto the original end date) or shrinking past
                // midnight (handle dragged above start → collapse to
                // same day).
                ? CalendarEventMutationHelper.ResizeTimedEvent(
                    state.Event.ToModel(),
                    TimedEventSpanHelper.ResolveResizeEndForDayView(
                        state.OriginalStart, state.OriginalEnd, proposedDateTime))
                : CalendarEventMutationHelper.MoveTimedEvent(state.Event.ToModel(), proposedDateTime);

            _suppressEventTap = true;

            // Safety: clear the flag on the next UI frame in case Tapped doesn't
            // fire (e.g. the pointer ended on a different element after dragging).
            DispatcherQueue.TryEnqueue(() => _suppressEventTap = false);

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
        Canvas.SetTop(state.EventBorder, state.OriginalTop);
        Canvas.SetZIndex(state.EventBorder, state.OriginalZIndex);
    }

    private double SnapCanvasY(double y)
    {
        double clamped = Math.Clamp(y, 0, TotalHours * HourHeight - (HourHeight / 4));
        double minuteHeight = HourHeight / 60.0;
        double snappedMinutes = Math.Round(clamped / (minuteHeight * CalendarEventMutationHelper.DefaultIncrementMinutes)) * CalendarEventMutationHelper.DefaultIncrementMinutes;
        return snappedMinutes * minuteHeight;
    }

    private DateTime GetDateTimeFromCanvasPoint(double y)
    {
        double snappedTop = SnapCanvasY(y);
        double minuteHeight = HourHeight / 60.0;
        int minutes = (int)Math.Round(snappedTop / minuteHeight);
        return ViewModel.CurrentDate.Date.AddMinutes(minutes);
    }

    private void ShowCurrentTimeIndicator()
    {
        // Remove any existing time indicator elements (red line + dot).
        // Iterate backwards so removals don't shift indices we haven't visited yet.
        const string indicatorTag = "__CurrentTimeIndicator__";
        for (int i = TimeGrid.Children.Count - 1; i >= 0; i--)
        {
            var child = TimeGrid.Children[i];
            // Check both Border (the red line) and Ellipse (the red dot)
            bool isIndicator =
                (child is Border b && b.Tag as string == indicatorTag) ||
                (child is Ellipse ell && ell.Tag as string == indicatorTag);

            if (isIndicator)
            {
                // Use RemoveAt to avoid a second collection lookup after the size changed.
                TimeGrid.Children.RemoveAt(i);
            }
        }

        if (!DateTimeHelper.IsSameDay(ViewModel.CurrentDate, DateTime.Today))
            return;

        var now = DateTime.Now;
        double totalMinutes = now.Hour * 60 + now.Minute;
        int hourRow = now.Hour;
        double minuteOffset = now.Minute * (HourHeight / 60.0);

        // Red line spanning column 1
        var line = new Border
        {
            Height = 2,
            Background = new SolidColorBrush(Colors.Red),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, minuteOffset, 0, 0),
            IsHitTestVisible = false,
            Tag = indicatorTag
        };
        Grid.SetRow(line, hourRow);
        Grid.SetColumn(line, 1);
        TimeGrid.Children.Add(line);

        // Small red circle at the left edge of the line
        var dot = new Ellipse
        {
            Width = 10,
            Height = 10,
            Fill = new SolidColorBrush(Colors.Red),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(-5, minuteOffset - 4, 0, 0),
            IsHitTestVisible = false,
            Tag = indicatorTag
        };
        Grid.SetRow(dot, hourRow);
        Grid.SetColumn(dot, 1);
        TimeGrid.Children.Add(dot);
    }

    private void ScrollToCurrentHour()
    {
        double scrollTarget;
        if (DateTimeHelper.IsSameDay(ViewModel.CurrentDate, DateTime.Today))
        {
            // Scroll so current hour is visible with some padding above
            int currentHour = DateTime.Now.Hour;
            int scrollHour = Math.Max(0, currentHour - 1);
            scrollTarget = Math.Max(scrollHour * HourHeight - (HourLabelTopOffset + InitialScrollPadding), 0);
        }
        else
        {
            // Default to 8 AM
            scrollTarget = Math.Max(8 * HourHeight - (HourLabelTopOffset + InitialScrollPadding), 0);
        }

        // Use DispatcherQueue to ensure layout has completed before scrolling
        DispatcherQueue.TryEnqueue(() =>
        {
            TimeScrollViewer.ChangeView(null, scrollTarget, null, disableAnimation: false);
        });
    }

    private async void EventBlock_Click(object sender, TappedRoutedEventArgs e)
    {
        e.Handled = true;

        if (_suppressEventTap)
        {
            _suppressEventTap = false;
            return;
        }

        if (sender is not FrameworkElement fe || fe.Tag is not CalendarEventViewModel evt)
            return;

        var result = await EventDialog.ShowManageDialog(
            DialogXamlRootHelper.Resolve(TimeGrid, AllDayPanel, TodayCircle),
            evt.ToModel(),
            fe);

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

    private static StackPanel BuildEventDetailContent(CalendarEventViewModel evt, FrameworkElement contextElement)
    {
        var theme = ThemeResourceHelper.GetEffectiveTheme(contextElement);
        var panel = new StackPanel { Spacing = 8 };

        panel.Children.Add(new TextBlock
        {
            Text = evt.Title,
            Style = (Style)Application.Current.Resources["SubtitleTextBlockStyle"]
        });

        panel.Children.Add(new TextBlock
        {
            Text = evt.TimeDisplay,
            Foreground = ThemeResourceHelper.GetBrush("TextFillColorSecondaryBrush", theme)
        });

        if (!string.IsNullOrWhiteSpace(evt.Location))
        {
            var locationPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
            locationPanel.Children.Add(new FontIcon
            {
                Glyph = "\uE707",
                FontSize = 14,
                Foreground = ThemeResourceHelper.GetBrush("TextFillColorSecondaryBrush", theme)
            });
            locationPanel.Children.Add(new TextBlock
            {
                Text = evt.Location,
                Foreground = ThemeResourceHelper.GetBrush("TextFillColorSecondaryBrush", theme)
            });
            panel.Children.Add(locationPanel);
        }

        if (!string.IsNullOrWhiteSpace(evt.Description))
        {
            panel.Children.Add(new TextBlock
            {
                Text = evt.Description,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 4, 0, 0)
            });
        }

        return panel;
    }

    private async void TimeSlot_Click(object sender, TappedRoutedEventArgs e)
    {
        if (sender is not FrameworkElement fe || fe.Tag is not int hour)
            return;

        // Determine minute from tap position within the hour cell
        var position = e.GetPosition(fe);
        int minute = (int)(position.Y / HourHeight * 60);
        minute = Math.Clamp(minute, 0, 59);

        // Round to nearest 15 minutes
        minute = (minute / 15) * 15;

        DateTime startTime = ViewModel.CurrentDate.Date.AddHours(hour).AddMinutes(minute);
        DateTime endTime = startTime.AddHours(1);

        var draftEvent = new CalendarEvent
        {
            StartTime = startTime,
            EndTime = endTime,
            IsAllDay = false,
            // Respect the user's Settings → Default reminder choice when seeding
            // a new event from an empty time slot.
            ReminderMinutes = await App.Database.GetDefaultReminderMinutesAsync()
        };

        var createdEvent = await EventDialog.ShowCreateDialog(
            DialogXamlRootHelper.Resolve(TimeGrid, AllDayPanel, TodayCircle),
            draftEvent);

        if (createdEvent is not null)
        {
            await App.Database.SaveEventAsync(createdEvent);
            App.MainAppWindow?.RefreshCurrentViewData();
        }
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
