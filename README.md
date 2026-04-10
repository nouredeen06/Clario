<div align="center">

<img src="./Clario/Assets/logo-no-bg.png" alt="Clario Logo" width="80" />

# Clario

**A clean, fast personal finance tracker for desktop and mobile.**

[![Beta](https://img.shields.io/badge/status-beta-f59e0b?style=flat-square)](https://github.com/yourusername/clario)
[![Built with Avalonia](https://img.shields.io/badge/built%20with-Avalonia%20UI-8b5cf6?style=flat-square)](https://avaloniaui.net/)
[![.NET](https://img.shields.io/badge/.NET-8.0-512bd4?style=flat-square)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/badge/license-MIT-22c55e?style=flat-square)](./LICENSE)

</div>

---

> ⚠️ **Clario is currently in beta.** Expect rough edges. Feedback and bug reports are welcome.

---

## Overview

Clario is a native app for tracking personal finances — expenses, income, and budgets — without the clutter of a browser tab. Built with [Avalonia UI](https://avaloniaui.net/), it runs natively on Windows, macOS, Linux, and Android.

---

## Screenshots

> _Screenshots coming soon. The UI is still being polished._

<!-- Uncomment and replace when ready:
![Dashboard](./Assets/Screenshots/dashboard.png)
![Transactions](./Assets/Screenshots/transactions.png)
![Analytics](./Assets/Screenshots/analytics.png)
![Budget](./Assets/Screenshots/budget.png)
-->

---

## Features

- **Expense & income tracking** — Log transactions with categories, amounts, dates, and notes
- **Date range filtering** — Quickly slice your data by day, week, month, or custom range
- **Categories** — Organize transactions with custom categories and icons
- **Budget goals** — Set spending limits per category with period tracking
- **Analytics** — 6 chart sections covering spending trends, category breakdowns, and more
- **Multi-account support** — Track balances across multiple accounts
- **Multi-currency** — Accounts in different currencies
- **Multiple themes** — Dark, Light, Catppuccin Latte, Macchiato, and Mocha
- **Cross-platform** — Runs natively on Windows, macOS, Linux, and Android
- **Real-time sync** — Powered by Supabase with live data updates

---

## Tech Stack

| Layer | Technology |
|---|---|
| UI Framework | [Avalonia UI 11](https://avaloniaui.net/) |
| Language | C# / .NET 8 |
| Architecture | MVVM (CommunityToolkit.MVVM) |
| Backend | [Supabase](https://supabase.com/) (PostgreSQL, Auth, Realtime) |
| Charts | [LiveCharts2](https://livecharts.dev/) (SkiaSharp) |
| Icons | [Lucide](https://lucide.dev/) |

---

## Getting Started

Download the latest release for your platform from the [Releases](https://github.com/yourusername/clario/releases) tab and run the installer.

If you'd prefer to run from source:

```bash
git clone https://github.com/yourusername/clario.git
cd clario
dotnet run --project Clario.Desktop
```

> Requires the [.NET 8 SDK](https://dotnet.microsoft.com/download) when running from source.

---

## Project Status

Clario is in active development. Core features are working:

- [x] Transaction entry & editing
- [x] Category management
- [x] Date range picker
- [x] Transaction list with filtering, search & sorting
- [x] Budget goals with period navigation
- [x] Analytics (charts & spending reports)
- [x] Multi-account management
- [x] Multi-currency support
- [x] Settings (profile, theme, currency, savings goal)
- [x] Android support
- [x] Multiple themes (Dark, Light, Catppuccin variants)
- [ ] Budget goal notifications
- [ ] CSV import / export
- [ ] Recurring transactions

---

## Contributing

The project isn't formally open to contributions yet while the core is still being shaped, but feel free to open an issue if you find a bug or have a suggestion.

---

<div align="center">

Made with ☕ and Avalonia UI.

</div>