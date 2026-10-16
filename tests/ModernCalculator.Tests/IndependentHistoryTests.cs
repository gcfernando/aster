using System.Globalization;
using ModernCalculator.Core.History;
using Xunit;

namespace ModernCalculator.Tests;

public sealed class IndependentHistoryTests
{
    [Theory]
    [InlineData("sv-SE", "Standard")]
    [InlineData("de-DE", "Scientific")]
    [InlineData("fr-FR", "Converter")]
    public void SearchFindsDisplayedAndCanonicalDecimalResults(string cultureName, string mode)
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(cultureName);
            var journal = new HistoryJournal();
            journal.Record(mode, "3 / 2 =", "1.5");
            var expected = Assert.Single(journal.Items);
            Assert.Equal(expected, Assert.Single(journal.Search("1,5")));
            Assert.Equal(expected, Assert.Single(journal.Search("1.5")));
            Assert.Equal(expected, Assert.Single(journal.Search("3 / 2")));
            Assert.Equal(expected, Assert.Single(journal.Search(mode.ToLowerInvariant())));
            Assert.Empty(journal.Search("1,6"));
            Assert.Equal("1.5", expected.Result);
        }
        finally { CultureInfo.CurrentCulture = previous; }
    }

    [Fact]
    public void RegionalSearchPreservesExactLongResultsAndDateText()
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("sv-SE");
            var journal = new HistoryJournal();
            const string exact = "12345678901234567890123456789012345678901234567890.125";
            journal.Record("Standard", "large / 8 =", exact);
            journal.Record("Dates", "Adjusted date", "2028-02-29");
            journal.Record("Dates", "Days between dates", "30 days");
            Assert.Equal(exact, Assert.Single(journal.Search("7890,125")).Result);
            Assert.Equal(exact, Assert.Single(journal.Search("7890.125")).Result);
            Assert.Equal("2028-02-29", Assert.Single(journal.Search("2028-02-29")).Result);
            Assert.Equal("30 days", Assert.Single(journal.Search("30 days")).Result);
            Assert.Equal(3, journal.Items.Count);
        }
        finally { CultureInfo.CurrentCulture = previous; }
    }
}
