namespace COSGeniusSystem.Services.Herbalist;
public static class HerbalistWorkflowRules
{
    public static bool SupportsStockUnit(string? unit) => unit is "kg" or "g";
    public static decimal StockToGrams(decimal quantity, string unit) => unit switch
    {
        "kg" => quantity * 1000m,
        "g" => quantity,
        _ => throw new ArgumentException("Unsupported stock unit.", nameof(unit))
    };
    public static decimal GramsToStock(decimal grams, string unit) => unit switch
    {
        "kg" => grams / 1000m,
        "g" => grams,
        _ => throw new ArgumentException("Unsupported stock unit.", nameof(unit))
    };
    // Unresolved surplus cannot be allocated; NULL and checked zero both provide no usable supply.
    public static decimal UsableReceipt(decimal issuedGrams, decimal? receivedGrams)
        => receivedGrams.HasValue ? Math.Min(issuedGrams, receivedGrams.Value) : 0m;
    public static bool FitsReceipt(decimal issuedGrams, decimal? receivedGrams, decimal alreadyAllocatedGrams, decimal requestedGrams)
        => requestedGrams > 0m && alreadyAllocatedGrams >= 0m
           && alreadyAllocatedGrams + requestedGrams <= UsableReceipt(issuedGrams, receivedGrams);
    public static decimal FefoAvailableGrams(decimal stockKg)
        => FefoAvailableGrams(stockKg, "kg");
    public static decimal FefoAvailableGrams(decimal stock, string unit)
        => Math.Max(0m, decimal.Floor(StockToGrams(stock, unit) * 100m) / 100m);
}
