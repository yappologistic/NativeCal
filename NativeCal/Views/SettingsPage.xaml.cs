using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Xaml.Shapes;
using NativeCal.Helpers;
using NativeCal.Models;
using ColorHelper = NativeCal.Helpers.ColorHelper;

namespace NativeCal.Views;

public sealed partial class SettingsPage : Page
{
    private const string ThemeSettingKey = "Theme";
    private const string DefaultReminderKey = "DefaultReminderMinutes";
    private const string FirstDayOfWeekKey = "FirstDayOfWeek";

    private bool _isLoading;

    private static readonly (string Label, int Minutes)[] ReminderOptions =
    {
        ("None", 0),
        ("5 minutes", 5),
        ("10 minutes", 10),
        ("15 minutes", 15),
        ("30 minutes", 30),
        ("1 hour", 60)
    };

    private static readonly (string Label, int Value)[] FirstDayOptions =
    {
        ("Sunday", 0),
        ("Monday", 1),
        ("Saturday", 6)
    };

    public SettingsPage()
    {
        this.InitializeComponent();
    }

    /// <summary>
    /// Called by MainWindow to load settings data.
    /// Replaces OnNavigatedTo since we no longer use Frame navigation.
    /// </summary>
    public void LoadData()
    {
        _ = LoadSettingsAsync();
        _ = LoadCalendarListAsync();
    }

    // ── Load settings from database ─────────────────────────────────────

    private async Task LoadSettingsAsync()
    {
        _isLoading = true;

        try
        {
            // Theme
            string themeValue = await App.Database.GetSettingAsync(ThemeSettingKey, "0");
            if (int.TryParse(themeValue, out int themeIndex))
            {
                ThemeComboBox.SelectedIndex = Math.Clamp(themeIndex, 0, 2);
            }
            else
            {
                ThemeComboBox.SelectedIndex = 0;
            }

            // First day of week
            string firstDayValue = await App.Database.GetSettingAsync(FirstDayOfWeekKey, "0");
            if (int.TryParse(firstDayValue, out int firstDayStored))
            {
                int matchIndex = 0;
                for (int i = 0; i < FirstDayOptions.Length; i++)
                {
                    if (FirstDayOptions[i].Value == firstDayStored)
                    {
                        matchIndex = i;
                        break;
                    }
                }
                FirstDayComboBox.SelectedIndex = matchIndex;
            }
            else
            {
                FirstDayComboBox.SelectedIndex = 0;
            }

            // Default reminder
            string reminderValue = await App.Database.GetSettingAsync(DefaultReminderKey, "15");
            if (int.TryParse(reminderValue, out int reminderMinutes))
            {
                int matchIndex = 3; // default to 15 minutes
                for (int i = 0; i < ReminderOptions.Length; i++)
                {
                    if (ReminderOptions[i].Minutes == reminderMinutes)
                    {
                        matchIndex = i;
                        break;
                    }
                }
                ReminderComboBox.SelectedIndex = matchIndex;
            }
            else
            {
                ReminderComboBox.SelectedIndex = 3;
            }

            // Version
            var version = Assembly.GetExecutingAssembly().GetName().Version;
            VersionText.Text = version is not null
                ? $"Version {version.Major}.{version.Minor}.{version.Build}"
                : "Version 1.0.0";
        }
        finally
        {
            _isLoading = false;
        }
    }

    // ── Theme change ────────────────────────────────────────────────────

    private async void ThemeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isLoading) return;

        int index = ThemeComboBox.SelectedIndex;
        await App.Database.SetSettingAsync(ThemeSettingKey, index.ToString());

        ApplyTheme(index);
    }

    private static void ApplyTheme(int themeIndex)
    {
        var requestedTheme = themeIndex switch
        {
            1 => ElementTheme.Light,
            2 => ElementTheme.Dark,
            _ => ElementTheme.Default
        };

        // Track the theme so all code-behind lookups resolve correctly
        ThemeResourceHelper.SetAppTheme(requestedTheme);

        if (App.MainAppWindow?.Content is FrameworkElement rootElement)
        {
            rootElement.RequestedTheme = requestedTheme;
        }
    }

    // ── First day of week change ────────────────────────────────────────

    private async void FirstDayComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isLoading) return;

        int selectedIndex = FirstDayComboBox.SelectedIndex;
        if (selectedIndex >= 0 && selectedIndex < FirstDayOptions.Length)
        {
            int dayValue = FirstDayOptions[selectedIndex].Value;
            await App.Database.SetSettingAsync(FirstDayOfWeekKey, dayValue.ToString());
        }
    }

    // ── Default reminder change ─────────────────────────────────────────

    private async void ReminderComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isLoading) return;

        int selectedIndex = ReminderComboBox.SelectedIndex;
        if (selectedIndex >= 0 && selectedIndex < ReminderOptions.Length)
        {
            int minutes = ReminderOptions[selectedIndex].Minutes;
            await App.Database.SetSettingAsync(DefaultReminderKey, minutes.ToString());
        }
    }

    // ── Calendar list ───────────────────────────────────────────────────

    private async Task LoadCalendarListAsync()
    {
        List<CalendarInfo> calendars;
        try
        {
            calendars = await App.Database.GetCalendarsAsync();
        }
        catch
        {
            return;
        }

        CalendarSettingsPanel.Children.Clear();

        foreach (var cal in calendars)
        {
            CalendarSettingsPanel.Children.Add(BuildCalendarCard(cal));
        }
    }

    private ElementTheme GetCurrentTheme()
    {
        if (App.MainAppWindow?.Content is FrameworkElement root)
            return ThemeResourceHelper.GetEffectiveTheme(root);
        return ElementTheme.Default;
    }

    private Border BuildCalendarCard(CalendarInfo cal)
    {
        var theme = GetCurrentTheme();

        // Color indicator circle
        var colorCircle = new Ellipse
        {
            Width = 16,
            Height = 16,
            Fill = ColorHelper.ToBrush(cal.ColorHex),
            VerticalAlignment = VerticalAlignment.Center
        };

        // Calendar name
        var nameBlock = new TextBlock
        {
            Text = cal.Name,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxLines = 1
        };
        if (ThemeResourceHelper.GetResource("BodyStrongTextBlockStyle", theme) is Style bodyStrong)
            nameBlock.Style = bodyStrong;

        // Default badge
        var defaultBadge = new Border
        {
            Background = ThemeResourceHelper.GetBrush("AccentFillColorDefaultBrush", theme),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(6, 2, 6, 2),
            VerticalAlignment = VerticalAlignment.Center,
            Visibility = cal.IsDefault ? Visibility.Visible : Visibility.Collapsed,
            Child = new TextBlock
            {
                Text = "Default",
                FontSize = 11,
                Foreground = new SolidColorBrush(Microsoft.UI.Colors.White)
            }
        };

        // Left section: circle + name + badge in a Grid for proper overflow
        var leftGrid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MinWidth = 0 },
                new ColumnDefinition { Width = GridLength.Auto }
            },
            ColumnSpacing = 10,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(colorCircle, 0);
        Grid.SetColumn(nameBlock, 1);
        Grid.SetColumn(defaultBadge, 2);
        leftGrid.Children.Add(colorCircle);
        leftGrid.Children.Add(nameBlock);
        leftGrid.Children.Add(defaultBadge);

        // Edit button
        var editButton = new Button
        {
            Content = new FontIcon
            {
                Glyph = "\uE70F",
                FontSize = 14
            },
            Tag = cal.Id,
            Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
            BorderThickness = new Thickness(0),
            Padding = new Thickness(8),
            VerticalAlignment = VerticalAlignment.Center
        };
        ToolTipService.SetToolTip(editButton, "Edit calendar");
        editButton.Click += EditCalendar_Click;

        // Delete button (disabled if default)
        var deleteButton = new Button
        {
            Content = new FontIcon
            {
                Glyph = "\uE74D",
                FontSize = 14
            },
            Tag = cal.Id,
            Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
            BorderThickness = new Thickness(0),
            Padding = new Thickness(8),
            IsEnabled = !cal.IsDefault,
            VerticalAlignment = VerticalAlignment.Center
        };
        ToolTipService.SetToolTip(deleteButton, cal.IsDefault ? "Cannot delete default calendar" : "Delete calendar");
        deleteButton.Click += DeleteCalendar_Click;

        // Right section: edit + delete buttons
        var rightPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 4,
            VerticalAlignment = VerticalAlignment.Center
        };
        rightPanel.Children.Add(editButton);
        rightPanel.Children.Add(deleteButton);

        // Layout grid
        var grid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MinWidth = 0 },
                new ColumnDefinition { Width = GridLength.Auto }
            },
            ColumnSpacing = 8
        };

        Grid.SetColumn(leftGrid, 0);
        Grid.SetColumn(rightPanel, 1);
        grid.Children.Add(leftGrid);
        grid.Children.Add(rightPanel);

        // Card border
        var card = new Border
        {
            Background = ThemeResourceHelper.GetBrush("CardBackgroundFillColorDefaultBrush", theme),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(16),
            Margin = new Thickness(0, 2, 0, 0),
            Child = grid,
            Tag = cal
        };

        return card;
    }

    // ── Add calendar ────────────────────────────────────────────────────

    private async void AddCalendar_Click(object sender, RoutedEventArgs e)
    {
        var (dialog, getName, getColor) = BuildCalendarDialog(
            title: "Add Calendar",
            initialName: "",
            initialColor: ColorHelper.CalendarColors[0]);

        var result = await dialog.ShowAsync();

        if (result == ContentDialogResult.Primary)
        {
            string name = getName();
            string color = getColor();

            if (string.IsNullOrWhiteSpace(name)) return;

            var newCal = new CalendarInfo
            {
                Name = name.Trim(),
                ColorHex = color,
                IsVisible = true,
                IsDefault = false,
                CreatedAt = DateTime.UtcNow
            };

            await App.Database.SaveCalendarAsync(newCal);
            await LoadCalendarListAsync();
        }
    }

    // ── Edit calendar ───────────────────────────────────────────────────

    private async void EditCalendar_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not int calendarId) return;

        var calendars = await App.Database.GetCalendarsAsync();
        var cal = calendars.FirstOrDefault(c => c.Id == calendarId);
        if (cal == null) return;

        var (dialog, getName, getColor) = BuildCalendarDialog(
            title: "Edit Calendar",
            initialName: cal.Name,
            initialColor: cal.ColorHex);

        var result = await dialog.ShowAsync();

        if (result == ContentDialogResult.Primary)
        {
            string name = getName();
            string color = getColor();

            if (string.IsNullOrWhiteSpace(name)) return;

            cal.Name = name.Trim();
            cal.ColorHex = color;

            await App.Database.SaveCalendarAsync(cal);
            await LoadCalendarListAsync();
        }
    }

    // ── Delete calendar ─────────────────────────────────────────────────

    private async void DeleteCalendar_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not int calendarId) return;

        var calendars = await App.Database.GetCalendarsAsync();
        var cal = calendars.FirstOrDefault(c => c.Id == calendarId);
        if (cal == null || cal.IsDefault) return;

        // Count events in this calendar for confirmation message
        var events = await App.Database.GetEventsByCalendarAsync(calendarId);

        string message = events.Count > 0
            ? $"Are you sure you want to delete \"{cal.Name}\"? This will also delete {events.Count} event{(events.Count == 1 ? "" : "s")} in this calendar."
            : $"Are you sure you want to delete \"{cal.Name}\"?";

        var confirmDialog = new ContentDialog
        {
            Title = "Delete Calendar",
            Content = message,
            PrimaryButtonText = "Delete",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = DialogXamlRootHelper.Resolve(ThemeComboBox, FirstDayComboBox, ReminderComboBox, CalendarSettingsPanel)
        };

        var result = await confirmDialog.ShowAsync();

        if (result == ContentDialogResult.Primary)
        {
            await App.Database.DeleteCalendarAsync(calendarId);

            // Ensure at least one calendar remains as default
            var remaining = await App.Database.GetCalendarsAsync();
            if (remaining.Count > 0 && !remaining.Any(c => c.IsDefault))
            {
                remaining[0].IsDefault = true;
                await App.Database.SaveCalendarAsync(remaining[0]);
            }

            await LoadCalendarListAsync();
        }
    }

    // ── Calendar dialog builder ─────────────────────────────────────────

    private (ContentDialog dialog, Func<string> getName, Func<string> getColor) BuildCalendarDialog(
        string title, string initialName, string initialColor)
    {
        var theme = GetCurrentTheme();

        var nameBox = new TextBox
        {
            Header = "Name",
            PlaceholderText = "Calendar name",
            Text = initialName,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };

        // Color swatches
        var colorHeader = new TextBlock
        {
            Text = "Color",
            Margin = new Thickness(0, 8, 0, 4)
        };

        var colorPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6
        };

        string selectedColor = initialColor;
        Border? selectedBorder = null;

        foreach (var hex in ColorHelper.CalendarColors)
        {
            var swatch = new Border
            {
                Width = 32,
                Height = 32,
                CornerRadius = new CornerRadius(16),
                Background = ColorHelper.ToBrush(hex),
                BorderThickness = new Thickness(2),
                BorderBrush = hex.Equals(initialColor, StringComparison.OrdinalIgnoreCase)
                    ? ThemeResourceHelper.GetBrush("TextFillColorPrimaryBrush", theme)
                    : new SolidColorBrush(Microsoft.UI.Colors.Transparent),
                Tag = hex
            };

            if (hex.Equals(initialColor, StringComparison.OrdinalIgnoreCase))
            {
                selectedBorder = swatch;
            }

            string capturedHex = hex;
            swatch.PointerPressed += (s, ev) =>
            {
                // Re-resolve theme at click time (could have changed)
                var currentTheme = GetCurrentTheme();
                if (selectedBorder != null)
                {
                    selectedBorder.BorderBrush = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
                }
                selectedColor = capturedHex;
                swatch.BorderBrush = ThemeResourceHelper.GetBrush("TextFillColorPrimaryBrush", currentTheme);
                selectedBorder = swatch;
            };

            colorPanel.Children.Add(swatch);
        }

        var contentPanel = new StackPanel
        {
            Spacing = 8,
            MinWidth = 320
        };
        contentPanel.Children.Add(nameBox);
        contentPanel.Children.Add(colorHeader);
        contentPanel.Children.Add(colorPanel);

        var dialog = new ContentDialog
        {
            Title = title,
            Content = contentPanel,
            PrimaryButtonText = "Save",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = DialogXamlRootHelper.Resolve(ThemeComboBox, FirstDayComboBox, ReminderComboBox, CalendarSettingsPanel)
        };

        // Validate: disable Save when name is empty
        dialog.IsPrimaryButtonEnabled = !string.IsNullOrWhiteSpace(nameBox.Text);
        nameBox.TextChanged += (s, ev) =>
        {
            dialog.IsPrimaryButtonEnabled = !string.IsNullOrWhiteSpace(nameBox.Text);
        };

        return (dialog, () => nameBox.Text, () => selectedColor);
    }
}
