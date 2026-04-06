using System;
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

public sealed partial class AgendaViewPage : Page
{
    public AgendaViewModel ViewModel { get; } = new AgendaViewModel();

    public AgendaViewPage()
    {
        this.InitializeComponent();
    }

    /// <summary>
    /// Gets the current effective theme for theme-aware resource lookups.
    /// </summary>
    private ElementTheme GetCurrentTheme()
    {
        if (AgendaPanel is FrameworkElement fe)
            return ThemeResourceHelper.GetEffectiveTheme(fe);
        return ElementTheme.Default;
    }

    /// <summary>
    /// Called by MainWindow to load agenda data.
    /// Replaces OnNavigatedTo since we no longer use Frame navigation.
    /// </summary>
    public async void LoadData()
    {
        LoadingRing.IsActive = true;
        EmptyState.Visibility = Visibility.Collapsed;

        await ViewModel.LoadAgendaCommand.ExecuteAsync(null);

        BuildAgendaList();

        LoadingRing.IsActive = false;
    }

    private void BuildAgendaList()
    {
        AgendaPanel.Children.Clear();

        if (ViewModel.HasNoEvents)
        {
            EmptyState.Visibility = Visibility.Visible;
            return;
        }

        EmptyState.Visibility = Visibility.Collapsed;

        foreach (var group in ViewModel.AgendaGroups)
        {
            // Date header
            var headerPanel = CreateDateHeader(group);
            AgendaPanel.Children.Add(headerPanel);

            // Event cards
            foreach (var evt in group.Events)
            {
                var card = CreateEventCard(evt);
                AgendaPanel.Children.Add(card);
            }
        }

        // "Load more" button at the bottom
        var loadMoreButton = new Button
        {
            Content = "Load more events",
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 16, 0, 16)
        };
        loadMoreButton.Click += LoadMore_Click;
        AgendaPanel.Children.Add(loadMoreButton);
    }

    private StackPanel CreateDateHeader(AgendaViewModel.AgendaGroup group)
    {
        var theme = GetCurrentTheme();
        bool isToday = DateTimeHelper.IsSameDay(group.Date, DateTime.Today);
        bool isTomorrow = DateTimeHelper.IsSameDay(group.Date, DateTime.Today.AddDays(1));

        var headerPanel = new StackPanel
        {
            Margin = new Thickness(0, 16, 0, 4),
            Padding = new Thickness(4, 0, 0, 0)
        };

        var dateText = new TextBlock
        {
            Text = group.DateHeader,
            FontSize = 16,
            FontWeight = isToday
                ? Microsoft.UI.Text.FontWeights.Bold
                : Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = isToday
                ? ThemeResourceHelper.GetBrush("AccentFillColorDefaultBrush", theme)
                : ThemeResourceHelper.GetBrush("TextFillColorPrimaryBrush", theme)
        };
        headerPanel.Children.Add(dateText);

        // Thin separator line under the header
        var separator = new Border
        {
            Height = 1,
            Margin = new Thickness(0, 4, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Background = ThemeResourceHelper.GetBrush("DividerStrokeColorDefaultBrush", theme),
            Opacity = 0.4
        };
        headerPanel.Children.Add(separator);

        return headerPanel;
    }

    private Border CreateEventCard(CalendarEventViewModel evt)
    {
        var theme = GetCurrentTheme();
        string colorHex = MainWindow.CurrentInstance?.GetEventDisplayColorHex(evt.CalendarId, evt.ColorHex)
            ?? App.MainAppWindow?.GetEventDisplayColorHex(evt.CalendarId, evt.ColorHex)
            ?? ColorHelper.CalendarColors[0];

        // Color indicator bar (left edge)
        var colorBar = new Rectangle
        {
            Width = 4,
            RadiusX = 2,
            RadiusY = 2,
            Fill = ColorHelper.ToBrush(colorHex),
            VerticalAlignment = VerticalAlignment.Stretch
        };

        // Title
        var titleBlock = new TextBlock
        {
            Text = evt.Title,
            Style = (Style)Application.Current.Resources["BodyStrongTextBlockStyle"],
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxLines = 1
        };

        // Time range
        var timeBlock = new TextBlock
        {
            Text = evt.TimeDisplay,
            Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"],
            Foreground = ThemeResourceHelper.GetBrush("TextFillColorSecondaryBrush", theme)
        };

        // Info stack (title + time + optional location)
        var infoStack = new StackPanel
        {
            Margin = new Thickness(12, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Spacing = 2
        };
        infoStack.Children.Add(titleBlock);
        infoStack.Children.Add(timeBlock);

        // Location (if present)
        if (!string.IsNullOrWhiteSpace(evt.Location))
        {
            var locationPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 4
            };
            locationPanel.Children.Add(new FontIcon
            {
                Glyph = "\uE707", // Map pin
                FontSize = 12,
                Foreground = ThemeResourceHelper.GetBrush("TextFillColorSecondaryBrush", theme),
                VerticalAlignment = VerticalAlignment.Center
            });
            locationPanel.Children.Add(new TextBlock
            {
                Text = evt.Location,
                Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"],
                Foreground = ThemeResourceHelper.GetBrush("TextFillColorSecondaryBrush", theme),
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxLines = 1
            });
            infoStack.Children.Add(locationPanel);
        }

        // Content grid: [color bar] [info] [calendar indicator]
        var contentGrid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = new GridLength(4) },
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                new ColumnDefinition { Width = GridLength.Auto }
            }
        };
        Grid.SetColumn(colorBar, 0);
        Grid.SetColumn(infoStack, 1);
        contentGrid.Children.Add(colorBar);
        contentGrid.Children.Add(infoStack);

        // Card border
        var card = new Border
        {
            Background = ThemeResourceHelper.GetBrush("CardBackgroundFillColorDefaultBrush", theme),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(12),
            Margin = new Thickness(0, 2, 0, 2),
            Child = contentGrid,
            Tag = evt
        };
        card.Tapped += EventCard_Click;

        // Pointer visual feedback
        card.PointerEntered += (s, e) =>
        {
            if (s is Border b)
            {
                b.Background = ThemeResourceHelper.GetBrush("CardBackgroundFillColorSecondaryBrush", GetCurrentTheme());
            }
        };
        card.PointerExited += (s, e) =>
        {
            if (s is Border b)
            {
                b.Background = ThemeResourceHelper.GetBrush("CardBackgroundFillColorDefaultBrush", GetCurrentTheme());
            }
        };

        return card;
    }

    private async void EventCard_Click(object sender, TappedRoutedEventArgs e)
    {
        if (sender is not FrameworkElement fe || fe.Tag is not CalendarEventViewModel evt)
            return;

        var theme = ThemeResourceHelper.GetEffectiveTheme(fe);
        var detailPanel = new StackPanel { Spacing = 8 };

        detailPanel.Children.Add(new TextBlock
        {
            Text = evt.Title,
            Style = (Style)Application.Current.Resources["SubtitleTextBlockStyle"]
        });

        detailPanel.Children.Add(new TextBlock
        {
            Text = $"{evt.DateDisplay}\n{evt.TimeDisplay}",
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
            detailPanel.Children.Add(locationRow);
        }

        if (!string.IsNullOrWhiteSpace(evt.Description))
        {
            detailPanel.Children.Add(new TextBlock
            {
                Text = evt.Description,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 4, 0, 0)
            });
        }

        var result = await EventDialog.ShowManageDialog(
            DialogXamlRootHelper.Resolve(AgendaPanel, EmptyState, LoadingRing),
            evt.ToModel(),
            this);

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

    private async void CreateEvent_Click(object sender, RoutedEventArgs e)
    {
        // Clamp the start hour so we don't overflow past midnight into the next day.
        // At 11 PM (hour 23), Hour + 1 = 24 which would roll to tomorrow.
        int nextHour = Math.Min(DateTime.Now.Hour + 1, 23);
        DateTime startTime = DateTime.Today.AddHours(nextHour);
        DateTime endTime = startTime.AddHours(1);

        var draftEvent = new CalendarEvent
        {
            StartTime = startTime,
            EndTime = endTime,
            IsAllDay = false,
            // Respect the user's Settings → Default reminder choice when seeding
            // a new agenda event draft.
            ReminderMinutes = await App.Database.GetDefaultReminderMinutesAsync()
        };

        var createdEvent = await EventDialog.ShowCreateDialog(
            DialogXamlRootHelper.Resolve(AgendaPanel, EmptyState, LoadingRing),
            draftEvent);

        if (createdEvent is not null)
        {
            await App.Database.SaveEventAsync(createdEvent);
            App.MainAppWindow?.RefreshCurrentViewData();
        }
    }

    private async void LoadMore_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn)
        {
            btn.IsEnabled = false;
            btn.Content = "Loading...";
        }

        // Preserve scroll position so the user doesn't jump back to the top
        // after new items are appended at the bottom of the list.
        double savedOffset = AgendaScrollViewer.VerticalOffset;

        await ViewModel.LoadMoreCommand.ExecuteAsync(null);
        BuildAgendaList();

        // Restore the scroll position after the list is rebuilt.
        AgendaScrollViewer.ChangeView(null, savedOffset, null, disableAnimation: true);
    }
}
