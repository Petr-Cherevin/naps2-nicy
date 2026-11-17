using NAPS2.Config;
using Xunit;

namespace NAPS2.Lib.Tests.Config;

public class PreviewToolbarLayoutTests
{
    [Fact]
    public void EmptyIsDefault()
    {
        var items = PreviewToolbarLayout.Parse("");

        Assert.Equal(PreviewToolbarLayout.DefaultOrder, items.Select(x => x.Id));
        Assert.All(items, x => Assert.True(x.Visible));
        Assert.Equal("", PreviewToolbarLayout.Serialize(items));
        Assert.Equal(PreviewToolbarLayout.DefaultOrder, PreviewToolbarLayout.Parse(null).Select(x => x.Id));
    }

    [Fact]
    public void ExtractAppearsImmediatelyAfterSavePdfInExistingLayouts()
    {
        // Existing saved toolbar configurations do not contain the new id.
        var items = PreviewToolbarLayout.Parse("savePdf,saveImages,delete");
        var ids = items.Select(item => item.Id).ToList();

        Assert.Equal(ids.IndexOf(PreviewToolbarLayout.SavePdf) + 1,
            ids.IndexOf(PreviewToolbarLayout.Extract));
        Assert.Equal(ids.IndexOf(PreviewToolbarLayout.Extract) + 1,
            ids.IndexOf(PreviewToolbarLayout.SaveImages));
    }

    [Fact]
    public void RoundTrip()
    {
        var items = PreviewToolbarLayout.Parse("");
        // Move Delete to the start and hide Crop
        var delete = items.Single(x => x.Id == PreviewToolbarLayout.Delete);
        items.Remove(delete);
        items.Insert(0, delete);
        int cropIndex = items.FindIndex(x => x.Id == PreviewToolbarLayout.Crop);
        items[cropIndex] = items[cropIndex] with { Visible = false };

        var serialized = PreviewToolbarLayout.Serialize(items);
        var parsed = PreviewToolbarLayout.Parse(serialized);

        Assert.NotEqual("", serialized);
        Assert.Equal(items, parsed);
        Assert.Equal(PreviewToolbarLayout.Delete, parsed[0].Id);
        Assert.False(parsed.Single(x => x.Id == PreviewToolbarLayout.Crop).Visible);
    }

    [Fact]
    public void MissingItemsFollowTheirDefaultPredecessor()
    {
        var items = PreviewToolbarLayout.Parse("next,prev,unknown,next,-crop");
        var ids = items.Select(x => x.Id).ToList();

        // Everything is present exactly once, with unknown ids and duplicates dropped
        Assert.Equal(PreviewToolbarLayout.DefaultOrder.Count, ids.Count);
        Assert.Equal(PreviewToolbarLayout.DefaultOrder.OrderBy(x => x), ids.OrderBy(x => x));
        // Specified order is kept
        Assert.True(ids.IndexOf(PreviewToolbarLayout.Next) < ids.IndexOf(PreviewToolbarLayout.Previous));
        // Page number has no predecessor so it goes first; zoom buttons follow "sep1" which follows "next"
        Assert.Equal(PreviewToolbarLayout.PageNumber, ids[0]);
        Assert.Equal(ids.IndexOf(PreviewToolbarLayout.Next) + 1, ids.IndexOf("sep1"));
        Assert.Equal(ids.IndexOf("sep1") + 1, ids.IndexOf(PreviewToolbarLayout.ZoomWindow));
        Assert.False(items.Single(x => x.Id == PreviewToolbarLayout.Crop).Visible);
    }

    [Fact]
    public void DisplayedIdsCollapseSeparators()
    {
        var items = new List<PreviewToolbarLayout.Item>
        {
            new("sep1", true),
            new("sep2", true),
            new(PreviewToolbarLayout.Previous, true),
            new("sep3", true),
            new("sep4", true),
            new(PreviewToolbarLayout.Next, false),
            new("sep5", true),
            new(PreviewToolbarLayout.Delete, true),
            new("sep6", true)
        };

        var displayed = PreviewToolbarLayout.GetDisplayedIds(items);

        Assert.Equal(new[] { PreviewToolbarLayout.Previous, "sep3", PreviewToolbarLayout.Delete }, displayed);
    }

    [Fact]
    public void DisplayedIdsSkipUnavailable()
    {
        var items = new List<PreviewToolbarLayout.Item>
        {
            new(PreviewToolbarLayout.Previous, true),
            new("sep1", true),
            new(PreviewToolbarLayout.Next, true),
            new("sep2", true),
            new(PreviewToolbarLayout.Delete, true)
        };

        var displayed = PreviewToolbarLayout.GetDisplayedIds(items, id => id != PreviewToolbarLayout.Next);

        Assert.Equal(new[] { PreviewToolbarLayout.Previous, "sep1", PreviewToolbarLayout.Delete }, displayed);
    }
}
