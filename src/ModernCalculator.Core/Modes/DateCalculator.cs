// Developer ::> Gehan Fernando
namespace ModernCalculator.Core.Modes;

public sealed record DateDifference(int TotalDays, int Years, int Months, int Days)
{
    public int Weeks => TotalDays / 7;
    public int RemainingDays => TotalDays % 7;
}

public static class DateCalculator
{
    public static DateDifference Difference(DateOnly first, DateOnly second)
    {
        var start = first < second ? first : second;
        var end = first < second ? second : first;
        var years = end.Year - start.Year;
        if (start.AddYears(years) > end) years--;
        var cursor = start.AddYears(years);
        var months = (end.Year - cursor.Year) * 12 + end.Month - cursor.Month;
        if (cursor.AddMonths(months) > end) months--;
        return new(end.DayNumber - start.DayNumber, years, months, end.DayNumber - cursor.AddMonths(months).DayNumber);
    }

    // Apply years and months before day offsets so month-end clamping follows DateOnly.
    public static DateOnly Add(DateOnly start, int years, int months, int weeks, int days) =>
        start.AddYears(years).AddMonths(months).AddDays(checked(weeks * 7 + days));
}
