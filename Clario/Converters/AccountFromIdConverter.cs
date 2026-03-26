using System;
using System.Globalization;
using System.Linq;
using Avalonia.Data.Converters;
using Clario.Data;

namespace Clario.Converters;

public class AccountFromIdConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not Guid) return null;
        var accounts = DataRepo.General.FetchAccounts().Result;
        if (accounts is null) return null;
        return accounts.FirstOrDefault(x => x.Id == (Guid)value)?.Name;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}