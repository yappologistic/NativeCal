using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using NativeCal.Helpers;
using NativeCal.Models;
using ColorHelper = NativeCal.Helpers.ColorHelper;

namespace NativeCal.Views;

public static class EventDialog
{
    public enum EventAction
    {
        None,
        Saved,
        Deleted
    }

    public sealed class EventActionResult
    {
        public EventAction Action { get; init; }
        public CalendarEvent? Event { get; init; }
    }

    private static readonly (string Label, int Minutes)[] ReminderOptions =
    {
        ("None", 0),
        ("5 minutes", 5),
        ("10 minutes", 10),
        ("15 minutes", 15),
        ("30 minutes", 30),
        ("1 hour", 60),
        ("2 hours", 120),
        ("1 day", 1440)
    };

    private static readonly (string Label, RecurrenceType Type)[] RecurrenceOptions =
    {
        ("Does not repeat", RecurrenceType.None),
        ("Daily", RecurrenceType.Daily),
        ("Weekly", RecurrenceType.Weekly),
        ("Biweekly", RecurrenceType.Biweekly),
        ("Monthly", RecurrenceType.Monthly),
        ("Yearly", RecurrenceType.Yearly)
    };

    /// <summary>
    /// Shows dialog for creating a new event.
    /// </summary>
    public static async Task<CalendarEvent?> ShowCreateDialog(XamlRoot xamlRoot, DateTime? defaultDate = null)
    {
        var (dialog, getResult) = await BuildEventDialog(xamlRoot, existing: null, defaultDate: defaultDate);

        dialog.Title = "New Event";
        dialog.PrimaryButtonText = "Save";
        dialog.CloseButtonText = "Cancel";
        dialog.DefaultButton = ContentDialogButton.Primary;

        var result = await dialog.ShowAsync();

        if (result == ContentDialogResult.Primary)
        {
            return getResult();
        }

        return null;
    }

    /// <summary>
    /// Shows dialog for editing an existing event.
    /// </summary>
    public static async Task<CalendarEvent?> ShowEditDialog(XamlRoot xamlRoot, CalendarEvent existingEvent)
    {
        var (dialog, getResult) = await BuildEventDialog(xamlRoot, existing: existingEvent, defaultDate: null);

        dialog.Title = "Edit Event";
        dialog.PrimaryButtonText = "Save";
        dialog.CloseButtonText = "Cancel";
        dialog.DefaultButton = ContentDialogButton.Primary;

        var result = await dialog.ShowAsync();

        if (result == ContentDialogResult.Primary)
        {
            var updated = getResult();
            updated.Id = existingEvent.Id;
            updated.CreatedAt = existingEvent.CreatedAt;
            return updated;
        }

        return null;
    }

    /// <summary>
    /// Shows a details dialog for an existing event and lets the user edit or delete it.
    /// </summary>
    public static async Task<EventActionResult> ShowManageDialog(XamlRoot xamlRoot, CalendarEvent existingEvent, FrameworkElement? contextElement = null)
    {
        var dialog = new ContentDialog
        {
            Title = existingEvent.Title,
            Content = BuildEventDetailContent(existingEvent, contextElement),
            PrimaryButtonText = "Edit",
            SecondaryButtonText = "Delete",
            CloseButtonText = "Close",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = xamlRoot
        };

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            var updated = await ShowEditDialog(xamlRoot, existingEvent);
            return updated is not null
                ? new EventActionResult { Action = EventAction.Saved, Event = updated }
                : new EventActionResult { Action = EventAction.None };
        }

        if (result == ContentDialogResult.Secondary)
        {
            bool confirmed = await ShowDeleteConfirmation(xamlRoot, existingEvent);
            return confirmed
                ? new EventActionResult { Action = EventAction.Deleted }
                : new EventActionResult { Action = EventAction.None };
        }

        return new EventActionResult { Action = EventAction.None };
    }

    /// <summary>
    /// Shows confirmation dialog for deleting an event.
    /// </summary>
    public static async Task<bool> ShowDeleteConfirmation(XamlRoot xamlRoot, CalendarEvent evt)
    {
        var dialog = new ContentDialog
        {
            Title = "Delete Event",
            Content = $"Are you sure you want to delete \"{evt.Title}\"? This action cannot be undone.",
            PrimaryButtonText = "Delete",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = xamlRoot
        };

        var result = await dialog.ShowAsync();
        return result == ContentDialogResult.Primary;
    }

    private static StackPanel BuildEventDetailContent(CalendarEvent evt, FrameworkElement? contextElement)
    {
        FrameworkElement? themeElement = contextElement ?? App.MainAppWindow?.Content as FrameworkElement;
        ElementTheme theme = themeElement is not null
            ? ThemeResourceHelper.GetEffectiveTheme(themeElement)
            : ElementTheme.Default;

        var panel = new StackPanel { Spacing = 8 };

        panel.Children.Add(new TextBlock
        {
            Text = DateTimeHelper.FormatTimeRange(evt.StartTime, evt.EndTime, evt.IsAllDay),
            Style = (Style)Application.Current.Resources["BodyStrongTextBlockStyle"]
        });

        panel.Children.Add(new TextBlock
        {
            Text = evt.StartTime.ToString("dddd, MMMM d, yyyy"),
            Foreground = ThemeResourceHelper.GetBrush("TextFillColorSecondaryBrush", theme)
        });

        if (!string.IsNullOrWhiteSpace(evt.Location))
        {
            var locationRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
            locationRow.Children.Add(new FontIcon
            {
                Glyph = "\uE707",
                FontSize = 14,
                Foreground = ThemeResourceHelper.GetBrush("TextFillColorSecondaryBrush", theme)
            });
            locationRow.Children.Add(new TextBlock
            {
                Text = evt.Location,
                Foreground = ThemeResourceHelper.GetBrush("TextFillColorSecondaryBrush", theme)
            });
            panel.Children.Add(locationRow);
        }

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

    /// <summary>
    /// Builds the event dialog content with all form controls.
    /// Returns the dialog and a function that reads all field values into a CalendarEvent.
    /// </summary>
    private static async Task<(ContentDialog dialog, Func<CalendarEvent> getResult)> BuildEventDialog(
        XamlRoot xamlRoot, CalendarEvent? existing, DateTime? defaultDate)
    {
        // Load calendars from the database
        var calendars = await App.Database.GetCalendarsAsync();

        // Determine initial date/time values
        DateTime startTime;
        DateTime endTime;
        bool isAllDay = false;

        if (existing != null)
        {
            startTime = existing.StartTime;
            endTime = existing.EndTime;
            isAllDay = existing.IsAllDay;
        }
        else
        {
            startTime = defaultDate ?? DateTime.Now;
            // Round to next 30-minute interval
            int minutes = startTime.Minute;
            int roundUp = (30 - (minutes % 30)) % 30;
            if (roundUp == 0) roundUp = 30;
            startTime = startTime.AddMinutes(roundUp).AddSeconds(-startTime.Second);
            endTime = startTime.AddHours(1);
        }

        // ── Title field ─────────────────────────────────────────────────
        var titleBox = new TextBox
        {
            Header = "Title",
            PlaceholderText = "Add title",
            Text = existing?.Title ?? string.Empty,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };

        // ── All-day toggle ──────────────────────────────────────────────
        var allDayToggle = new ToggleSwitch
        {
            Header = "All day",
            OnContent = "Yes",
            OffContent = "No",
            IsOn = isAllDay
        };

        // ── Date/Time pickers for non-all-day ───────────────────────────
        var startDatePicker = new DatePicker
        {
            Header = "Start date",
            Date = new DateTimeOffset(startTime.Date),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            MinWidth = 220
        };

        var startTimePicker = new TimePicker
        {
            Header = "Start time",
            Time = startTime.TimeOfDay,
            MinuteIncrement = 5,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            MinWidth = 120
        };

        var endDatePicker = new DatePicker
        {
            Header = "End date",
            Date = new DateTimeOffset(endTime.Date),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            MinWidth = 220
        };

        var endTimePicker = new TimePicker
        {
            Header = "End time",
            Time = endTime.TimeOfDay,
            MinuteIncrement = 5,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            MinWidth = 120
        };

        // Non-all-day grid: 2 rows, 2 columns (date + time)
        var dateTimeGrid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) },
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }
            },
            RowDefinitions =
            {
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Auto }
            },
            ColumnSpacing = 12,
            RowSpacing = 8,
            MinWidth = 360
        };

        Grid.SetRow(startDatePicker, 0);
        Grid.SetColumn(startDatePicker, 0);
        Grid.SetRow(startTimePicker, 0);
        Grid.SetColumn(startTimePicker, 1);
        Grid.SetRow(endDatePicker, 1);
        Grid.SetColumn(endDatePicker, 0);
        Grid.SetRow(endTimePicker, 1);
        Grid.SetColumn(endTimePicker, 1);

        dateTimeGrid.Children.Add(startDatePicker);
        dateTimeGrid.Children.Add(startTimePicker);
        dateTimeGrid.Children.Add(endDatePicker);
        dateTimeGrid.Children.Add(endTimePicker);

        // All-day grid: stack the two date pickers vertically so the full year stays visible.
        var allDayGrid = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Auto }
            },
            RowSpacing = 8,
            MinWidth = 300
        };

        var allDayStartDatePicker = new DatePicker
        {
            Header = "Start date",
            Date = new DateTimeOffset(startTime.Date),
            HorizontalAlignment = HorizontalAlignment.Stretch
        };

        var allDayEndDatePicker = new DatePicker
        {
            Header = "End date",
            Date = new DateTimeOffset(endTime.Date),
            HorizontalAlignment = HorizontalAlignment.Stretch
        };

        Grid.SetRow(allDayStartDatePicker, 0);
        Grid.SetRow(allDayEndDatePicker, 1);

        allDayGrid.Children.Add(allDayStartDatePicker);
        allDayGrid.Children.Add(allDayEndDatePicker);

        // Set initial visibility
        dateTimeGrid.Visibility = isAllDay ? Visibility.Collapsed : Visibility.Visible;
        allDayGrid.Visibility = isAllDay ? Visibility.Visible : Visibility.Collapsed;

        // Toggle visibility on all-day switch
        allDayToggle.Toggled += (s, e) =>
        {
            bool on = allDayToggle.IsOn;
            dateTimeGrid.Visibility = on ? Visibility.Collapsed : Visibility.Visible;
            allDayGrid.Visibility = on ? Visibility.Visible : Visibility.Collapsed;

            // Sync dates between grids when toggling
            if (on)
            {
                allDayStartDatePicker.Date = startDatePicker.Date;
                allDayEndDatePicker.Date = endDatePicker.Date;
            }
            else
            {
                startDatePicker.Date = allDayStartDatePicker.Date;
                endDatePicker.Date = allDayEndDatePicker.Date;
            }
        };

        // ── Location field ──────────────────────────────────────────────
        var locationBox = new TextBox
        {
            Header = "Location",
            PlaceholderText = "Add location",
            Text = existing?.Location ?? string.Empty,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };

        // ── Description field ───────────────────────────────────────────
        var descriptionBox = new TextBox
        {
            Header = "Description",
            PlaceholderText = "Add description",
            Text = existing?.Description ?? string.Empty,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            MinHeight = 80,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };

        // ── Calendar selector ───────────────────────────────────────────
        var calendarCombo = new ComboBox
        {
            Header = "Calendar",
            HorizontalAlignment = HorizontalAlignment.Stretch
        };

        int selectedCalendarIndex = 0;
        for (int i = 0; i < calendars.Count; i++)
        {
            var cal = calendars[i];
            var item = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            var circle = new Ellipse
            {
                Width = 12,
                Height = 12,
                Fill = ColorHelper.ToBrush(cal.ColorHex)
            };
            var name = new TextBlock
            {
                Text = cal.Name,
                VerticalAlignment = VerticalAlignment.Center
            };
            item.Children.Add(circle);
            item.Children.Add(name);

            calendarCombo.Items.Add(item);

            if (existing != null && cal.Id == existing.CalendarId)
            {
                selectedCalendarIndex = i;
            }
            else if (existing == null && cal.IsDefault)
            {
                selectedCalendarIndex = i;
            }
        }

        calendarCombo.SelectedIndex = calendars.Count > 0 ? selectedCalendarIndex : -1;

        // ── Reminder selector ───────────────────────────────────────────
        var reminderCombo = new ComboBox
        {
            Header = "Reminder",
            HorizontalAlignment = HorizontalAlignment.Stretch
        };

        int selectedReminderIndex = 0;
        for (int i = 0; i < ReminderOptions.Length; i++)
        {
            reminderCombo.Items.Add(ReminderOptions[i].Label);
            if (existing != null && ReminderOptions[i].Minutes == existing.ReminderMinutes)
            {
                selectedReminderIndex = i;
            }
            else if (existing == null && ReminderOptions[i].Minutes == 15)
            {
                selectedReminderIndex = i;
            }
        }

        reminderCombo.SelectedIndex = selectedReminderIndex;

        // ── Recurrence selector ─────────────────────────────────────────
        var recurrenceCombo = new ComboBox
        {
            Header = "Repeat",
            HorizontalAlignment = HorizontalAlignment.Stretch
        };

        int selectedRecurrenceIndex = 0;
        RecurrenceType existingRecurrence = RecurrenceType.None;

        if (existing != null && !string.IsNullOrEmpty(existing.RecurrenceRule))
        {
            Enum.TryParse(existing.RecurrenceRule, out existingRecurrence);
        }

        for (int i = 0; i < RecurrenceOptions.Length; i++)
        {
            recurrenceCombo.Items.Add(RecurrenceOptions[i].Label);
            if (RecurrenceOptions[i].Type == existingRecurrence)
            {
                selectedRecurrenceIndex = i;
            }
        }

        recurrenceCombo.SelectedIndex = selectedRecurrenceIndex;

        // ── Color override ──────────────────────────────────────────────
        var colorHeader = new TextBlock
        {
            Text = "Color",
            Style = (Style)Application.Current.Resources["BodyTextBlockStyle"],
            Margin = new Thickness(0, 0, 0, 4)
        };

        var colorPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6
        };

        string[] colorChoices = ColorHelper.CalendarColors;
        // Track which color border is selected
        Border? selectedColorBorder = null;
        string? selectedColorHex = existing?.ColorHex; // null means "Auto"

        // Auto option
        var autoBorder = new Border
        {
            Width = 28,
            Height = 28,
            CornerRadius = new CornerRadius(14),
            BorderThickness = new Thickness(2),
            BorderBrush = selectedColorHex == null
                ? (Brush)Application.Current.Resources["TextFillColorPrimaryBrush"]
                : new SolidColorBrush(Microsoft.UI.Colors.Transparent),
            Background = new SolidColorBrush(Microsoft.UI.Colors.Gray),
            Child = new TextBlock
            {
                Text = "A",
                FontSize = 11,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = new SolidColorBrush(Microsoft.UI.Colors.White)
            }
        };

        if (selectedColorHex == null)
        {
            selectedColorBorder = autoBorder;
        }

        autoBorder.PointerPressed += (s, e) =>
        {
            if (selectedColorBorder != null)
            {
                selectedColorBorder.BorderBrush = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
            }
            selectedColorHex = null;
            autoBorder.BorderBrush = (Brush)Application.Current.Resources["TextFillColorPrimaryBrush"];
            selectedColorBorder = autoBorder;
        };

        colorPanel.Children.Add(autoBorder);

        foreach (var hex in colorChoices)
        {
            var swatch = new Border
            {
                Width = 28,
                Height = 28,
                CornerRadius = new CornerRadius(14),
                BorderThickness = new Thickness(2),
                BorderBrush = (selectedColorHex != null && selectedColorHex.Equals(hex, StringComparison.OrdinalIgnoreCase))
                    ? (Brush)Application.Current.Resources["TextFillColorPrimaryBrush"]
                    : new SolidColorBrush(Microsoft.UI.Colors.Transparent),
                Background = ColorHelper.ToBrush(hex),
                Tag = hex
            };

            if (selectedColorHex != null && selectedColorHex.Equals(hex, StringComparison.OrdinalIgnoreCase))
            {
                selectedColorBorder = swatch;
            }

            string capturedHex = hex;
            swatch.PointerPressed += (s, e) =>
            {
                if (selectedColorBorder != null)
                {
                    selectedColorBorder.BorderBrush = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
                }
                selectedColorHex = capturedHex;
                swatch.BorderBrush = (Brush)Application.Current.Resources["TextFillColorPrimaryBrush"];
                selectedColorBorder = swatch;
            };

            colorPanel.Children.Add(swatch);
        }

        var colorSection = new StackPanel { Spacing = 0 };
        colorSection.Children.Add(colorHeader);
        colorSection.Children.Add(colorPanel);

        // ── Assemble the content StackPanel ─────────────────────────────
        var contentPanel = new StackPanel
        {
            Spacing = 16,
            MinWidth = 440
        };

        contentPanel.Children.Add(titleBox);
        contentPanel.Children.Add(allDayToggle);
        contentPanel.Children.Add(dateTimeGrid);
        contentPanel.Children.Add(allDayGrid);
        contentPanel.Children.Add(locationBox);
        contentPanel.Children.Add(descriptionBox);
        contentPanel.Children.Add(calendarCombo);
        contentPanel.Children.Add(reminderCombo);
        contentPanel.Children.Add(recurrenceCombo);
        contentPanel.Children.Add(colorSection);

        var scrollViewer = new ScrollViewer
        {
            Content = contentPanel,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            MaxHeight = 520
        };

        // ── Build the dialog ────────────────────────────────────────────
        var dialog = new ContentDialog
        {
            Content = scrollViewer,
            XamlRoot = xamlRoot
        };

        // Validate: disable Save when title is empty or no calendar can be selected.
        dialog.PrimaryButtonText = "Save";

        void UpdatePrimaryButtonState()
        {
            bool hasTitle = !string.IsNullOrWhiteSpace(titleBox.Text);
            bool hasCalendar = calendars.Count > 0;
            dialog.IsPrimaryButtonEnabled = hasTitle && hasCalendar;
        }

        UpdatePrimaryButtonState();
        titleBox.TextChanged += (s, e) => UpdatePrimaryButtonState();

        // ── Result builder function ─────────────────────────────────────
        Func<CalendarEvent> getResult = () =>
        {
            DateTime resultStart;
            DateTime resultEnd;
            bool resultAllDay = allDayToggle.IsOn;

            if (resultAllDay)
            {
                resultStart = allDayStartDatePicker.Date.DateTime.Date;
                resultEnd = allDayEndDatePicker.Date.DateTime.Date;
                // Ensure end is at least start
                if (resultEnd < resultStart)
                {
                    resultEnd = resultStart;
                }
            }
            else
            {
                resultStart = startDatePicker.Date.DateTime.Date + startTimePicker.Time;
                resultEnd = endDatePicker.Date.DateTime.Date + endTimePicker.Time;
                // Ensure end is after start
                if (resultEnd <= resultStart)
                {
                    resultEnd = resultStart.AddHours(1);
                }
            }

            int calendarId = 0;
            if (calendarCombo.SelectedIndex >= 0 && calendarCombo.SelectedIndex < calendars.Count)
            {
                calendarId = calendars[calendarCombo.SelectedIndex].Id;
            }
            else
            {
                calendarId = calendars.FirstOrDefault(c => c.IsDefault)?.Id
                    ?? calendars.FirstOrDefault()?.Id
                    ?? 0;
            }

            int reminderMinutes = 15;
            if (reminderCombo.SelectedIndex >= 0 && reminderCombo.SelectedIndex < ReminderOptions.Length)
            {
                reminderMinutes = ReminderOptions[reminderCombo.SelectedIndex].Minutes;
            }

            RecurrenceType recurrence = RecurrenceType.None;
            if (recurrenceCombo.SelectedIndex >= 0 && recurrenceCombo.SelectedIndex < RecurrenceOptions.Length)
            {
                recurrence = RecurrenceOptions[recurrenceCombo.SelectedIndex].Type;
            }

            return new CalendarEvent
            {
                Title = titleBox.Text.Trim(),
                Description = string.IsNullOrWhiteSpace(descriptionBox.Text) ? null : descriptionBox.Text.Trim(),
                Location = string.IsNullOrWhiteSpace(locationBox.Text) ? null : locationBox.Text.Trim(),
                StartTime = resultStart,
                EndTime = resultEnd,
                IsAllDay = resultAllDay,
                CalendarId = calendarId,
                ColorHex = selectedColorHex,
                RecurrenceRule = recurrence == RecurrenceType.None ? null : recurrence.ToString(),
                ReminderMinutes = reminderMinutes,
                ModifiedAt = DateTime.UtcNow
            };
        };

        return (dialog, getResult);
    }
}
