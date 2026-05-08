using System.Text.Json;

namespace FilmAPI.Services;

public static class SeatPricingUtils
{
    public const decimal VipSupplement = 2.00m;

    public static HashSet<string> GetVipSeats(int rows, int cols, string? rawSeatMapJson)
    {
        var safeRows = Math.Clamp(rows, 1, 26);
        var safeCols = Math.Clamp(cols, 1, 50);
        var (aisleStart, aisleEnd) = ResolveCenterAisle(safeCols);
        var validSeats = ParseValidSeats(rawSeatMapJson, safeRows, safeCols);

        var vipSeats = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var seat in validSeats)
        {
            if (!TryParseSeatCode(seat, out var rowIndex, out var colIndex))
            {
                continue;
            }

            if (IsVipSeat(rowIndex, colIndex, safeRows, safeCols, aisleStart, aisleEnd))
            {
                vipSeats.Add(seat);
            }
        }

        return vipSeats;
    }

    public static decimal CalculateTotal(decimal basePrice, IEnumerable<string> requestedSeats, HashSet<string> vipSeats)
    {
        var seats = requestedSeats
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim().ToUpperInvariant())
            .Distinct()
            .ToList();

        var vipCount = seats.Count(seat => vipSeats.Contains(seat));
        var total = (basePrice * seats.Count) + (VipSupplement * vipCount);
        return decimal.Round(total, 2);
    }

    private static HashSet<string> ParseValidSeats(string? rawSeatMapJson, int rows, int cols)
    {
        if (string.IsNullOrWhiteSpace(rawSeatMapJson))
        {
            return BuildDefaultSeatMap(rows, cols);
        }

        try
        {
            using var doc = JsonDocument.Parse(rawSeatMapJson);
            if (!doc.RootElement.TryGetProperty("seats", out var seatsElement) || seatsElement.ValueKind != JsonValueKind.Array)
            {
                return BuildDefaultSeatMap(rows, cols);
            }

            var seats = seatsElement
                .EnumerateArray()
                .Where(x => x.ValueKind == JsonValueKind.String)
                .Select(x => (x.GetString() ?? string.Empty).Trim().ToUpperInvariant())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            return seats.Count > 0 ? seats : BuildDefaultSeatMap(rows, cols);
        }
        catch
        {
            return BuildDefaultSeatMap(rows, cols);
        }
    }

    private static HashSet<string> BuildDefaultSeatMap(int rows, int cols)
    {
        var list = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var r = 0; r < rows; r++)
        {
            var rowCode = ((char)('A' + r)).ToString();
            for (var c = 1; c <= cols; c++)
            {
                list.Add($"{rowCode}{c}");
            }
        }
        return list;
    }

    private static bool IsVipSeat(int rowIndex, int colIndex, int rows, int cols, int aisleStart, int aisleEnd)
    {
        if (colIndex == aisleStart || colIndex == aisleEnd)
        {
            return false;
        }

        var vipRowStart = Math.Max(1, (int)Math.Floor(rows * 0.35));
        var vipRowEnd = Math.Min(rows - 2, (int)Math.Floor(rows * 0.75));
        if (rowIndex < vipRowStart || rowIndex > vipRowEnd)
        {
            return false;
        }

        var vipBand = Math.Max(2, (int)Math.Floor(cols * 0.18));
        var leftVipStart = Math.Max(0, aisleStart - vipBand);
        var rightVipEnd = Math.Min(cols - 1, aisleEnd + vipBand);
        return (colIndex >= leftVipStart && colIndex < aisleStart) || (colIndex > aisleEnd && colIndex <= rightVipEnd);
    }

    private static (int Start, int End) ResolveCenterAisle(int cols)
    {
        if (cols < 10)
        {
            return (-1, -1);
        }

        var start = (cols / 2) - 1;
        return (start, start + 1);
    }

    private static bool TryParseSeatCode(string seatCode, out int rowIndex, out int colIndex)
    {
        rowIndex = -1;
        colIndex = -1;

        var value = (seatCode ?? string.Empty).Trim().ToUpperInvariant();
        if (value.Length < 2)
        {
            return false;
        }

        var letters = new string(value.TakeWhile(char.IsLetter).ToArray());
        var digits = new string(value.SkipWhile(char.IsLetter).ToArray());
        if (letters.Length != 1 || !int.TryParse(digits, out var colNumber))
        {
            return false;
        }

        rowIndex = letters[0] - 'A';
        colIndex = colNumber - 1;
        return rowIndex >= 0 && colIndex >= 0;
    }
}
