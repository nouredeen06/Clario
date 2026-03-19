using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;

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


    public static readonly StyledProperty<IList<DateTime>> SelectedDatesProperty =
        AvaloniaProperty.Register<DateRangePicker, IList<DateTime>>(
            nameof(SelectedDates), new List<DateTime>());

    public IList<DateTime> SelectedDates
    {
        get => GetValue(SelectedDatesProperty);
        set => SetValue(SelectedDatesProperty, value);
    }

    public static readonly StyledProperty<string> DisplayTextProperty =
        AvaloniaProperty.Register<DateRangePicker, string>(
            nameof(DisplayText), "Select Date");

    public string DisplayText
    {
        get => GetValue(DisplayTextProperty);
        set => SetValue(DisplayTextProperty, value);
    }

    private Button _button;
    private Popup _popup;
    private Calendar _calendar;


    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SelectedDatesProperty && _calendar != null)
        {
            _calendar.SelectedDates.Clear();

            foreach (var date in SelectedDates)
            {
                _calendar.SelectedDates.Add(date);
            }

            if (SelectionMode == CalendarSelectionMode.SingleDate)
                _popup.IsOpen = false;
        }
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _button = e.NameScope.Find<Button>("PART_Button");
        _popup = e.NameScope.Find<Popup>("PART_Popup");
        _calendar = e.NameScope.Find<Calendar>("PART_Calendar");

        if (_button != null)
        {
            _button.Click += (_, __) => _popup.IsOpen = true;
            _button.PointerEntered += (_, __) => PseudoClasses.Add(":pointerover");
            _button.PointerExited += (_, __) => PseudoClasses.Remove(":pointerover");
            _button.PointerPressed += (_, __) => PseudoClasses.Add(":pressed");
            _button.PointerReleased += (_, __) => PseudoClasses.Remove(":pressed");
        }

        if (_calendar != null)
        {
            _calendar.SelectedDatesChanged += (_, __) =>
            {
                SelectedDates.Clear();
                foreach (var date in _calendar.SelectedDates)
                {
                    SelectedDates.Add(date);
                }

                if (SelectionMode == CalendarSelectionMode.SingleDate && SelectedDates.Count == 1) _popup.IsOpen = false;
                else if (SelectionMode == CalendarSelectionMode.SingleRange && SelectedDates.Count == 2) _popup.IsOpen = false;
                UpdateDisplayText();
            };
            _calendar.AllowTapRangeSelection = true;
        }

        UpdateDisplayText();
    }

    private void UpdateDisplayText()
    {
        if (SelectedDates == null || SelectedDates.Count == 0)
        {
            switch (SelectionMode)
            {
                case CalendarSelectionMode.SingleDate:
                    DisplayText = "Select Date";
                    break;

                case CalendarSelectionMode.SingleRange:
                    DisplayText = "Select Date Range";
                    break;
                default:
                    DisplayText = "Select Date";
                    break;
            }

            return;
        }

        var ordered = SelectedDates.OrderBy(d => d).ToList();

        switch (SelectionMode)
        {
            case CalendarSelectionMode.SingleDate:
                DisplayText = ordered[0].ToString("MMM dd, yyyy");
                break;

            case CalendarSelectionMode.SingleRange:
                if (ordered.Count == 1)
                {
                    DisplayText = ordered[0].ToString("MMM dd, yyyy");
                }
                else
                {
                    DisplayText =
                        $"{ordered.First():MMM dd, yyyy} → {ordered.Last():MMM dd, yyyy}";
                }

                break;

            case CalendarSelectionMode.MultipleRange:
                DisplayText = string.Join(", ",
                    ordered.Select(d => d.ToString("MMM dd, yyyy")));
                break;

            default:
                DisplayText = ordered[0].ToString("MMM dd, yyyy");
                break;
        }
    }
}