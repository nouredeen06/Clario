using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Calendar = Avalonia.Controls.Calendar;

namespace Clario.CustomControls;

public class DateRangePicker : TemplatedControl
{
    public static readonly StyledProperty<CalendarSelectionMode> SelectionModeProperty =
        AvaloniaProperty.Register<DateRangePicker, CalendarSelectionMode>(
            nameof(SelectionMode), CalendarSelectionMode.SingleRange);

    public CalendarSelectionMode SelectionMode
    {
        get => GetValue(SelectionModeProperty);
        set => SetValue(SelectionModeProperty, value);
    }

    private IList<DateTime> _selectedDates = new List<DateTime>();

    public static readonly DirectProperty<DateRangePicker, IList<DateTime>> SelectedDatesProperty =
        AvaloniaProperty.RegisterDirect<DateRangePicker, IList<DateTime>>(
            nameof(SelectedDates),
            o => o.SelectedDates,
            (o, v) => o.SelectedDates = v,
            defaultBindingMode: BindingMode.TwoWay);

    public IList<DateTime> SelectedDates
    {
        get => _selectedDates;
        set => SetAndRaise(SelectedDatesProperty, ref _selectedDates, value);
    }

    public static readonly StyledProperty<DateTime?> SelectedDateProperty =
        AvaloniaProperty.Register<DateRangePicker, DateTime?>(
            nameof(SelectedDate),
            defaultValue: null,
            defaultBindingMode: BindingMode.TwoWay);

    public DateTime? SelectedDate
    {
        get => GetValue(SelectedDateProperty);
        set => SetValue(SelectedDateProperty, value);
    }

    public static readonly StyledProperty<string> DisplayTextProperty =
        AvaloniaProperty.Register<DateRangePicker, string>(
            nameof(DisplayText), "Select Date");

    public string DisplayText
    {
        get => GetValue(DisplayTextProperty);
        set => SetValue(DisplayTextProperty, value);
    }


    private Button? _button;
    private Popup? _popup;
    private Calendar? _calendar;

    private bool _isSyncing = false;


    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        if (_button != null) _button.Click -= OnButtonClick;
        if (_calendar != null)
        {
            _calendar.SelectedDatesChanged -= OnCalendarDatesChanged;
            _calendar.RemoveHandler(PointerReleasedEvent, OnCalendarPointerReleased);
            // _calendar.RemoveHandler(Button.ClickEvent, OnCalendarInternalClick); // add this
        }

        _button = e.NameScope.Find<Button>("PART_Button");
        _popup = e.NameScope.Find<Popup>("PART_Popup");
        _calendar = e.NameScope.Find<Calendar>("PART_Calendar");

        if (_button != null)
            _button.Click += OnButtonClick;

        if (_calendar != null)
        {
            _calendar.AllowTapRangeSelection = true;

            _calendar.SelectedDatesChanged += OnCalendarDatesChanged;
            _calendar.AddHandler(PointerReleasedEvent, OnCalendarPointerReleased, RoutingStrategies.Tunnel);
            // _calendar.AddHandler(Button.ClickEvent, OnCalendarInternalClick, RoutingStrategies.Tunnel);

            SyncToCalendar();
        }

        UpdateDisplayText();
    }

    private void OnCalendarInternalClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
    }

    private void OnCalendarPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_calendar!.SelectionMode != CalendarSelectionMode.SingleDate) return;
        if (_isSyncing) return;
        if (_popup is null || !_popup.IsOpen) return;

        if (e.Source is not Control source) return;
        if (source.TemplatedParent is CalendarDayButton == false &&
            source.FindAncestorOfType<CalendarDayButton>() is null)
            return;

        var newDates = _calendar!.SelectedDates.OrderBy(d => d).ToList();

        _isSyncing = true;
        try
        {
            SelectedDates = newDates;
            SelectedDate = newDates.Count > 0 ? newDates[0] : null;
            UpdateDisplayText();

            bool shouldClose = SelectionMode switch
            {
                CalendarSelectionMode.SingleDate => newDates.Count >= 1,
                CalendarSelectionMode.SingleRange => newDates.Count >= 2,
                _ => false
            };

            if (shouldClose)
                _popup.IsOpen = false;
        }
        finally
        {
            _isSyncing = false;
        }
    }

    private void OnButtonClick(object? sender, RoutedEventArgs e)
    {
        if (_popup is null) return;

        SyncToCalendar();
        _popup.IsOpen = true;
    }

    private void OnCalendarDatesChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_calendar!.SelectionMode == CalendarSelectionMode.SingleDate) return;

        if (_isSyncing) return;

        if (_popup is null || !_popup.IsOpen) return;

        var newDates = _calendar!.SelectedDates.OrderBy(d => d).ToList();

        _isSyncing = true;
        try
        {
            SelectedDates = newDates;
            SelectedDate = newDates.Count > 0 ? newDates[0] : null;

            UpdateDisplayText();

            bool shouldClose = SelectionMode switch
            {
                CalendarSelectionMode.SingleDate => newDates.Count >= 1,
                CalendarSelectionMode.SingleRange => newDates.Count >= 2,
                _ => false
            };

            if (shouldClose)
                _popup.IsOpen = false;
        }
        finally
        {
            _isSyncing = false;
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (_isSyncing) return;

        if (change.Property == SelectedDatesProperty)
        {
            _isSyncing = true;
            try
            {
                var dates = SelectedDates?.OrderBy(d => d).ToList() ?? new List<DateTime>();
                SelectedDate = dates.Count > 0 ? dates[0] : null;
                SyncToCalendar();
                UpdateDisplayText();
            }
            finally
            {
                _isSyncing = false;
            }
        }
        else if (change.Property == SelectedDateProperty)
        {
            _isSyncing = true;
            try
            {
                SelectedDates = SelectedDate.HasValue
                    ? new List<DateTime> { SelectedDate.Value }
                    : new List<DateTime>();
                SyncToCalendar();
                UpdateDisplayText();
            }
            finally
            {
                _isSyncing = false;
            }
        }
    }

    private void SyncToCalendar()
    {
        if (_calendar is null || _isSyncing) return;

        _isSyncing = true;
        try
        {
            _calendar.SelectedDates.Clear();
            if (SelectedDates is not null)
            {
                foreach (var date in SelectedDates)
                    _calendar.SelectedDates.Add(date);
            }
        }
        finally
        {
            _isSyncing = false;
        }
    }

    private void UpdateDisplayText()
    {
        var culture = new CultureInfo("en-US");

        if (SelectedDates is null || SelectedDates.Count == 0)
        {
            DisplayText = SelectionMode switch
            {
                CalendarSelectionMode.SingleDate => "Select Date",
                CalendarSelectionMode.SingleRange => "Select Date Range",
                _ => "Select Date"
            };
            return;
        }

        var ordered = SelectedDates.OrderBy(d => d).ToList();

        DisplayText = SelectionMode switch
        {
            CalendarSelectionMode.SingleDate => ordered[0].ToString("MMM dd, yyyy", culture),

            CalendarSelectionMode.SingleRange => ordered.Count == 1
                ? ordered[0].ToString("MMM dd, yyyy", culture)
                : $"{ordered.First().ToString("MMM dd, yyyy", culture)} → {ordered.Last().ToString("MMM dd, yyyy", culture)}",

            CalendarSelectionMode.MultipleRange => string.Join(", ",
                ordered.Select(d => d.ToString("MMM dd, yyyy", culture))),

            _ => ordered[0].ToString("MMM dd, yyyy", culture)
        };
    }
}