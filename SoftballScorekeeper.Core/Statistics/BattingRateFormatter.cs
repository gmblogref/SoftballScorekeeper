using System.Globalization;

namespace SoftballScorekeeper.Core.Statistics;

public static class BattingRateFormatter
{
    public static string Format(decimal rate)
    {
        if (rate < 0m)
            throw new ArgumentOutOfRangeException(nameof(rate),
                "A batting rate cannot be negative.");

        var rounded = decimal.Round(rate, 3, MidpointRounding.AwayFromZero);
        var formatted = rounded.ToString("0.000", CultureInfo.InvariantCulture);
        return rounded < 1m ? formatted[1..] : formatted;
    }
}
