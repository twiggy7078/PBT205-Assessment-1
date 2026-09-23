namespace Pbt205.Trading;

/// <summary>Validation shared by the trading apps.</summary>
public static class TradingNames
{
    public const string DefaultSymbol = "XYZ";
    public const int FixedQuantity = 100;
    public const int MaxSymbolLength = 10;

    /// <summary>
    /// Symbols become RabbitMQ routing keys, so "." "*" and "#" are not allowed.
    /// </summary>
    /// <returns>An error message, or null if the symbol is fine.</returns>
    public static string? ValidateSymbol(string? symbol)
    {
        if (string.IsNullOrWhiteSpace(symbol))
        {
            return "Stock symbol can't be empty.";
        }
        if (symbol.Length > MaxSymbolLength)
        {
            return $"Stock symbol must be {MaxSymbolLength} characters or fewer.";
        }
        return symbol.All(char.IsAsciiLetterOrDigit) ? null : "Stock symbols can only use letters and digits.";
    }

    /// <summary>Symbols are case-insensitive: "xyz" and "XYZ" are the same stock.</summary>
    public static string NormaliseSymbol(string symbol) => symbol.Trim().ToUpperInvariant();

    public static string? ValidateUsername(string? username) =>
        string.IsNullOrWhiteSpace(username) ? "Username can't be empty." :
        username.Length > 32 ? "Username must be 32 characters or fewer." :
        username.Any(char.IsControl) ? "Username contains invalid characters." : null;
}
