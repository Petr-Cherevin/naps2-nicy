using NAPS2.Sdk.Tests;
using Xunit;

namespace NAPS2.Lib.Tests.Config;

public class ToolbarButtonsTests : ContextualTests
{
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
