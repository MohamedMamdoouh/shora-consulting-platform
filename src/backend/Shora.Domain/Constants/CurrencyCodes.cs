namespace Shora.Domain.Constants;

public static class CurrencyCodes
{
    public const string Egp = "EGP";

    public static string DisplayLabel(string currency) =>
        currency.ToUpperInvariant() switch
        {
            Egp => "جنيه",
            _ => currency
        };
}
