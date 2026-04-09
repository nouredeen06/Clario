using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using Clario.Data;
using Clario.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Clario.Services;

public static class PdfExportService
{
    //  Print palette (readable on white paper) 
    private const string TextPrimary   = "#111827";
    private const string TextSecondary = "#374151";
    private const string TextMuted     = "#6B7280";
    private const string Border        = "#E5E7EB";
    private const string HeaderBg      = "#F3F4F6";
    private const string IncomeColor   = "#15803D";
    private const string ExpenseColor  = "#B91C1C";
    private const string BlueColor     = "#1D4ED8";
    private const string AccentBar     = "#1D4ED8"; // header rule

    static PdfExportService()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public static async Task<string?> ExportAsync(
        GeneralDataRepo data,
        DateTime start,
        DateTime end,
        string periodLabel,
        List<CategorySpendRow> topCategories)
    {
        var culture = new CultureInfo("en-US");
        var sym = CurrencyService.GetSymbol(data.PrimaryAccount?.Currency ?? data.Profile?.Currency ?? "USD");
        var userName = data.Profile?.DisplayName ?? "User";

        var periodTxs = data.Transactions
            .Where(t => !t.IsTransfer && t.Date.Date >= start.Date && t.Date.Date <= end.Date)
            .OrderByDescending(t => t.Date)
            .ToList();

        var totalIncome   = periodTxs.Where(t => t.Type == "income").Sum(t => t.ConvertedAmount);
        var totalExpenses = periodTxs.Where(t => t.Type == "expense").Sum(t => t.ConvertedAmount);
        var net           = totalIncome - totalExpenses;
        var savingsRate   = totalIncome > 0 ? net / totalIncome * 100 : 0;
        var subtitle      = $"{start.ToString("MMM d, yyyy", culture)} – {end.ToString("MMM d, yyyy", culture)}";
        var generatedAt   = DateTime.Now.ToString("MMM d, yyyy 'at' h:mm tt", culture);

        // Load logo on the calling (UI) thread before entering Task.Run
        byte[] logoBytes;
        using (var assetStream = AssetLoader.Open(new Uri("avares://Clario/Assets/Logo/logo-combined-primary-transparent-384x128.png")))
        using (var ms = new MemoryStream())
        {
            await assetStream.CopyToAsync(ms);
            logoBytes = ms.ToArray();
        }

        byte[] pdfBytes = [];

        await Task.Run(() =>
        {
            pdfBytes = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.MarginHorizontal(1.8f, Unit.Centimetre);
                    page.MarginVertical(1.5f, Unit.Centimetre);
                    page.DefaultTextStyle(s => s.FontSize(10).FontColor(TextPrimary).FontFamily("Arial"));

                    //  Header 
                    page.Header().Column(col =>
                    {
                        col.Item().Row(row =>
                        {
                            row.RelativeItem().Column(left =>
                            {
                                left.Item().Height(32).Image(logoBytes).FitHeight();
                                left.Item().PaddingTop(4).Text($"Financial Report — {periodLabel}")
                                    .FontSize(12).SemiBold().FontColor(TextSecondary);
                                left.Item().Text(subtitle).FontSize(9).FontColor(TextMuted);
                            });
                            row.ConstantItem(140).AlignRight().Column(right =>
                            {
                                right.Item().AlignRight().Text(userName)
                                    .FontSize(10).SemiBold().FontColor(TextPrimary);
                                right.Item().AlignRight().Text($"Generated {generatedAt}")
                                    .FontSize(8).FontColor(TextMuted);
                            });
                        });
                        col.Item().PaddingTop(10).LineHorizontal(2).LineColor(AccentBar);
                    });

                    //  Content 
                    page.Content().PaddingTop(18).Column(col =>
                    {
                        //  KPI cards 
                        col.Item().Text("Summary").FontSize(11).SemiBold().FontColor(TextPrimary);
                        col.Item().PaddingTop(6).Table(table =>
                        {
                            table.ColumnsDefinition(c =>
                            {
                                c.RelativeColumn();
                                c.RelativeColumn();
                                c.RelativeColumn();
                                c.RelativeColumn();
                            });

                            void KpiCell(string label, string value, string valueColor)
                            {
                                table.Cell()
                                    .Border(1).BorderColor(Border)
                                    .Background(HeaderBg)
                                    .Padding(12)
                                    .Column(c =>
                                    {
                                        c.Item().Text(label).FontSize(8).FontColor(TextMuted);
                                        c.Item().PaddingTop(4).Text(value)
                                            .FontSize(14).Bold().FontColor(valueColor);
                                    });
                            }

                            KpiCell("TOTAL INCOME",   $"{sym}{totalIncome:N2}",  IncomeColor);
                            KpiCell("TOTAL EXPENSES", $"{sym}{totalExpenses:N2}", ExpenseColor);
                            KpiCell("NET SAVINGS",    $"{(net >= 0 ? "+" : "")}{sym}{net:N2}",
                                net >= 0 ? IncomeColor : ExpenseColor);
                            KpiCell("SAVINGS RATE",   totalIncome > 0 ? $"{savingsRate:F1}%" : "—", BlueColor);
                        });

                        col.Item().Height(20);

                        //  Top categories 
                        if (topCategories.Count > 0)
                        {
                            col.Item().Text("Top Spending Categories")
                                .FontSize(11).SemiBold().FontColor(TextPrimary);
                            col.Item().PaddingTop(6).Table(table =>
                            {
                                table.ColumnsDefinition(c =>
                                {
                                    c.RelativeColumn(4);
                                    c.RelativeColumn(2);
                                    c.RelativeColumn(1);
                                });

                                // Header row — CATEGORY stays left, rest centered
                                void TH(string text, bool leftAlign = false)
                                {
                                    var cell = table.Cell().Background(HeaderBg)
                                        .PaddingHorizontal(8).PaddingVertical(7)
                                        .BorderBottom(1).BorderColor(Border);
                                    if (leftAlign)
                                        cell.Text(text).FontSize(8).SemiBold().FontColor(TextMuted);
                                    else
                                        cell.AlignCenter().Text(text).FontSize(8).SemiBold().FontColor(TextMuted);
                                }

                                TH("CATEGORY", leftAlign: true); TH("AMOUNT"); TH("SHARE");

                                foreach (var cat in topCategories)
                                {
                                    table.Cell().PaddingHorizontal(8).PaddingVertical(7)
                                        .BorderBottom(1).BorderColor(Border)
                                        .Text(cat.Name).FontSize(10).FontColor(TextPrimary);

                                    table.Cell().PaddingHorizontal(8).PaddingVertical(7)
                                        .BorderBottom(1).BorderColor(Border)
                                        .Text(cat.AmountFormatted).FontSize(10).FontColor(TextSecondary);

                                    table.Cell().PaddingHorizontal(8).PaddingVertical(7)
                                        .BorderBottom(1).BorderColor(Border)
                                        .AlignRight()
                                        .Text(cat.PercentageFormatted).FontSize(10).FontColor(TextMuted);
                                }
                            });

                            col.Item().Height(20);
                        }

                        //  Transactions 
                        col.Item().Text($"Transactions ({periodTxs.Count})")
                            .FontSize(11).SemiBold().FontColor(TextPrimary);
                        col.Item().PaddingTop(6).Table(table =>
                        {
                            table.ColumnsDefinition(c =>
                            {
                                c.ConstantColumn(60);  // date
                                c.RelativeColumn(3);   // description
                                c.RelativeColumn(2);   // category
                                c.RelativeColumn(2);   // account
                                c.RelativeColumn(2);   // amount
                            });

                            // Header — DESCRIPTION stays left, all others centered
                            void TH(string text, bool leftAlign = false)
                            {
                                var cell = table.Cell().Background(HeaderBg)
                                    .PaddingHorizontal(6).PaddingVertical(7)
                                    .BorderBottom(2).BorderColor(Border);
                                if (leftAlign)
                                    cell.Text(text).FontSize(8).SemiBold().FontColor(TextMuted);
                                else
                                    cell.AlignCenter().Text(text).FontSize(8).SemiBold().FontColor(TextMuted);
                            }

                            TH("DATE"); TH("DESCRIPTION", leftAlign: true); TH("CATEGORY"); TH("ACCOUNT"); TH("AMOUNT");

                            foreach (var tx in periodTxs)
                            {
                                var amountStr   = tx.Type == "income"
                                    ? $"+{sym}{tx.ConvertedAmount:N2}"
                                    : $"-{sym}{tx.ConvertedAmount:N2}";
                                var amountColor = tx.Type == "income" ? IncomeColor : ExpenseColor;

                                void TD(string text, string color = TextPrimary, bool rightAlign = false)
                                {
                                    var cell = table.Cell()
                                        .PaddingHorizontal(6).PaddingVertical(6)
                                        .BorderBottom(1).BorderColor(Border);
                                    if (rightAlign)
                                        cell.AlignRight().Text(text).FontSize(9).FontColor(color);
                                    else
                                        cell.Text(text).FontSize(9).FontColor(color);
                                }

                                TD(tx.Date.ToString("MMM d, yy", culture), TextMuted);
                                TD(tx.Description);
                                TD(tx.Category?.Name ?? "—", TextSecondary);
                                TD(tx.AccountDisplayText, TextSecondary);
                                TD(amountStr, amountColor, rightAlign: true);
                            }
                        });
                    });

                    //  Footer 
                    page.Footer().PaddingTop(6).BorderTop(1).BorderColor(Border).Row(row =>
                    {
                        row.RelativeItem().Text("Generated by Clario — Your personal finance tracker")
                            .FontSize(8).FontColor(TextMuted);
                        row.ConstantItem(60).AlignRight().Text(t =>
                        {
                            t.AlignRight();
                            t.Span("Page ").FontSize(8).FontColor(TextMuted);
                            t.CurrentPageNumber().FontSize(8).FontColor(TextMuted);
                            t.Span(" of ").FontSize(8).FontColor(TextMuted);
                            t.TotalPages().FontSize(8).FontColor(TextMuted);
                        });
                    });
                });
            }).GeneratePdf();
        });

        return await SavePdfAsync(pdfBytes, $"Clario_Report_{DateTime.Now:yyyy-MM-dd}.pdf");
    }

    private static async Task<string?> SavePdfAsync(byte[] pdfBytes, string suggestedName)
    {
        var topLevel = GetTopLevel();
        if (topLevel is null)
        {
            var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), suggestedName);
            await File.WriteAllBytesAsync(path, pdfBytes);
            return path;
        }

        var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save PDF Report",
            SuggestedFileName = suggestedName,
            FileTypeChoices = [new FilePickerFileType("PDF Document") { Patterns = ["*.pdf"] }]
        });

        if (file is null) return null;

        await using var stream = await file.OpenWriteAsync();
        await stream.WriteAsync(pdfBytes);
        return file.Path.LocalPath;
    }

    private static TopLevel? GetTopLevel()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            return TopLevel.GetTopLevel(desktop.MainWindow);
        if (Application.Current?.ApplicationLifetime is ISingleViewApplicationLifetime single)
            return TopLevel.GetTopLevel(single.MainView as Visual);
        return null;
    }
}
