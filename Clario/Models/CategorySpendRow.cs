namespace Clario.Models;

public class CategorySpendRow
{
    public string Name { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public string Color { get; set; } = "#7B9CFF";
    public decimal Amount { get; set; }
    public double Percentage { get; set; }
    public string AmountFormatted { get; set; } = string.Empty;
    public string PercentageFormatted => $"{Percentage:F1}%";
}
