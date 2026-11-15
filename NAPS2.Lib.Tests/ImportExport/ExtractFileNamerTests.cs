using NAPS2.ImportExport;
using Xunit;

namespace NAPS2.Lib.Tests.ImportExport;

public class ExtractFileNamerTests
{
    [Theory]
    [InlineData(new[] { 7 }, 9, "7")]
    [InlineData(new[] { 7 }, 45, "07")]
    [InlineData(new[] { 7 }, 120, "007")]
    [InlineData(new[] { 45 }, 45, "45")]
    [InlineData(new[] { 3, 4, 5 }, 45, "03-05")]
    [InlineData(new[] { 5, 3, 4 }, 45, "03-05")]
    [InlineData(new[] { 3, 4 }, 45, "03-04")]
    [InlineData(new[] { 3, 7, 12 }, 45, "03,07,12")]
    [InlineData(new[] { 3, 4, 5, 9, 12 }, 45, "03-05,09,12")]
    [InlineData(new[] { 1, 2, 3, 100, 101 }, 120, "001-003,100-101")]
    public void PageSuffix(int[] pageNumbers, int totalPages, string expected)
    {
        Assert.Equal(expected, ExtractFileNamer.GetPageSuffix(pageNumbers, totalPages));
    }

    [Fact]
    public void LongSuffixUsesRandomCode()
    {
        // Every other page out of 200 would give a suffix far longer than the limit
        var pageNumbers = Enumerable.Range(1, 100).Select(x => x * 2).ToArray();

        var suffix = ExtractFileNamer.GetPageSuffix(pageNumbers, 200, new Random(0));

        Assert.Equal(6, suffix.Length);
        Assert.All(suffix, c => Assert.True(c is >= 'a' and <= 'z' or >= '0' and <= '9'));
    }

    [Fact]
    public void SuffixAtLimitIsKept()
    {
        // 8 separate two-digit pages: 8 * 2 digits + 7 commas = 23 characters
        var pageNumbers = new[] { 1, 3, 5, 7, 9, 11, 13, 15 };

        var suffix = ExtractFileNamer.GetPageSuffix(pageNumbers, 45);

        Assert.Equal("01,03,05,07,09,11,13,15", suffix);
        Assert.True(suffix.Length <= ExtractFileNamer.MaxSuffixLength);
    }
}
