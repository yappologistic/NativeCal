using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
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
    private const int TotalHours = 24;
    private Canvas _eventCanvas = null!;
    private readonly DispatcherTimer _timeIndicatorTimer;

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

        // Add hour labels in column 0 and horizontal separator lines in column 1
        for (int hour = 0; hour < TotalHours; hour++)
        {
            // Hour label
            string label = DateTime.Today.AddHours(hour).ToString("h tt", CultureInfo.InvariantCulture);
            var hourLabel = new TextBlock
            {
                Text = label,
                FontSize = 12,
                Foreground = ThemeResourceHelper.GetBrush("TextFillColorSecondaryBrush", theme),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top,
                Padding = new Thickness(0, 0, 8, 0),
                Margin = new Thickness(0, -7, 0, 0) // Nudge up so the label sits on the line
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
        string colorHex = evt.ColorHex ?? ColorHelper.CalendarColors[0];
        var brush = ColorHelper.ToBrush(colorHex);

        var titleBlock = new TextBlock
        {
            Text = evt.Title,
            FontSize = 13,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Colors.White),
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
        var placements = CalculateOverlapColumns(events);

        // We need the canvas to have rendered at least once to know its width.
        // Use ActualWidth of the TimeGrid's second column as a fallback.
        double canvasWidth = _eventCanvas.ActualWidth;
        if (canvasWidth <= 0)
        {
            // Estimate: total width minus the hour gutter (60px)
            canvasWidth = TimeGrid.ActualWidth - 60;
            if (canvasWidth <= 0)
                canvasWidth = 400; // reasonable default
        }

        foreach (var placement in placements)
        {
            var evt = placement.Event;
            double top = (evt.StartTime.Hour * 60 + evt.StartTime.Minute) * (HourHeight / 60.0);
            double durationMinutes = (evt.EndTime - evt.StartTime).TotalMinutes;
            double height = Math.Max(durationMinutes * (HourHeight / 60.0), 20);

            double columnWidth = (canvasWidth - 8) / placement.TotalColumns; // 8px right margin
            double left = placement.ColumnIndex * columnWidth + 4; // 4px left margin

            var block = CreateEventBlock(evt, columnWidth - 2, height);
            Canvas.SetLeft(block, left);
            Canvas.SetTop(block, top);
            _eventCanvas.Children.Add(block);
        }

        // Make event blocks receive taps
        _eventCanvas.IsHitTestVisible = true;
    }

    private Border CreateEventBlock(CalendarEventViewModel evt, double width, double height)
    {
        string colorHex = evt.ColorHex ?? ColorHelper.CalendarColors[0];
        var color = ColorHelper.FromHex(colorHex);
        var bgBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(220, color.R, color.G, color.B));

        var titleBlock = new TextBlock
        {
            Text = evt.Title,
            FontSize = 12,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Colors.White),
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxLines = 1
        };

        var timeBlock = new TextBlock
        {
            Text = evt.TimeDisplay,
            FontSize = 11,
            Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(200, 255, 255, 255)),
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxLines = 1
        };

        var stack = new StackPanel
        {
            Spacing = 1
        };
        stack.Children.Add(titleBlock);
        if (height > 30)
        {
            stack.Children.Add(timeBlock);
        }

        var border = new Border
        {
            Background = bgBrush,
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(8, 4, 8, 4),
            Width = width,
            Height = height,
            Tag = evt,
            IsHitTestVisible = true
        };
        border.Child = stack;
        border.Tapped += EventBlock_Click;
        return border;
    }

    private struct EventPlacement
    {
        public CalendarEventViewModel Event;
        public int ColumnIndex;
        public int TotalColumns;
    }

    private static List<EventPlacement> CalculateOverlapColumns(List<CalendarEventViewModel> events)
    {
        if (events.Count == 0)
            return new List<EventPlacement>();

        var sorted = events.OrderBy(e => e.StartTime).ThenBy(e => e.EndTime).ToList();
        var placements = new List<EventPlacement>();

        // Group overlapping events into clusters
        var clusters = new List<List<CalendarEventViewModel>>();
        List<CalendarEventViewModel>? currentCluster = null;
        DateTime clusterEnd = DateTime.MinValue;

        foreach (var evt in sorted)
        {
            if (currentCluster == null || evt.StartTime >= clusterEnd)
            {
                // Start a new cluster
                currentCluster = new List<CalendarEventViewModel> { evt };
                clusters.Add(currentCluster);
                clusterEnd = evt.EndTime;
            }
            else
            {
                // Add to current cluster
                currentCluster.Add(evt);
                if (evt.EndTime > clusterEnd)
                    clusterEnd = evt.EndTime;
            }
        }

        // For each cluster, assign columns
        foreach (var cluster in clusters)
        {
            int totalColumns = 1;
            var columnEnds = new List<DateTime> { DateTime.MinValue };

            var assignments = new List<(CalendarEventViewModel Event, int Col)>();

            foreach (var evt in cluster)
            {
                int assignedCol = -1;
                for (int c = 0; c < columnEnds.Count; c++)
                {
                    if (evt.StartTime >= columnEnds[c])
                    {
                        assignedCol = c;
                        columnEnds[c] = evt.EndTime;
                        break;
                    }
                }

                if (assignedCol == -1)
                {
                    assignedCol = columnEnds.Count;
                    columnEnds.Add(evt.EndTime);
                    totalColumns = columnEnds.Count;
                }

                assignments.Add((evt, assignedCol));
            }

            foreach (var (evt, col) in assignments)
            {
                placements.Add(new EventPlacement
                {
                    Event = evt,
                    ColumnIndex = col,
                    TotalColumns = totalColumns
                });
            }
        }

        return placements;
    }

    private void ShowCurrentTimeIndicator()
    {
        // Remove any existing time indicator
        const string indicatorTag = "__CurrentTimeIndicator__";
        for (int i = TimeGrid.Children.Count - 1; i >= 0; i--)
        {
            if (TimeGrid.Children[i] is Border b && b.Tag as string == indicatorTag)
            {
                TimeGrid.Children.Remove(b);
            }
            if (TimeGrid.Children[i] is Ellipse ell && ell.Tag as string == indicatorTag)
            {
                TimeGrid.Children.Remove(ell);
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
            scrollTarget = scrollHour * HourHeight;
        }
        else
        {
            // Default to 8 AM
            scrollTarget = 8 * HourHeight;
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
            ReminderMinutes = 15
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
}
