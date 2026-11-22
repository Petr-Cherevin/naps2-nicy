using NAPS2.Sdk.Tests;
using Xunit;

namespace NAPS2.Lib.Tests.Config;

public class ToolbarButtonsTests : ContextualTests
{
    [Theory]
    [InlineData(ToolbarButtons.None, false, false)]
    [InlineData(ToolbarButtons.SaveSeparatePdfs, true, false)]
    [InlineData(ToolbarButtons.ExtractSeparatePdfs, false, true)]
    [InlineData(ToolbarButtons.SaveSeparatePdfs | ToolbarButtons.ExtractSeparatePdfs, true, true)]
    public void SeparatePdfButtonsAreIndependent(ToolbarButtons extra, bool save, bool extract)
    {
        var config = Naps2Config.Stub();
        Assert.False(config.IsToolbarButtonShown(ToolbarButtons.SaveSeparatePdfs));
        Assert.False(config.IsToolbarButtonShown(ToolbarButtons.ExtractSeparatePdfs));
        config.User.Set(c => c.ExtraButtons, extra);
        Assert.Equal(save, config.IsToolbarButtonShown(ToolbarButtons.SaveSeparatePdfs));
        Assert.Equal(extract, config.IsToolbarButtonShown(ToolbarButtons.ExtractSeparatePdfs));
    }

    [Fact]
    public void DefaultButtonsShownUnlessHidden()
    {
        var config = Naps2Config.Stub();
        Assert.True(config.IsToolbarButtonShown(ToolbarButtons.Scan));

        config.User.Set(c => c.HiddenButtons, ToolbarButtons.Scan);
        Assert.False(config.IsToolbarButtonShown(ToolbarButtons.Scan));

        // ExtraButtons only applies to optional buttons
        config.User.Set(c => c.ExtraButtons, ToolbarButtons.Scan);
        Assert.False(config.IsToolbarButtonShown(ToolbarButtons.Scan));
    }

    [Fact]
    public void OptionalButtonsShownOnlyIfExtra()
    {
        var config = Naps2Config.Stub();
        Assert.True(ToolbarButtons.RotateButtons.IsOptional());
        Assert.False(config.IsToolbarButtonShown(ToolbarButtons.RotateButtons));

        config.User.Set(c => c.ExtraButtons, ToolbarButtons.RotateButtons);
        Assert.True(config.IsToolbarButtonShown(ToolbarButtons.RotateButtons));

        // HiddenButtons doesn't apply to optional buttons
        config.User.Set(c => c.HiddenButtons, ToolbarButtons.RotateButtons);
        Assert.True(config.IsToolbarButtonShown(ToolbarButtons.RotateButtons));
    }
}
