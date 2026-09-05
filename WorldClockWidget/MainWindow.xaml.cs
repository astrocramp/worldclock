using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
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

    private readonly List<TimeZoneRow> _rows = new();
    private readonly List<TextBlock> _dateLabels = new();
    private DateTime[] _columnInstantsUtc = Array.Empty<DateTime>();
    private DateTime _baseDate = DateTime.Today;
    private int _selectedColumn;

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

    private void ShiftDay(int days)
    {
        var hourOfDay = ((_selectedColumn - HoursBefore) % 24 + 24) % 24;
        _baseDate = _baseDate.AddDays(days);
        _selectedColumn = HoursBefore + hourOfDay;
        BuildGrid();
        ScrollToColumn(_selectedColumn);
    }

    private void PrevDayButton_Click(object sender, RoutedEventArgs e) => ShiftDay(-1);

    private void NextDayButton_Click(object sender, RoutedEventArgs e) => ShiftDay(1);

    private void TodayButton_Click(object sender, RoutedEventArgs e) => GoToToday();

    private void AlwaysOnTopCheckBox_Changed(object sender, RoutedEventArgs e) => Topmost = AlwaysOnTopCheckBox.IsChecked == true;

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
                Content = "✕",
                Width = 22,
                Height = 22,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(4, 4, 0, 0)
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
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0, 0, 1, 1),
            BorderBrush = new SolidColorBrush(Color.FromRgb(224, 224, 224)),
            Focusable = false
        };
        button.Click += (_, _) => SelectColumn(columnIndex);
        return button;
    }

    private void RefreshHeaders()
    {
        DateLabel.Text = _baseDate.ToString("dddd, MMMM d, yyyy");

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
