---
name: avalonia
description: >
  Use when working on any Avalonia UI code — AXAML, control styling, bindings,
  control templates, animations, custom controls, platform differences, or
  LiveCharts2/Svg.Skia integration. Triggers on questions about Avalonia
  controls, properties, ControlThemes, styles, pseudo-classes, DataTemplates,
  ViewLocator, or any "how do I do X in Avalonia" question.
---

# Avalonia UI Skill

You are working in an Avalonia UI project. This skill gives you accurate,
verified knowledge about Avalonia and prevents hallucinating WPF-style patterns
that do not work in Avalonia.

---

## Step 1 — Check before you answer

**NEVER** answer from memory alone for:
- Specific control properties or template part names
- Pseudo-class selectors (`:pointerover`, `:pressed`, `:focus`, etc.)
- Animation API (`Animation`, `KeyFrame`, `Cue`, `Easing` classes)
- `ControlTheme` vs `Style` syntax differences
- Platform-specific behaviors (mobile vs desktop)
- LiveCharts2 or Svg.Skia properties

**Always verify** using one of these sources in order:

1. **Official docs**: `https://docs.avaloniaui.net/docs/reference/controls/{control-name}`
2. **GitHub source** (most reliable for exact property names):
   `https://github.com/AvaloniaUI/Avalonia/blob/master/src/Avalonia.Controls/{ControlName}.cs`
3. **Avalonia samples**: `https://github.com/AvaloniaUI/Avalonia.Samples`

For styling/theming questions also check:
- `https://github.com/AvaloniaUI/Avalonia/tree/master/src/Avalonia.Themes.Fluent/Controls`

---

## Step 2 — Core Avalonia vs WPF differences

These are frequent sources of errors. Apply automatically:

### Styling
```xml
<!-- Avalonia: CSS-like selectors -->
<Style Selector="Button.primary:pointerover /template/ ContentPresenter">
    <Setter Property="Background" Value="Blue"/>
</Style>

<!-- NOT WPF DataTriggers — those do not exist in Avalonia -->
<!-- NOT WPF Triggers — use pseudo-classes instead -->
```

### ControlTheme (Avalonia 11+)
```xml
<!-- For re-theming built-in controls use ControlTheme, not Style -->
<ControlTheme x:Key="{x:Type Button}" TargetType="Button">
    <Setter Property="Template">
        <ControlTemplate>...</ControlTemplate>
    </Setter>
</ControlTheme>
```

### Bindings
```xml
<!-- x:CompileBindings="True" (default) requires x:DataType -->
<!-- Use x:CompileBindings="False" on shell/dynamic views -->
<!-- DynamicResource NOT StaticResource for theme colors -->
<!-- No ElementName binding across UserControl boundaries — use RelativeSource or pass via property -->
```

### No DataTriggers
Avalonia has no DataTriggers. Use instead:
- `Classes.myClass="{Binding SomeBool}"` + style on `.myClass`
- `IsVisible="{Binding SomeBool}"`
- `MultiBinding` with converter

### x:Name in code-behind
`x:Name` does NOT create direct fields in Avalonia. Access named controls via:
```csharp
var btn = this.Get<Button>("PART_Button");       // throws if not found
var btn = this.FindControl<Button>("PART_Button"); // returns null if not found
// TranslateTransform cannot have x:Name — access via RenderTransform:
var tf = (TranslateTransform)someControl.RenderTransform!;
```

### Animations in code-behind
```csharp
var animation = new Animation
{
    Duration = TimeSpan.FromMilliseconds(320),
    Easing   = new CubicEaseOut(),
    FillMode = FillMode.Forward,
    Children =
    {
        new KeyFrame { Cue = new Cue(0d), Setters = { new Setter(TranslateTransform.YProperty, 0d) } },
        new KeyFrame { Cue = new Cue(1d), Setters = { new Setter(TranslateTransform.YProperty, 300d) } }
    }
};
await animation.RunAsync(targetControl);
```

### Platform detection
```csharp
bool isMobile = ApplicationLifetime is ISingleViewApplicationLifetime;
// App.IsMobile is the project's cached version
```

---

## Step 3 — Known Avalonia gotchas from this project

### ViewLocator (no DataTemplates in AXAML)
```csharp
// ViewLocator auto-resolves: {Name}ViewModel → {Name}View (desktop) or {Name}ViewMobile (mobile)
// Do NOT register DataTemplates in AXAML
// Register FuncDataTemplate in App.axaml.cs code-behind if needed
```

### Observable property initialization order
Object initializers set properties one by one — `partial void On{Property}Changed` fires
immediately, before other properties are set. **Never** trigger initialization logic from
property changed handlers when the VM needs multiple properties. Always use an explicit
`Initialize()` method called after the object initializer.

```csharp
// WRONG
partial void OnTransactionsChanged(List<Transaction> value) => ProcessData(); // Categories may be null

// RIGHT
var vm = new MyViewModel { Transactions = t, Categories = c, Accounts = a };
vm.Initialize(); // all props guaranteed set
```

### ObservableCollection mutations
Mutating a `List<T>` never triggers binding updates. Replace the entire collection:
```csharp
MyList = new List<T>(newItems); // triggers OnPropertyChanged
// NOT: MyList.Add(item); // binding won't update for List<T>
```
For `ObservableCollection<T>`, `.Add()` and `.Remove()` do trigger updates but `.Clear()` +
re-add causes a full re-render. Prefer replacing the collection for large updates.

### ScrollViewer + LiveCharts2
LiveCharts2 CartesianChart intercepts scroll events. Forward them manually:
```csharp
protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
{
    base.OnAttachedToVisualTree(e);
    var charts = this.GetVisualDescendants().OfType<CartesianChart>();
    foreach (var chart in charts)
        chart.AddHandler(PointerWheelChangedEvent, OnChartScroll, RoutingStrategies.Tunnel);
}
private void OnChartScroll(object? sender, PointerWheelEventArgs e)
{
    var sv = this.GetVisualAncestors().OfType<ScrollViewer>().FirstOrDefault();
    if (sv is null) return;
    sv.Offset = new Vector(sv.Offset.X, sv.Offset.Y - e.Delta.Y * sv.SmallChange.Height * 3);
    e.Handled = true;
}
```

### Half-donut chart
```xml
<Border Height="150" ClipToBounds="True">
    <lvc:PieChart Series="{Binding ...}" Height="300" Margin="0,0,0,-150"
                  InitialRotation="-180" MaxAngle="180" LegendPosition="Hidden"
                  ZoomMode="None"/>
</Border>
```

### Svg.Skia CSS
```xml
<!-- stroke-based (Lucide icons) -->
<Svg Path="../Assets/Icons/name.svg" Css="{DynamicResource SvgBlue}"/>

<!-- SvgBlue resource = "path, circle, rect, ellipse, line, polyline, polygon, text, use { stroke: #7B9CFF; }" -->
<!-- Fill-based icons use SvgFillBlue etc. -->
```

### Mobile-specific AXAML rules
- No `BoxShadow` — GPU expensive, causes jitter
- No `MinWidth`/`MinHeight` on UserControl root
- Add `Classes="mobile"` to root element for mobile-specific style overrides
- Use `VirtualizingStackPanel` in ItemsControl for long lists
- Page size 10 on mobile vs 25 on desktop

### CalendarDayButton / Calendar
Avalonia's Calendar uses `CalendarDayButton` not `CalendarDayItem`.
Template parts: `PART_MonthView`, `PART_YearView`, `PART_HeaderButton`, `PART_PreviousButton`, `PART_NextButton`.

### FlyoutPresenter
```xml
<!-- Custom transparent flyout presenter must be a ControlTheme in Resources, not Styles -->
<ControlTheme x:Key="TransparentFlyoutPresenter" TargetType="FlyoutPresenter">
    <Setter Property="Background" Value="Transparent"/>
    <Setter Property="BorderThickness" Value="0"/>
    <Setter Property="Padding" Value="0"/>
</ControlTheme>
```

### TextBox ghost class
```xml
<!-- Transparent textbox that works in all states -->
<Style Selector="TextBox.ghost">
    <Setter Property="Background" Value="Transparent"/>
    <Setter Property="BorderThickness" Value="0"/>
    <Setter Property="Padding" Value="0"/>
    <Setter Property="FocusAdorner" Value="{x:Null}"/>
</Style>
<Style Selector="TextBox.ghost:pointerover /template/ Border#PART_BorderElement">
    <Setter Property="Background" Value="Transparent"/>
    <Setter Property="BorderThickness" Value="0"/>
</Style>
<!-- Also add :focus and :disabled variants -->
```

---

## Step 4 — How to look up unfamiliar Avalonia APIs

### For a control's properties:
```
Fetch: https://docs.avaloniaui.net/docs/reference/controls/{control-name-lowercase}
```

### For template part names (e.g. what's inside a ComboBox):
```
Search GitHub: https://github.com/search?q=repo:AvaloniaUI/Avalonia+PART_+{ControlName}&type=code
Or fetch: https://github.com/AvaloniaUI/Avalonia/blob/master/src/Avalonia.Themes.Fluent/Controls/{ControlName}.axaml
```

### For pseudo-class selectors:
```
Fetch: https://docs.avaloniaui.net/docs/reference/styles/pseudo-classes
```

### For animation classes (Easing, FillMode, etc.):
```
Fetch: https://docs.avaloniaui.net/docs/guides/graphics-and-animations/animation
```

### For ColorPicker internals:
```
Fetch: https://raw.githubusercontent.com/AvaloniaUI/Avalonia/refs/heads/master/src/Avalonia.Controls.ColorPicker/Themes/Fluent/ColorPicker.xaml
```

---

## Step 5 — Response format

1. State what you verified and where
2. Provide the correct AXAML or C# with no WPF-isms
3. Flag any Avalonia version caveat if relevant (project uses 11.x)
4. If something cannot be done via AXAML, explain the code-behind approach
5. Never guess at property names — fetch source if uncertain
