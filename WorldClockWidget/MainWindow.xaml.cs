using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using WorldClockWidget.Models;

namespace WorldClockWidget;

public partial class MainWindow : Window
{
    private const int ColumnWidth = 64;
    private const int RowHeight = 64;
    private const int HoursBefore = 24;
    private const int HoursAfter = 48;
    private const int TotalColumns = HoursBefore + HoursAfter;

    private static readonly SolidColorBrush NightCellBrush = new(Color.FromRgb(0xE4, 0xE4, 0xEA));

    private readonly List<TimeZoneRow> _rows = new();
    private readonly List<TextBlock> _dateLabels = new();
    private DateTime[] _columnInstantsUtc = Array.Empty<DateTime>();
    private DateTime _baseDate = DateTime.Today;
    private int _selectedColumn;
    private bool _alwaysOnTop;

    public MainWindow()
    {
        InitializeComponent();

        _rows.Add(new TimeZoneRow("Local time", TimeZoneInfo.Local, isLocal: true));
        var tokyo = TryFindTimeZone("Tokyo Standard Time");
        if (tokyo != null)
        {
            _rows.Add(new TimeZoneRow(FriendlyZoneName(tokyo), tokyo));
        }

        Loaded += (_, _) => GoToToday();
    }

    private static TimeZoneInfo TryFindTimeZone(string id)
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(id);
        }
        catch (TimeZoneNotFoundException)
        {
            return null;
        }
    }

    private static string FriendlyZoneName(TimeZoneInfo zone)
    {
        var name = zone.DisplayName;
        var parenEnd = name.IndexOf(')');
        return parenEnd >= 0 && parenEnd + 1 < name.Length ? name[(parenEnd + 1)..].Trim() : name;
    }

    private void GoToToday()
    {
        _baseDate = DateTime.Today;
        _selectedColumn = HoursBefore + DateTime.Now.Hour;
        BuildGrid();
        ScrollToColumn(_selectedColumn);
    }

    private void SetBaseDate(DateTime newDate)
    {
        var hourOfDay = ((_selectedColumn - HoursBefore) % 24 + 24) % 24;
        _baseDate = newDate.Date;
        _selectedColumn = HoursBefore + hourOfDay;
        BuildGrid();
        ScrollToColumn(_selectedColumn);
    }

    private void AdjustMonth(int delta)
    {
        var year = _baseDate.Year;
        var month = _baseDate.Month + delta;
        while (month < 1) { month += 12; year--; }
        while (month > 12) { month -= 12; year++; }
        var day = Math.Min(_baseDate.Day, DateTime.DaysInMonth(year, month));
        SetBaseDate(new DateTime(year, month, day));
    }

    private void AdjustDay(int delta) => SetBaseDate(_baseDate.AddDays(delta));

    private void AdjustYear(int delta)
    {
        var year = _baseDate.Year + delta;
        var day = Math.Min(_baseDate.Day, DateTime.DaysInMonth(year, _baseDate.Month));
        SetBaseDate(new DateTime(year, _baseDate.Month, day));
    }

    private void TodayButton_Click(object sender, RoutedEventArgs e) => GoToToday();

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

    private void PinButton_Click(object sender, RoutedEventArgs e)
    {
        _alwaysOnTop = !_alwaysOnTop;
        Topmost = _alwaysOnTop;
        PinIcon.Opacity = _alwaysOnTop ? 1.0 : 0.45;
    }

    private void AddCityButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new AddCityWindow { Owner = this };
        if (dialog.ShowDialog() == true && dialog.SelectedTimeZone != null)
        {
            _rows.Add(new TimeZoneRow(FriendlyZoneName(dialog.SelectedTimeZone), dialog.SelectedTimeZone));
            BuildGrid();
        }
    }

    private void RemoveRow(TimeZoneRow row)
    {
        _rows.Remove(row);
        BuildGrid();
    }

    private void SelectColumn(int columnIndex)
    {
        _selectedColumn = columnIndex;
        BuildGrid();
    }

    // Columns are stored as absolute UTC instants so every row's local time is
    // derived with proper per-zone DST handling instead of a fixed hour offset.
    private void BuildColumns()
    {
        _columnInstantsUtc = new DateTime[TotalColumns];
        var localMidnight = DateTime.SpecifyKind(_baseDate, DateTimeKind.Unspecified);
        for (var i = 0; i < TotalColumns; i++)
        {
            var localInstant = localMidnight.AddHours(i - HoursBefore);
            _columnInstantsUtc[i] = TimeZoneInfo.ConvertTimeToUtc(localInstant, TimeZoneInfo.Local);
        }
    }

    private void BuildGrid()
    {
        BuildColumns();
        BuildDateSpinners();

        HeaderPanel.Children.Clear();
        HourGrid.Children.Clear();
        HourGrid.ColumnDefinitions.Clear();
        HourGrid.RowDefinitions.Clear();
        _dateLabels.Clear();

        for (var c = 0; c < TotalColumns; c++)
        {
            HourGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(ColumnWidth) });
        }

        for (var r = 0; r < _rows.Count; r++)
        {
            HourGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(RowHeight) });
            HeaderPanel.Children.Add(BuildHeaderCell(_rows[r]));

            for (var c = 0; c < TotalColumns; c++)
            {
                var cell = BuildHourCell(_rows[r], c);
                Grid.SetRow(cell, r);
                Grid.SetColumn(cell, c);
                HourGrid.Children.Add(cell);
            }
        }

        var highlight = new Border
        {
            BorderBrush = Brushes.DodgerBlue,
            BorderThickness = new Thickness(2),
            Background = new SolidColorBrush(Color.FromArgb(24, 30, 144, 255)),
            IsHitTestVisible = false
        };
        Grid.SetRow(highlight, 0);
        Grid.SetRowSpan(highlight, Math.Max(_rows.Count, 1));
        Grid.SetColumn(highlight, _selectedColumn);
        HourGrid.Children.Add(highlight);

        RefreshHeaders();
    }

    private void BuildDateSpinners()
    {
        DateSpinnerPanel.Children.Clear();
        DateSpinnerPanel.Children.Add(BuildDateSpinner(_baseDate.ToString("MMMM"), 92, AdjustMonth));
        DateSpinnerPanel.Children.Add(BuildDateSpinner(_baseDate.Day.ToString(), 46, AdjustDay));
        DateSpinnerPanel.Children.Add(BuildDateSpinner(_baseDate.Year.ToString(), 62, AdjustYear));
    }

    private UIElement BuildDateSpinner(string text, double width, Action<int> onAdjust)
    {
        var container = new StackPanel { Width = width, Margin = new Thickness(3, 0, 3, 0) };

        var upButton = BuildSpinnerChevron("");
        upButton.Click += (_, _) => onAdjust(1);

        var label = new TextBlock
        {
            Text = text,
            FontSize = 14,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 1, 0, 1)
        };

        var downButton = BuildSpinnerChevron("");
        downButton.Click += (_, _) => onAdjust(-1);

        container.Children.Add(upButton);
        container.Children.Add(label);
        container.Children.Add(downButton);

        container.PreviewMouseWheel += (_, e) =>
        {
            onAdjust(e.Delta > 0 ? 1 : -1);
            e.Handled = true;
        };

        return container;
    }

    private Button BuildSpinnerChevron(string glyph)
    {
        return new Button
        {
            Content = new TextBlock { Text = glyph, FontFamily = new FontFamily("Segoe MDL2 Assets"), FontSize = 10 },
            Style = (Style)FindResource("SpinnerChevronButtonStyle")
        };
    }

    private UIElement BuildHeaderCell(TimeZoneRow row)
    {
        var grid = new Grid { Height = RowHeight };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var textStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        textStack.Children.Add(new TextBlock { Text = row.DisplayName, FontWeight = FontWeights.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis });
        var dateText = new TextBlock { Foreground = Brushes.Gray, FontSize = 12 };
        textStack.Children.Add(dateText);
        Grid.SetColumn(textStack, 0);
        grid.Children.Add(textStack);
        _dateLabels.Add(dateText);

        if (!row.IsLocal)
        {
            var removeButton = new Button
            {
                Content = new TextBlock { Text = "", FontFamily = new FontFamily("Segoe MDL2 Assets"), FontSize = 12 },
                ToolTip = "Remove city"
            };
            removeButton.Click += (_, _) => RemoveRow(row);
            Grid.SetColumn(removeButton, 1);
            grid.Children.Add(removeButton);
        }

        return grid;
    }

    private UIElement BuildHourCell(TimeZoneRow row, int columnIndex)
    {
        var instantUtc = _columnInstantsUtc[columnIndex];
        var localTime = TimeZoneInfo.ConvertTimeFromUtc(instantUtc, row.Zone);
        var isDay = localTime.Hour is >= 6 and < 18;

        var stack = new StackPanel
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            IsHitTestVisible = false
        };
        stack.Children.Add(new TextBlock
        {
            Text = isDay ? "☀" : "🌙",
            FontSize = 14,
            HorizontalAlignment = HorizontalAlignment.Center,
            Foreground = isDay ? Brushes.Goldenrod : Brushes.SlateGray
        });
        stack.Children.Add(new TextBlock
        {
            Text = localTime.ToString("h:mm tt"),
            FontSize = 12,
            HorizontalAlignment = HorizontalAlignment.Center
        });

        var button = new Button
        {
            Content = stack,
            Style = (Style)FindResource("HourCellButtonStyle"),
            Background = isDay ? Brushes.Transparent : NightCellBrush
        };
        button.Click += (_, _) => SelectColumn(columnIndex);
        return button;
    }

    private void RefreshHeaders()
    {
        var selectedInstantUtc = _columnInstantsUtc[_selectedColumn];
        var localOffset = TimeZoneInfo.Local.GetUtcOffset(selectedInstantUtc);

        for (var i = 0; i < _rows.Count; i++)
        {
            var row = _rows[i];
            var localTime = TimeZoneInfo.ConvertTimeFromUtc(selectedInstantUtc, row.Zone);
            var offsetHours = (row.Zone.GetUtcOffset(selectedInstantUtc) - localOffset).TotalHours;

            var text = localTime.ToString("M/d/yyyy");
            if (Math.Abs(offsetHours) > 0.01)
            {
                var sign = offsetHours > 0 ? "+" : "";
                text += $", {sign}{offsetHours:0.##} hrs";
            }

            _dateLabels[i].Text = text;
        }
    }

    private void ScrollToColumn(int columnIndex)
    {
        Dispatcher.BeginInvoke(new Action(() =>
        {
            var viewportWidth = HourScrollViewer.ViewportWidth;
            var target = columnIndex * ColumnWidth - viewportWidth / 2 + ColumnWidth / 2.0;
            HourScrollViewer.ScrollToHorizontalOffset(Math.Max(0, target));
        }), DispatcherPriority.Loaded);
    }
}
