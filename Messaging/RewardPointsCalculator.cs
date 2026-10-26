namespace Mango.Services.RewardAPI.Messaging;

public static class RewardPointsCalculator
{
    public static int Calculate(double eligibleAmount, string currency)
    {
        if (!string.Equals(currency, "INR", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Only INR orders are eligible for rewards.");

        if (!double.IsFinite(eligibleAmount) || eligibleAmount < 0)
            throw new InvalidDataException("Order amount must be a finite, non-negative value.");

        decimal amount;
        try
        {
            amount = (decimal)eligibleAmount;
        }
        catch (OverflowException ex)
        {
            throw new InvalidDataException("Order amount is outside the supported range.", ex);
        }

        var points = decimal.Floor(amount / 50m);
        if (points > int.MaxValue)
            throw new InvalidDataException("Calculated reward points exceed the supported range.");

        return (int)points;
    }
}
