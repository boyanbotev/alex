// Inline icons work in both world-space labels and canvas text.
public static class CurrencyUI
{
    public const string Icon = "<sprite=\"Currency\" index=0>";
    public static string Amount(int amount) => $"{Icon} {amount}";
    public static string Refund(int amount) => $"{Icon} +{amount}";
}
