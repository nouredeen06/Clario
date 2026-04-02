using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace Clario.Services;

public static class CurrencyService
{
    private static readonly HttpClient _http = new();

    public static readonly IReadOnlyDictionary<string, string> CurrencyNames =
        new Dictionary<string, string>
        {
            // Major
            { "USD", "US Dollar" },
            { "EUR", "Euro" },
            { "GBP", "British Pound" },
            { "JPY", "Japanese Yen" },
            { "CHF", "Swiss Franc" },
            { "CAD", "Canadian Dollar" },
            { "AUD", "Australian Dollar" },
            { "NZD", "New Zealand Dollar" },
            // Asia-Pacific
            { "CNY", "Chinese Yuan" },
            { "HKD", "Hong Kong Dollar" },
            { "SGD", "Singapore Dollar" },
            { "KRW", "South Korean Won" },
            { "TWD", "Taiwan Dollar" },
            { "INR", "Indian Rupee" },
            { "PKR", "Pakistani Rupee" },
            { "BDT", "Bangladeshi Taka" },
            { "LKR", "Sri Lankan Rupee" },
            { "NPR", "Nepalese Rupee" },
            { "MMK", "Myanmar Kyat" },
            { "THB", "Thai Baht" },
            { "MYR", "Malaysian Ringgit" },
            { "IDR", "Indonesian Rupiah" },
            { "PHP", "Philippine Peso" },
            { "VND", "Vietnamese Dong" },
            { "KHR", "Cambodian Riel" },
            { "LAK", "Lao Kip" },
            { "MNT", "Mongolian Tögrög" },
            { "AFN", "Afghan Afghani" },
            { "BND", "Brunei Dollar" },
            { "MOP", "Macanese Pataca" },
            // Middle East
            { "AED", "UAE Dirham" },
            { "SAR", "Saudi Riyal" },
            { "QAR", "Qatari Riyal" },
            { "KWD", "Kuwaiti Dinar" },
            { "BHD", "Bahraini Dinar" },
            { "OMR", "Omani Rial" },
            { "JOD", "Jordanian Dinar" },
            { "ILS", "Israeli Shekel" },
            { "IQD", "Iraqi Dinar" },
            { "YER", "Yemeni Rial" },
            { "LBP", "Lebanese Pound" },
            // Africa
            { "EGP", "Egyptian Pound" },
            { "MAD", "Moroccan Dirham" },
            { "TND", "Tunisian Dinar" },
            { "DZD", "Algerian Dinar" },
            { "LYD", "Libyan Dinar" },
            { "NGN", "Nigerian Naira" },
            { "GHS", "Ghanaian Cedi" },
            { "KES", "Kenyan Shilling" },
            { "UGX", "Ugandan Shilling" },
            { "TZS", "Tanzanian Shilling" },
            { "ETB", "Ethiopian Birr" },
            { "ZAR", "South African Rand" },
            { "ZMW", "Zambian Kwacha" },
            { "BWP", "Botswana Pula" },
            { "MZN", "Mozambican Metical" },
            { "AOA", "Angolan Kwanza" },
            { "XOF", "West African CFA Franc" },
            { "XAF", "Central African CFA Franc" },
            { "MUR", "Mauritian Rupee" },
            { "RWF", "Rwandan Franc" },
            { "SDG", "Sudanese Pound" },
            { "MGA", "Malagasy Ariary" },
            // Europe (non-EUR)
            { "SEK", "Swedish Krona" },
            { "NOK", "Norwegian Krone" },
            { "DKK", "Danish Krone" },
            { "ISK", "Icelandic Króna" },
            { "PLN", "Polish Złoty" },
            { "CZK", "Czech Koruna" },
            { "HUF", "Hungarian Forint" },
            { "RON", "Romanian Leu" },
            { "BGN", "Bulgarian Lev" },
            { "HRK", "Croatian Kuna" },
            { "RSD", "Serbian Dinar" },
            { "ALL", "Albanian Lek" },
            { "MKD", "Macedonian Denar" },
            { "BAM", "Bosnian Mark" },
            { "MDL", "Moldovan Leu" },
            { "UAH", "Ukrainian Hryvnia" },
            { "BYN", "Belarusian Ruble" },
            { "RUB", "Russian Ruble" },
            { "TRY", "Turkish Lira" },
            // Caucasus & Central Asia
            { "GEL", "Georgian Lari" },
            { "AMD", "Armenian Dram" },
            { "AZN", "Azerbaijani Manat" },
            { "KZT", "Kazakhstani Tenge" },
            { "UZS", "Uzbekistani Som" },
            { "TJS", "Tajikistani Somoni" },
            { "TMT", "Turkmenistani Manat" },
            { "KGS", "Kyrgyzstani Som" },
            // Americas
            { "MXN", "Mexican Peso" },
            { "BRL", "Brazilian Real" },
            { "ARS", "Argentine Peso" },
            { "CLP", "Chilean Peso" },
            { "COP", "Colombian Peso" },
            { "PEN", "Peruvian Sol" },
            { "BOB", "Bolivian Boliviano" },
            { "PYG", "Paraguayan Guaraní" },
            { "UYU", "Uruguayan Peso" },
            { "VES", "Venezuelan Bolívar" },
            { "GTQ", "Guatemalan Quetzal" },
            { "HNL", "Honduran Lempira" },
            { "NIO", "Nicaraguan Córdoba" },
            { "CRC", "Costa Rican Colón" },
            { "PAB", "Panamanian Balboa" },
            { "DOP", "Dominican Peso" },
            { "JMD", "Jamaican Dollar" },
            { "TTD", "Trinidad & Tobago Dollar" },
            { "BSD", "Bahamian Dollar" },
            { "CUP", "Cuban Peso" },
            { "HTG", "Haitian Gourde" },
            { "XCD", "Eastern Caribbean Dollar" },
            { "BBD", "Barbadian Dollar" },
            { "GYD", "Guyanese Dollar" },
            { "SRD", "Surinamese Dollar" },
        };

    public static IReadOnlyList<string> AvailableCurrencies { get; } =
        new List<string>(CurrencyNames.Keys);

    /// <summary>
    /// Maps each account currency → rate to convert 1 unit to the current primary currency.
    /// Populated on startup and refreshed whenever the primary currency changes.
    /// </summary>
    public static Dictionary<string, decimal> LiveRates { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Fetches fresh exchange rates for every currency in <paramref name="accountCurrencies"/>
    /// relative to <paramref name="primaryCurrency"/> and stores them in <see cref="LiveRates"/>.
    /// </summary>
    public static async Task RefreshLiveRatesAsync(string primaryCurrency, IEnumerable<string> accountCurrencies)
    {
        var currencies = accountCurrencies.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var tasks = currencies.Select(async currency =>
        {
            if (currency.Equals(primaryCurrency, StringComparison.OrdinalIgnoreCase))
            {
                LiveRates[currency] = 1m;
                return;
            }
            var rate = await GetExchangeRateAsync(currency, primaryCurrency);
            if (rate.HasValue) LiveRates[currency] = rate.Value;
        });
        await Task.WhenAll(tasks);
    }

    /// <summary>
    /// Fetches the live exchange rate from <paramref name="from"/> to <paramref name="to"/>
    /// using the Frankfurter API. Returns null on failure.
    /// </summary>
    public static async Task<decimal?> GetExchangeRateAsync(string from, string to)
    {
        try
        {
            if (from.Equals(to, StringComparison.OrdinalIgnoreCase)) return 1m;
            var url = $"https://api.frankfurter.dev/v2/rates?base={from.ToUpper()}&quotes={to.ToUpper()}";
            var json = await _http.GetStringAsync(url);
            var arr = JArray.Parse(json);
            var rate = arr[0]?["rate"]?.Value<decimal>();
            return Math.Round(rate ?? 1, 2);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Returns the display symbol for a given ISO currency code.</summary>
    public static string GetSymbol(string? code) => code?.ToUpper() switch
    {
        "USD" => "$",
        "CAD" => "CA$",
        "AUD" => "A$",
        "NZD" => "NZ$",
        "HKD" => "HK$",
        "SGD" => "S$",
        "BSD" => "B$",
        "BND" => "B$",
        "BBD" => "Bds$",
        "EUR" => "€",
        "GBP" => "£",
        "EGP" => "E£",
        "LBP" => "L£",
        "SYP" => "S£",
        "JPY" => "¥",
        "CNY" => "¥",
        "CHF" => "Fr",
        "SEK" => "kr",
        "NOK" => "kr",
        "DKK" => "kr",
        "ISK" => "kr",
        "INR" => "₹",
        "NPR" => "₨",
        "PKR" => "₨",
        "LKR" => "₨",
        "MUR" => "₨",
        "SCR" => "₨",
        "BRL" => "R$",
        "RUB" => "₽",
        "KRW" => "₩",
        "TRY" => "₺",
        "ILS" => "₪",
        "UAH" => "₴",
        "KZT" => "₸",
        "MNT" => "₮",
        "THB" => "฿",
        "VND" => "₫",
        "PHP" => "₱",
        "IDR" => "Rp",
        "MYR" => "RM",
        "KWD" => "KD",
        "BHD" => "BD",
        "OMR" => "OMR",
        "JOD" => "JD",
        "SAR" => "SR",
        "AED" => "AED",
        "QAR" => "QR",
        "IQD" => "IQD",
        "YER" => "YR",
        "IRR" => "﷼",
        "HUF" => "Ft",
        "CZK" => "Kč",
        "PLN" => "zł",
        "RON" => "lei",
        "BGN" => "лв",
        "HRK" => "kn",
        "RSD" => "din",
        "GEL" => "₾",
        "AMD" => "֏",
        "AZN" => "₼",
        "AFN" => "؋",
        "NGN" => "₦",
        "GHS" => "₵",
        "ZAR" => "R",
        "KES" => "Ksh",
        "UGX" => "USh",
        "TZS" => "TSh",
        "ETB" => "Br",
        "MAD" => "MAD",
        "DZD" => "DA",
        "TND" => "DT",
        "XOF" => "CFA",
        "XAF" => "FCFA",
        "MXN" => "MX$",
        "ARS" => "AR$",
        "CLP" => "CL$",
        "COP" => "CO$",
        "PEN" => "S/",
        "BOB" => "Bs",
        "PYG" => "₲",
        "UYU" => "$U",
        "VES" => "Bs.S",
        "GTQ" => "Q",
        "HNL" => "L",
        "NIO" => "C$",
        "CRC" => "₡",
        "DOP" => "RD$",
        "JMD" => "J$",
        "TTD" => "TT$",
        "HTG" => "G",
        "GYD" => "G$",
        _ => code ?? "?"
    };
}
