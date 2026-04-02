# Clario — New Chat Context Summary

Paste this entire file at the start of a new chat to continue work without quality degradation.

---

## What Clario Is

**Clario** is a cross-platform personal finance tracking app built by Nouredeen (based in Jordan, Windows dev environment with Arabic region — always use `en-US` CultureInfo for dates/numbers). The app is in active development, not yet released.

**Tech stack:**
- Avalonia UI XPlat template — C# .NET 9
- CommunityToolkit.MVVM (`[ObservableProperty]`, `[RelayCommand]`)
- Supabase backend (PostgreSQL, Auth, RLS, Realtime)
- LiveCharts2 (SkiaSharp) for charts
- Avalonia.Controls.ColorPicker (built-in)
- JetBrains Rider on Windows

---

## Project Structure

```
Clario/                     ← shared (ViewModels, Models, Services, Data, CustomControls, Behaviors, Converters)
Clario.Desktop/             ← Windows/macOS/Linux entry point (Program.cs)
Clario.Android/             ← Android entry point (MainActivity.cs)
Clario.iOS/                 ← iOS entry point (AppDelegate.cs)
Clario.Browser/             ← Web/WASM entry point
Views/                      ← desktop AXAML views
MobileViews/                ← mobile AXAML views
```

**Namespaces:** `Clario.ViewModels`, `Clario.Views`, `Clario.MobileViews`, `Clario.Models`, `Clario.Data`, `Clario.Services`, `Clario.CustomControls`, `Clario.Behaviors`, `Clario.Converters`

---

## Architecture

### Platform detection
```csharp
// App.axaml.cs
public static bool IsMobile { get; private set; }
IsMobile = ApplicationLifetime is ISingleViewApplicationLifetime;
```

### ViewLocator (auto view resolution)
```csharp
// mobile: tries Clario.MobileViews.{Name}ViewMobile, falls back to Clario.Views.{Name}View
// desktop: Clario.Views.{Name}View
// No DataTemplates in AXAML — all registered via ViewLocator in App.axaml
```

### App.axaml startup flow
```csharp
public override async void OnFrameworkInitializationCompleted()
{
    base.OnFrameworkInitializationCompleted();
    IsMobile = ApplicationLifetime is ISingleViewApplicationLifetime;
    // ViewLocator handles platform routing automatically

    CultureInfo.DefaultThreadCurrentCulture = new CultureInfo("en-US");
    CultureInfo.DefaultThreadCurrentUICulture = new CultureInfo("en-US");

    await SupabaseService.InitializeAsync(
        IsMobile && !(ApplicationLifetime is ISingleViewApplicationLifetime browser)
            ? new FileSessionStorage()
            : new FileSessionStorage() // BrowserSessionStorage for web
    );
    await SupabaseService.Client.Auth.RetrieveSessionAsync(); // catch { }

    var user = SupabaseService.Client.Auth.CurrentUser;
    var profile = await DataRepo.General.FetchProfileInfo();
    if (profile is not null) ThemeService.SwitchToTheme(profile.Theme);

    // desktop
    desktop.MainWindow = new MainWindow { DataContext = user != null ? new MainViewModel() : new AuthViewModel() };
    // mobile
    singleView.MainView = new MobileShellView { DataContext = user != null ? new MainViewModel() : new AuthViewModel() };
}
```

### MainViewModel (shell)
```csharp
// constructor: sets CurrentView = _dashboardViewModel immediately, then _ = InitializeApp()
// InitializeApp(): Task.WhenAll all fetches, then pushes data into child VMs, calls vm.Initialize()
// Navigation: isOnDashboard, isOnTransactions, isOnAccounts, isOnBudget (bool properties)
// Commands: GoToDashboardCommand, GoToTransactionsCommand, GoToAccountsCommand, GoToBudgetCommand
// Modal: IsTransactionFormVisible, TransactionFormViewModel instance
// DeleteAccount: IsDeleteAccountDialogVisible, DeleteAccountDialogViewModel instance
// Data lists: List<Transaction> _transactions, _categories, _accounts, _budgets, Profile
```

### ViewModel pattern
```csharp
public partial class MyViewModel : ViewModelBase
{
    public required ViewModelBase parentViewModel;
    // Data set as plain fields/properties, NOT fetched in constructor
    public void Initialize() { /* called after all data is set */ }
}
```

**Critical rule:** Never fetch data in child VM constructors. Never trigger init from `partial void On{Property}Changed` when VM depends on multiple properties. Always call `Initialize()` explicitly after object initializer sets all required fields.

### DataRepo
```csharp
public static class DataRepo { public static GeneralDataRepo General { get; } = new(); }
// Methods: FetchTransactions, FetchCategories, FetchAccounts, FetchBudgets, FetchProfileInfo
// InsertTransaction, UpdateTransaction, DeleteTransaction
// MigrateTransactions(fromId, toId), RecalculateAccountBalance(id), DeleteAccount(id)
```

---

## Supabase Schema

### Tables
- `profiles` — `id` (UUID = auth.uid()), `display_name`, `avatar_url`, `currency`, `theme`, `language`, `savings_goal` (DECIMAL), `savings_goal_deadline` (DATE), `created_at`, `updated_at`
- `accounts` — `id`, `user_id`, `name`, `type`, `institution`, `mask`, `currency`, `balance`, `credit_limit`, `is_archived`, `opened_at`, `created_at`
- `categories` — `id`, `user_id`, `name`, `icon` (lucide icon name), `color` (hex), `type` (income/expense), `created_at`
- `transactions` — `id`, `user_id`, `account_id`, `category_id`, `amount` (DECIMAL, always positive), `type` (income/expense), `description`, `note`, `date` (DATE), `created_at`
- `budgets` — `id`, `user_id`, `category_id`, `amount`, `period` (monthly/quarterly/yearly), `alert_threshold` (int, default 80), `rollover` (bool), `created_at`

### RLS
- All tables have RLS enabled
- SELECT/UPDATE/DELETE: `USING (auth.uid() = user_id)`
- INSERT: `WITH CHECK (auth.uid() = user_id)`
- Profile auto-created via DB trigger on `auth.users` INSERT (SECURITY DEFINER)

### Session persistence
- `FileSessionStorage` — desktop/mobile, stores in `%AppData%\Clario\session.json`
- `BrowserSessionStorage` — web, uses JS interop to localStorage
- `SupabaseSessionHandler` implements `IGotrueSessionPersistence<Session>`
- Session restored manually: `await client.Auth.SetSession(accessToken, refreshToken)`
- `AutoRefreshToken = true`, `AutoConnectRealtime = true`

---

## Models

```csharp
[Table("transactions")]
public class Transaction : BaseModel
{
    [PrimaryKey("id", false)] public Guid Id { get; set; }
    [Column("user_id")]       public Guid UserId { get; set; }
    [Column("account_id")]    public Guid AccountId { get; set; }
    [Column("category_id")]   public Guid? CategoryId { get; set; }
    [Column("amount")]        public decimal Amount { get; set; }
    [Column("type")]          public string Type { get; set; }
    [Column("description")]   public string Description { get; set; }
    [Column("note")]          public string? Note { get; set; }
    [Column("date")]          public DateTime Date { get; set; }
    [Column("created_at")]    public DateTime CreatedAt { get; set; }
    // non-DB nav properties (no [Column]):
    public Category? Category { get; set; }
    public bool GroupHeader { get; set; }
    public bool IsExpense => Type == "expense";
}

// Account has: Id, UserId, Name, Type, Institution, Mask, Currency, Balance,
//   OpeningBalance, CreditLimit, IsArchived, OpenedAt, CreatedAt
//   + non-DB: Color (string hex), Icon (string lucide name), CurrentBalance,
//     TransactionsCount, MonthlyIncrease, TotalIncomeThisMonth, TotalExpenseThisMonth,
//     IncomeTransactionsThisMonth, ExpenseTransactionsThisMonth, RecentTransactions

// Category has: Id, UserId, Name, Icon, Color, Type, CreatedAt

// Budget has: Id, UserId, CategoryId, LimitAmount (was "Amount"), Period, AlertThreshold,
//   Rollover, CreatedAt
//   + non-DB: Category, Spent, TransactionsCount, GroupHeader
//   + computed: IsOnTrack, IsWarning, IsOverBudget, PercentageUsed, RemainingFormatted,
//     SpentFormatted, AmountFormatted, PercentageFormatted

// Profile has: Id, DisplayName, AvatarUrl, Currency, Theme, Language,
//   SavingsGoal (decimal?), SavingsGoalDeadline (DateTime?), CreatedAt, UpdatedAt
```

---

## Design System

### Dark theme colors
```
BgBase: #0D0F14     BgSurface: #13161E    BgSidebar: #0B0D12
BgElevated: #0D0F14  BgHover: #1A1E2A
BorderSubtle: #1E2330  BorderAccent: #2A3050
TextPrimary: #F0F2F8   TextSecondary: #C8D0E8  TextMuted: #7A8090  TextDisabled: #5A6070
AccentBlue: #7B9CFF    AccentGreen: #2ECC8A    AccentYellow: #F5C842
AccentRed: #FF5E5E     AccentPurple: #9B7BFF   AccentOrange: #FF7E5E  AccentPink: #FF5E9B
IconBgBlue: #1A2240    IconBgGreen: #0D2A1A    IconBgOrange: #2A1A0D
IconBgRed: #2A0D0D     IconBgPurple: #1A1A2A   IconBgPink: #1A0D1A
BadgeBgRed, BadgeBgYellow (semi-transparent dark versions of accent colors)
```

### Light theme
Softened versions (~10% darker accents), `BgBase: #EDEEF4`, `BgSurface: #F5F6FA`

### Theme switching
`ThemeService.SwitchToTheme(string theme)` — "dark"/"light"/"system"
Profile stores theme preference. `IsDarkTheme` bool on `MainViewModel`.

### Dynamic resources used everywhere
```xml
{DynamicResource BgBase}, {DynamicResource AccentBlue}, {DynamicResource TextPrimary}
{DynamicResource RadiusControl}, {DynamicResource RadiusIcon}, {DynamicResource RadiusPill}
{DynamicResource FontSizeBody}, {DynamicResource FontSizeLabel}
```

### SVG icons (Lucide)
```xml
<Svg Path="../Assets/Icons/icon-name.svg" Width="16" Height="16" Css="{DynamicResource SvgBlue}"/>
```
SVG CSS resources: `SvgPrimary`, `SvgSecondary`, `SvgMuted`, `SvgDisabled`, `SvgBlue`, `SvgGreen`, `SvgYellow`, `SvgRed`, `SvgPurple`, `SvgOrange`, `SvgPink`
Icon bg opacity pattern: `<SolidColorBrush Color="{Binding Color, Converter=..., ConverterParameter=color}" Opacity="0.15"/>`

---

## Button / Style Classes

- `accented` — primary action (AccentBlue bg)
- `base` — secondary action
- `nav` — transparent nav/toggle, `Classes.active="{Binding bool}"` for active state
- `account` — account card button
- `ghost` — transparent TextBox (all states transparent, no border)
- `label` — uppercase muted label TextBlock
- `muted` — TextMuted foreground
- `badge-green` / `badge-red` / `badge-warning` — status badges
- `budget-card` / `budget-card-warning` / `budget-card-over` — budget card borders
- `mobile` — root class on mobile views (enables mobile style overrides)

ProgressBar: `Classes="green"` / `"yellow"` / `"red"` / `"blue"`

Mobile nav button override:
```xml
<Style Selector=".mobile Button.nav:pressed /template/ ContentPresenter"> Transparent </Style>
<Style Selector=".mobile Button.nav:pointerover /template/ ContentPresenter"> Transparent </Style>
```

---

## View Architecture

### Desktop shell (MainView.axaml)
```xml
<Grid ColumnDefinitions="220,*">
    <Border><!-- 220px sidebar with nav + user profile --></Border>
    <Grid Column="1">
        <ContentControl Content="{Binding CurrentView}"/>
        <!-- Transaction form modal overlay — IsVisible="{Binding IsTransactionFormVisible}" -->
        <!-- Delete account dialog overlay — IsVisible="{Binding IsDeleteAccountDialogVisible}" -->
    </Grid>
</Grid>
```
Views do NOT include sidebar. `Background="{DynamicResource BgBase}"` on root.
Typical layout: `Grid RowDefinitions="Auto,*"` — top bar + content.
Top bar margin: `Margin="32,28,32,0"`.

### Mobile shell (MainViewMobile.axaml)
```xml
<Grid Classes="mobile" RowDefinitions="*,Auto">
    <ContentControl Content="{Binding CurrentView}"/>
    <!-- Bottom tab bar: Home | Transactions | + FAB | Accounts | Budget -->
</Grid>
```
Mobile views: single column, `Margin="16,12,16,0"` top bar, no `BoxShadow`, no `MinWidth`/`MinHeight`.

### Views built so far
**Desktop:** DashboardView, TransactionsView, AccountsView, BudgetView, AuthView, MainView
**Mobile:** DashboardViewMobile, TransactionsViewMobile, AccountsViewMobile, BudgetViewMobile, MainViewMobile, AuthViewMobile (not yet built)
**Forms/Dialogs:** TransactionFormView (add/edit modal), DeleteAccountDialogView (two-step)
**Shared:** BudgetFormView, BudgetCardMenuView

---

## Key Converters

| Key | Purpose |
|-----|---------|
| `HexToColorConverter` | ConverterParameter: `color`→Color, `css`→SVG CSS, `brush`→IBrush |
| `AmountColorConverter` | type string → AccentRed or AccentGreen brush |
| `AmountSignConverter` | MultiBinding(amount, type) → `+$x.xx` / `-$x.xx` |
| `BoolToColorConverter` | ConverterParameter=`'#hex1\|#hex2'` → color |
| `BoolToCssConverter` | ConverterParameter=`'#hex1\|#hex2'` → SVG CSS string |
| `SvgPathFromName` | `"icon-name"` → `"../Assets/Icons/icon-name.svg"` |
| `DateFormatConverter` | DateTime → formatted string (en-US), ConverterParameter=format |
| `EqualValueConverter` | MultiBinding equality → bool |
| `DecimalColorConverter` | decimal sign → one of two values |
| `NetworthSumConverter` | MultiBinding(income, expenses) → net formatted |
| `PercentageConverter` | MultiBinding(value, total) → percentage string |
| `MaskToStringConverter` | account mask → `"•••• 1234"` |
| `FirstValueConverter` | `double[]` → first element |
| `SkPaintToBrushConverter` | SolidColorPaint → IBrush |
| `AccountFromIdConverter` | Guid → account name string |

---

## Custom Controls & Behaviors

### DateRangePicker (`Clario.CustomControls`)
```xml
<cc:DateRangePicker SelectionMode="SingleDate" SelectedDates="{Binding Dates}"/>
<cc:DateRangePicker SelectionMode="SingleRange" SelectedDates="{Binding Dates}"/>
<cc:DateRangePicker Classes="ghost" SelectionMode="SingleDate" SelectedDates="{Binding Dates}" Padding="12,10"/>
```
Has `SelectedDate` (DateTime?) property that syncs with `SelectedDates`.
Uses `_isSyncing` guard flag to prevent feedback loops.
Only processes `SelectedDatesChanged` when popup is open (fixes instant-close bug).

### NumericTextBox (`Clario.CustomControls`)
Subclass of TextBox, only accepts digits and one decimal point.
```xml
<local:NumericTextBox Classes="ghost" Text="{Binding Amount, Mode=TwoWay}"/>
```

### NumericInputBehavior (`Clario.Behaviors`)
Alternative to NumericTextBox, attach to regular TextBox.
```xml
<TextBox><Interaction.Behaviors><behaviors:NumericInputBehavior/></Interaction.Behaviors></TextBox>
```

---

## Transaction Form (TransactionFormViewModel)

- `IsEditMode`, `FormTitle`, `FormSubtitle`, `SaveButtonLabel` (switches add/edit)
- `SetupForAdd(categories, accounts)` / `SetupForEdit(transaction, categories, accounts)`
- `OnSaved` / `OnCancelled` / `OnDeleted` — Action callbacks
- Commands: `SetTypeCommand(string)`, `SetTodayCommand`, `SaveCommand`, `DeleteCommand`, `RequestDeleteCommand`, `CancelCommand`
- `ShowDeleteConfirm` bool — confirm delete sub-modal inside form
- Validation: `IsValid` bool, `HasError` + `ErrorMessage`

---

## TransactionsViewModel — Key Details

- `_allTransactions` (all), `_filteredTransactions` (after filters), `PagedTransactions` (ObservableCollection, current page)
- Page size: 25 desktop / 10 mobile
- `LoadPage(int)` — desktop pagination (clears and replaces)
- `LoadMoreCommand` — mobile infinite scroll (appends, does NOT call ApplyFilters)
- `GroupTransactions()` — inserts date header rows, uses `_isSyncing` guard, processes dates in reverse order to avoid index shifting
- `Transaction.GroupHeader = true` rows act as section headers with `Description` = date label
- Filters: SearchText, SelectedDateRangeOption, SelectedCategory, SelectedAccount, TransactionType, SelectedSortOption
- `ApplyFilters()` does NOT re-sort at end — sort is inside the switch
- `WeakReferenceMessenger.Default.Send(new TransactionsScrollToTop())` on desktop page change

---

## BudgetViewModel — Key Details

- `CurrentPeriod` DateTime — navigable by month
- `ProcessBudgets()` — sets `Spent`, `TransactionsCount` per budget, adds group headers to `VisibleBudgets`
- `ProcessChartData()` — builds `SpendingBreakdownChartSeries` (half-donut) + `SpendingBreakdownLegends`
- `TotalLeft` = `TotalBudgeted - TotalSpent` (clamped to 0)
- `SavingsHint` — compares `TotalLeft` to `Profile.SavingsGoal`
- `DailyBudgetLeftFormatted` = remaining budget ÷ days left in period

---

## Charts (LiveCharts2)

### Bar chart (SpendingByCategory on Dashboard)
```csharp
// ISeries[] — one ColumnSeries<double> per category
// Replace whole array on update (don't use ObservableCollection + SeriesSource)
// ZoomMode="None" on CartesianChart to allow scroll passthrough
// Scroll forwarding in code-behind via PointerWheelChangedEvent tunnel handler
```

### Half-donut (Budget spending breakdown)
```xml
<Border Height="150" ClipToBounds="True">
    <lvc:PieChart Series="{Binding ...}" Height="300" Margin="0,0,0,-150"
                  InitialRotation="-180" MaxAngle="180" LegendPosition="Hidden"/>
</Border>
```

### TooltipLabelFormatter
```csharp
TooltipLabelFormatter = point => $"${point.Coordinate.PrimaryValue:N0}"
```

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
`TransparentFlyoutPresenter` ControlTheme defined in `App.axaml` resources (not Styles).

## Modal Pattern (desktop)
Full-screen Grid overlay on top of ContentControl in MainView.
`IsVisible` bound to bool on MainViewModel.
Dim: `<Border Background="#70000000"/>` + centered card.

## Bottom Sheet Pattern (mobile — AccountsViewMobile)
```csharp
// public async Task ShowSheet() / HideSheet()
// TranslateTransform animation, CubicEaseOut 320ms up, CubicEaseIn 260ms down
// OverlayGrid.IsVisible = false by default in XAML
// Dismiss: DimOverlay.PointerPressed + CloseButton.Click
// Sheet height: BottomSheet.MaxHeight = Bounds.Height * 0.82 in OnAttachedToVisualTree
```

---

## XAML Rules

- Always `{DynamicResource}` for theme colors
- Never hardcode hex except in SVG CSS strings
- `x:CompileBindings="False"` on shell views with dynamic DataContext
- Separator between list items: `Spacing="1"` on StackPanel + `BorderSubtle` background on container
- Never `MinWidth`/`MinHeight` on mobile UserControls
- Icon bg always: `<SolidColorBrush Color="..." Opacity="0.15"/>`
- No `BoxShadow` on mobile

---

## Performance Notes

- Build in Release mode for realistic Android performance
- Use `VirtualizingStackPanel` in ItemsControl for long lists
- Mobile page size = 10
- Remove `BoxShadow` from all mobile views
- `base.OnFrameworkInitializationCompleted()` must be called FIRST in `OnFrameworkInitializationCompleted`

---

## Things Currently In Progress / Not Yet Done

- AuthViewMobile (not yet built)
- Settings view (desktop + mobile, not yet designed)
- Analytics view (not yet designed)
- Edit account form (not yet built)
- Edit budget form (BudgetFormView exists but not wired to data)
- Light theme AppTheme variant file (not created yet)
- Notification/alert system for budget threshold warnings
- Real-time Supabase subscriptions (AutoConnectRealtime=true but not wired to UI refresh)

---

## App Identity

- **Name:** Clario
- **Logo:** C-shaped segmented donut chart, 4 segments, blue/green/yellow/red gradient, on dark background
- **Tagline:** "Your personal finance tracker"
- **Package:** `com.CompanyName.Clario` (placeholder, not finalized)
