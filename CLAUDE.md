# Clario — Claude Code Instructions

Clario is a cross-platform personal finance tracking app.
See @NEW_CHAT_CONTEXT.md for full project context before starting any task.

---

## Tech Stack

- **UI**: Avalonia UI XPlat (.NET 9), CommunityToolkit.MVVM
- **Backend**: Supabase (PostgreSQL, Auth, RLS, Realtime)
- **Charts**: LiveCharts2 (SkiaSharp)
- **IDE**: JetBrains Rider, Windows dev machine (Arabic region — always use `en-US` CultureInfo)

## Project Structure

```
Clario/                  ← shared (ViewModels, Models, Services, Data, CustomControls, Behaviors, Converters)
Clario.Desktop/          ← Windows/macOS/Linux entry point
Clario.Android/          ← Android entry point
Views/                   ← desktop AXAML views only
MobileViews/             ← mobile AXAML views only
```

## Build & Run

```bash
# Desktop
dotnet run --project Clario.Desktop

# Android (requires connected device or emulator)
dotnet build Clario.Android -c Release

# Verify build
dotnet build Clario.sln
```

## Platform Detection

```csharp
// Always check this before any platform-specific logic
App.IsMobile  // true on Android/iOS, false on desktop
```

---

## CRITICAL RULES — Read before every task

### AXAML Rules

- **ALWAYS** use `{DynamicResource}` for theme colors, never hardcode hex
- **NEVER** put DataTemplates in AXAML — ViewLocator handles all view resolution
- **NEVER** add `MinWidth`/`MinHeight` to UserControl in mobile views
- **NEVER** use `BoxShadow` in mobile views
- Use `x:CompileBindings="False"` on shell views with dynamic DataContext
- Desktop views go in `Views/`, mobile views go in `MobileViews/` named `{Name}ViewMobile.axaml`
- Icon background opacity always: `<SolidColorBrush Color="..." Opacity="0.15"/>`
- Separator between list items: `Spacing="1"` on StackPanel + `BorderSubtle` background on container

### ViewModel Rules

- **NEVER** fetch data in child ViewModel constructors
- **NEVER** trigger initialization from `partial void On{Property}Changed` when VM depends on multiple properties
- Call `Initialize()` explicitly after object initializer sets all required fields
- Child VMs have `public required ViewModelBase parentViewModel`
- Replace lists entirely to trigger bindings — never mutate and expect updates

### C# Rules

- Always `en-US` CultureInfo for dates/numbers (Windows has Arabic region)
- Use `Task.WhenAll` for parallel async fetches in `InitializeApp`
- Use `_ = SomeAsyncMethod()` for fire-and-forget with try/catch inside the method
- Wrap fire-and-forget in try/catch — exceptions are silently swallowed

### Style Classes

```
accented   → primary action button (AccentBlue bg)
base       → secondary action button
nav        → transparent navigation/toggle button
danger     → destructive action (DangerButtonBackground + AccentRed text)
ghost      → transparent TextBox (no border, any state)
label      → uppercase muted TextBlock label
muted      → TextMuted foreground
mobile     → root class on mobile views (enables mobile overrides)
```

---

## Design Tokens (quick reference)

```
BgBase/BgSurface/BgSidebar/BgHover
BorderSubtle/BorderAccent
TextPrimary/TextSecondary/TextMuted/TextDisabled
AccentBlue/AccentGreen/AccentYellow/AccentRed/AccentPurple/AccentOrange/AccentPink
IconBgBlue/IconBgGreen/IconBgRed/IconBgOrange/IconBgPurple/IconBgPink
BadgeBgRed/BadgeBgYellow/BadgeBgGreen/BadgeBgBlue
DangerButtonBackground/DangerButtonBorder
SvgPrimary/SvgSecondary/SvgMuted/SvgDisabled/SvgBlue/SvgGreen/SvgYellow/SvgRed
```

## SVG Pattern

```xml
<Svg Path="../Assets/Icons/icon-name.svg" Width="16" Height="16" Css="{DynamicResource SvgBlue}"/>
```

---

## Converters (quick reference)

| Key | Usage |
|-----|-------|
| `HexToColorConverter` | `ConverterParameter=color/css/brush` |
| `AmountColorConverter` | type string → AccentRed/Green brush |
| `AmountSignConverter` | MultiBinding(amount, type) → `+$x.xx` |
| `BoolToColorConverter` | `ConverterParameter='#hex1\|#hex2'` |
| `BoolToCssConverter` | `ConverterParameter='#hex1\|#hex2'` → SVG CSS |
| `SvgPathFromName` | `"icon-name"` → `"../Assets/Icons/icon-name.svg"` |
| `DateFormatConverter` | DateTime → string (always en-US) |
| `EqualValueConverter` | MultiBinding equality → bool |
| `NetworthSumConverter` | MultiBinding(income, expenses) → net |
| `PercentageConverter` | MultiBinding(value, total) → % |

---

## Flyout Pattern

```xml
<Button.Flyout>
    <Flyout Placement="BottomEdgeAlignedRight"
            FlyoutPresenterTheme="{StaticResource TransparentFlyoutPresenter}">
        <views:SomeView/>
    </Flyout>
</Button.Flyout>
```

## Modal Overlay Pattern

```xml
<!-- In MainView content area, on top of ContentControl -->
<views:SomeFormView
    DataContext="{Binding SomeFormVM}"
    IsVisible="{Binding IsFormVisible}"/>
```
The view's root Grid must have `<Border Background="#70000000"/>` as the dim layer.

## Bottom Sheet Pattern (mobile)

- Controlled via `ShowSheet()` / `HideSheet()` public methods in code-behind
- `TranslateTransform` animation: CubicEaseOut 320ms up, CubicEaseIn 260ms down
- `OverlayGrid.IsVisible = false` by default in AXAML
- Set `BottomSheet.MaxHeight = Bounds.Height * 0.82` in `OnAttachedToVisualTree`

---

## Supabase

```csharp
// All queries via DataRepo
DataRepo.General.FetchTransactions()
DataRepo.General.FetchCategories()
DataRepo.General.FetchAccounts()
DataRepo.General.FetchBudgets()
DataRepo.General.FetchProfileInfo()
// etc.

// Auth
SupabaseService.Client.Auth.CurrentUser
SupabaseService.Client.Auth.SignIn(email, password)
SupabaseService.Client.Auth.SignOut()
SupabaseService.Client.Auth.Update(new UserAttributes { ... })
```

RLS: all tables enabled. INSERT uses `WITH CHECK (auth.uid() = user_id)`.

---

## Verification

After any code change, verify by:
1. `dotnet build Clario.sln` — must have zero errors
2. Check AXAML for `{DynamicResource}` on all color bindings
3. Check that no ViewModel constructor fetches data
4. On mobile views: no `BoxShadow`, no `MinWidth`/`MinHeight`, has `Classes="mobile"` on root

---

## What's Not Yet Built

- `AuthViewMobile` — needs creating
- Settings view mobile version — needs creating  
- Analytics view — not designed yet
- Light theme — token file incomplete, do not assume it's complete
- Real-time Supabase subscriptions — not wired to UI
