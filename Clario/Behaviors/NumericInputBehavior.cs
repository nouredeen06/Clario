using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Xaml.Interactivity;

namespace Clario.Behaviors;

public class NumericInputBehavior : Behavior<TextBox>
{
    protected override void OnAttached()
    {
        base.OnAttached();
        AssociatedObject!.AddHandler(TextBox.TextInputEvent, OnTextInput, RoutingStrategies.Tunnel);
        AssociatedObject.TextChanged += OnTextChanged;
    }

    protected override void OnDetaching()
    {
        base.OnDetaching();
        AssociatedObject!.RemoveHandler(TextBox.TextInputEvent, OnTextInput);
        AssociatedObject.TextChanged -= OnTextChanged;
    }

    private void OnTextInput(object? sender, TextInputEventArgs e)
    {
        if (e.Text is null) return;
        foreach (var c in e.Text)
        {
            if (!char.IsDigit(c) && c != '.')
            {
                e.Handled = true;
                return;
            }
        }

        var current = (sender as TextBox)?.Text ?? "";
        if (e.Text.Contains('.') && current.Contains('.'))
        {
            e.Handled = true;
        }
    }

    private void OnTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (sender is not TextBox tb) return;
        var text = tb.Text ?? "";

        var clean = new string(text.Where(c => char.IsDigit(c) || c == '.').ToArray());

        var dotIndex = clean.IndexOf('.');
        if (dotIndex >= 0)
        {
            clean = clean[..(dotIndex + 1)] + clean[(dotIndex + 1)..].Replace(".", "");
        }

        if (clean != text)
        {
            var caret = tb.CaretIndex;
            tb.Text = clean;
            tb.CaretIndex = Math.Min(caret, clean.Length);
        }
    }
}