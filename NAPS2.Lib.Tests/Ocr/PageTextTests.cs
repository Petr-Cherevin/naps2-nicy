using NAPS2.Ocr;
using Xunit;

namespace NAPS2.Lib.Tests.Ocr;

public class PageTextTests
{
    [Fact]
    public void GroupIntoLines()
    {
        var words = new List<PageTextWord>
        {
            // Second line, given first to check ordering
            new("second", 10, 40, 50, 10),
            new("line", 65, 41, 30, 9),
            // First line, with words at slightly different heights (as with descenders etc.)
            new("line", 60, 10, 30, 10),
            new("First", 10, 11, 40, 12),
            // Far to the right on the first line, e.g. in another column
            new("aside", 300, 10, 40, 10)
        };

        var lines = PageText.GroupIntoLines(words);

        Assert.Equal(3, lines.Count);
        Assert.Equal("First line", lines[0].Text);
        Assert.Equal(10, lines[0].X);
        Assert.Equal(10, lines[0].Y);
        Assert.Equal(80, lines[0].Width);
        Assert.Equal(13, lines[0].Height);
        Assert.Equal("aside", lines[1].Text);
        Assert.Equal(300, lines[1].X);
        Assert.Equal("second line", lines[2].Text);
        Assert.Equal(40, lines[2].Y);
    }

    [Fact]
    public void GroupIntoLinesEmpty()
    {
        Assert.Empty(PageText.GroupIntoLines([]));
    }
}
