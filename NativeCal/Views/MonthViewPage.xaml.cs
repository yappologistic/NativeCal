using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using NativeCal.Helpers;
using NativeCal.Models;
using NativeCal.ViewModels;
using ColorHelper = NativeCal.Helpers.ColorHelper;

namespace NativeCal.Views;

public sealed partial class MonthViewPage : Page
{
    public MonthViewModel ViewModel { get; } = new MonthViewModel();

    private const int TotalCells = 42;
    private const int MaxVisibleEvents = 3;

    // Cached UI references for each of the 42 day cells
    private readonly Border[] _cellBorders = new Border[TotalCells];
    private readonly TextBlock[] _dayNumbers = new TextBlock[TotalCells];
    private readonly Border[] _todayCircles = new Border[TotalCells];
    private readonly StackPanel[] _eventPanels = new StackPanel[TotalCells];
    private readonly TextBlock[] _moreLabels = new TextBlock[TotalCells];

    // Track which date each cell represents so click handlers work
    private readonly DateTime[] _cellDates = new DateTime[TotalCells];

    public MonthViewPage()
    {
        this.InitializeComponent();
        CreateCalendarCells();
        Loaded += MonthViewPage_Loaded;
    }

    private void MonthViewPage_Loaded(object sender, RoutedEventArgs e)
    {
        ConstrainCalendarWidth();
    }

    /// <summary>
    /// Called by MainWindow to load data for the specified date.
    /// Replaces OnNavigatedTo since we no longer use Frame navigation.
    /// </summary>
    public void LoadData(DateTime targetDate)
    {
        _ = LoadMonthAsync(targetDate);
    }

    private void LayoutRoot_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        ConstrainCalendarWidth();
    }

    private void ConstrainCalendarWidth()
    {
        double availableWidth = Math.Max(
            LayoutRoot.ActualWidth - LayoutRoot.Padding.Left - LayoutRoot.Padding.Right,
            0);

        if (availableWidth <= 0)
        {
            return;
        }

        DayHeadersGrid.Width = availableWidth;
        DayHeadersGrid.MaxWidth = availableWidth;
        CalendarGrid.Width = availableWidth;
        CalendarGrid.MaxWidth = availableWidth;

        int colCount = CalendarGrid.ColumnDefinitions.Count;
        double totalSpacing = CalendarGrid.ColumnSpacing * (colCount - 1);
        // Floor cell widths to prevent sub-pixel overflow at the right edge
        double cellWidth = Math.Floor(Math.Max((availableWidth - totalSpacing) / colCount, 0));
        double contentWidth = Math.Max(cellWidth - 4, 0);

        foreach (ColumnDefinition column in DayHeadersGrid.ColumnDefinitions)
        {
            column.Width = new GridLength(cellWidth);
        }

        foreach (ColumnDefinition column in CalendarGrid.ColumnDefinitions)
        {
            column.Width = new GridLength(cellWidth);
        }

        for (int i = 0; i < TotalCells; i++)
        {
            _cellBorders[i].Width = cellWidth;
            _cellBorders[i].MaxWidth = cellWidth;
            _eventPanels[i].Width = contentWidth;
            _eventPanels[i].MaxWidth = contentWidth;
            _moreLabels[i].Width = contentWidth;
            _moreLabels[i].MaxWidth = contentWidth;
        }
    }

    /// <summary>
    /// Gets the current effective theme for theme-aware resource lookups.
    /// Uses the RootGrid (the root FrameworkElement) to determine the actual theme.
    /// </summary>
    private ElementTheme GetCurrentTheme()
    {
        // The page content is extracted and placed inside MainWindow's ContentHost,
        // so we use the LayoutRoot (which IS in the visual tree) to detect the theme.
        if (LayoutRoot is FrameworkElement fe)
            return ThemeResourceHelper.GetEffectiveTheme(fe);
        return ElementTheme.Default;
    }

    /// <summary>
    /// Creates the 42 day cell UI elements and places them in the CalendarGrid.
    /// Each cell is a Border containing a vertical StackPanel with:
    ///   - A header area with the day number (with today highlight circle)
    ///   - A StackPanel for event chips
    ///   - A "+N more" label
    /// </summary>
    private void CreateCalendarCells()
    {
        var theme = GetCurrentTheme();

        for (int i = 0; i < TotalCells; i++)
        {
            int row = i / 7;
            int col = i % 7;

            // Day number text
            var dayNumberText = new TextBlock
            {
                FontSize = 13,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0)
            };
            _dayNumbers[i] = dayNumberText;

            // Today highlight circle (hidden by default)
            var todayCircle = new Border
            {
                Width = 28,
                Height = 28,
                CornerRadius = new CornerRadius(14),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 4, 0, 2),
                Visibility = Visibility.Collapsed,
                Child = dayNumberText
            };
            _todayCircles[i] = todayCircle;

            // Event chips panel
            var eventPanel = new StackPanel
            {
                Spacing = 1,
                Padding = new Thickness(2, 0, 2, 0),
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            _eventPanels[i] = eventPanel;

            // "+N more" label
            var moreLabel = new TextBlock
            {
                FontSize = 10,
                Foreground = ThemeResourceHelper.GetBrush("TextFillColorSecondaryBrush", theme),
                Margin = new Thickness(4, 0, 0, 0),
                Visibility = Visibility.Collapsed
            };
            _moreLabels[i] = moreLabel;

            // Cell container
            var cellContent = new StackPanel
            {
                Spacing = 0,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            cellContent.Children.Add(todayCircle);
            cellContent.Children.Add(eventPanel);
            cellContent.Children.Add(moreLabel);

            var cellBorder = new Border
            {
                CornerRadius = new CornerRadius(4),
                Background = ThemeResourceHelper.GetBrush("CardBackgroundFillColorDefaultBrush", theme),
                BorderBrush = ThemeResourceHelper.GetBrush("CardStrokeColorDefaultBrush", theme),
                BorderThickness = new Thickness(0.5),
                Padding = new Thickness(0),
                Child = cellContent,
                Tag = i // store index so we can retrieve it on click
            };
            cellBorder.PointerPressed += CellBorder_PointerPressed;
            _cellBorders[i] = cellBorder;

            Grid.SetRow(cellBorder, row);
            Grid.SetColumn(cellBorder, col);
            CalendarGrid.Children.Add(cellBorder);
        }
    }

    /// <summary>
    /// Loads the month data via the ViewModel and refreshes all 42 cells.
    /// </summary>
    private async Task LoadMonthAsync(DateTime date)
    {
        await ViewModel.LoadMonthCommand.ExecuteAsync(date);
        UpdateCells();
    }

    /// <summary>
    /// Synchronizes all 42 cell UI elements with the ViewModel.DayCells collection.
    /// Updates day numbers, today highlights, current-month dimming, event chips,
    /// AND cell backgrounds/borders (for correct theme rendering).
    /// </summary>
    private void UpdateCells()
    {
        ObservableCollection<MonthViewModel.DayCell> cells = ViewModel.DayCells;
        var theme = GetCurrentTheme();

        for (int i = 0; i < TotalCells; i++)
        {
            if (i >= cells.Count)
            {
                _cellBorders[i].Visibility = Visibility.Collapsed;
                continue;
            }

            _cellBorders[i].Visibility = Visibility.Visible;
            MonthViewModel.DayCell cell = cells[i];
            _cellDates[i] = cell.Date;

            // -- Cell background and border (refresh on every update for theme correctness) --
            _cellBorders[i].Background = ThemeResourceHelper.GetBrush("CardBackgroundFillColorDefaultBrush", theme);
            _cellBorders[i].BorderBrush = ThemeResourceHelper.GetBrush("CardStrokeColorDefaultBrush", theme);

            // -- Day number --
            _dayNumbers[i].Text = cell.DayNumber.ToString(CultureInfo.InvariantCulture);

            // -- Today highlight --
            if (cell.IsToday)
            {
                _todayCircles[i].Background = ThemeResourceHelper.GetBrush("AccentFillColorDefaultBrush", theme);
                _dayNumbers[i].Foreground = new SolidColorBrush(Microsoft.UI.Colors.White);
                _todayCircles[i].Visibility = Visibility.Visible;
            }
            else
            {
                _todayCircles[i].Background = null;
                _todayCircles[i].Visibility = Visibility.Visible; // still show, just no colored background

                if (cell.IsCurrentMonth)
                {
                    _dayNumbers[i].Foreground = ThemeResourceHelper.GetBrush("TextFillColorPrimaryBrush", theme);
                }
                else
                {
                    _dayNumbers[i].Foreground = ThemeResourceHelper.GetBrush("TextFillColorDisabledBrush", theme);
                }
            }

            // -- Cell background dimming for non-current-month --
            if (!cell.IsCurrentMonth)
            {
                _cellBorders[i].Opacity = 0.5;
            }
            else
            {
                _cellBorders[i].Opacity = 1.0;
            }

            // -- "+N more" label foreground (theme-aware) --
            _moreLabels[i].Foreground = ThemeResourceHelper.GetBrush("TextFillColorSecondaryBrush", theme);

            // -- Event chips --
            _eventPanels[i].Children.Clear();

            int eventCount = cell.Events.Count;
            int visibleCount = Math.Min(eventCount, MaxVisibleEvents);

            for (int j = 0; j < visibleCount; j++)
            {
                CalendarEventViewModel evt = cell.Events[j];
                _eventPanels[i].Children.Add(CreateEventChip(evt));
            }

            // -- "+N more" indicator --
            if (eventCount > MaxVisibleEvents)
            {
                int remaining = eventCount - MaxVisibleEvents;
                _moreLabels[i].Text = $"+{remaining} more";
                _moreLabels[i].Visibility = Visibility.Visible;
            }
            else
            {
                _moreLabels[i].Visibility = Visibility.Collapsed;
            }
        }
    }

    /// <summary>
    /// Creates a single event chip: a small colored rounded rectangle with the event title.
    /// </summary>
    private static Border CreateEventChip(CalendarEventViewModel evt)
    {
        string colorHex = evt.ColorHex ?? ColorHelper.CalendarColors[0];
        SolidColorBrush bgBrush;
        try
        {
            bgBrush = ColorHelper.ToBrush(colorHex);
        }
        catch
        {
            bgBrush = ColorHelper.ToBrush(ColorHelper.CalendarColors[0]);
        }

        var titleText = new TextBlock
        {
            Text = evt.Title,
            FontSize = 11,
            TextTrimming = TextTrimming.CharacterEllipsis,
            TextWrapping = TextWrapping.NoWrap,
            Foreground = new SolidColorBrush(Microsoft.UI.Colors.White),
            MaxLines = 1
        };

        var chip = new Border
        {
            CornerRadius = new CornerRadius(4),
            Background = bgBrush,
            Padding = new Thickness(6, 2, 6, 2),
            Margin = new Thickness(0, 1, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Child = titleText,
            Tag = evt // store the event so we can retrieve it on click
        };

        chip.PointerPressed += EventChip_PointerPressed;

        return chip;
    }

    /// <summary>
    /// Handles click on a day cell - navigates to the day view for that date.
    /// </summary>
    private void CellBorder_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (sender is Border border && border.Tag is int index && index >= 0 && index < TotalCells)
        {
            DateTime clickedDate = _cellDates[index];

            // Navigate the main window to day view for the clicked date
            if (App.MainAppWindow is MainWindow mainWindow)
            {
                mainWindow.NavigateToDayView(clickedDate);
            }
        }
    }

    /// <summary>
    /// Handles click on an event chip - shows event details in a dialog.
    /// </summary>
    private static async void EventChip_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        e.Handled = true; // prevent the cell click from also firing

        if (sender is not Border chip || chip.Tag is not CalendarEventViewModel evt)
            return;

        var result = await EventDialog.ShowManageDialog(chip.XamlRoot, evt.ToModel(), chip);

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
    /// Builds a simple read-only detail view for an event shown in the popup dialog.
    /// </summary>
    private static StackPanel BuildEventDetailContent(CalendarEventViewModel evt, FrameworkElement contextElement)
    {
        var theme = ThemeResourceHelper.GetEffectiveTheme(contextElement);
        var panel = new StackPanel { Spacing = 6 };

        // Time
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

        // Location (if present)
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

        // Description (if present)
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
