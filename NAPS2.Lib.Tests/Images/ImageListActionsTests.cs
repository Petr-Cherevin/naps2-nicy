using NAPS2.EtoForms.Desktop;
using NAPS2.EtoForms.Notifications;
using NAPS2.ImportExport;
using NAPS2.Sdk.Tests;
using NSubstitute;
using Xunit;

namespace NAPS2.Lib.Tests.Images;

public class ImageListActionsTests : ContextualTests
{
    private static ImageListActions MakeActions(UiImageList list, IExportController exporter)
    {
        // Extraction only uses the image list, export controller and notifier.
        return new ImageListActions(list, null!, null!, Naps2Config.Stub(), null!,
            exporter, Substitute.For<INotify>(), null!);
    }

    private UiImage AddPage(UiImageList list)
    {
        var page = new UiImage(CreateScannedImage());
        list.Mutate(new ListMutation<UiImage>.Append(page));
        return page;
    }

    [Fact]
    public async Task ExtractFromPreview_UsesCurrentPageNotMainWindowSelection()
    {
        var list = new UiImageList();
        var first = AddPage(list);
        var current = AddPage(list);
        list.UpdateSelection(ListSelection.From(new[] { first, current }));
        var exporter = Substitute.For<IExportController>();
        exporter.ExtractPdf(Arg.Any<ICollection<UiImage>>(), Arg.Any<ISaveNotify>())
            .Returns(Task.FromResult(true));

        await MakeActions(list, exporter)
            .WithSelection(() => ListSelection.Of(current))
            .ExtractSelectedAsPdf();

        await exporter.Received(1).ExtractPdf(
            Arg.Is<ICollection<UiImage>>(pages => pages.Count == 1 && pages.Single() == current),
            Arg.Any<ISaveNotify>());
        Assert.Single(list.Images);
        Assert.Contains(first, list.Images);
        Assert.DoesNotContain(current, list.Images);
    }

    [Fact]
    public async Task ExtractCancelledOrFailed_DoesNotRemovePage()
    {
        var list = new UiImageList();
        var first = AddPage(list);
        var exporter = Substitute.For<IExportController>();
        exporter.ExtractPdf(Arg.Any<ICollection<UiImage>>(), Arg.Any<ISaveNotify>())
            .Returns(Task.FromResult(false));

        await MakeActions(list, exporter)
            .WithSelection(() => ListSelection.Of(first))
            .ExtractSelectedAsPdf();

        Assert.Single(list.Images);
        Assert.Contains(first, list.Images);
    }

    [Fact]
    public async Task ExtractWithoutSelectedPages_DoesNotOpenSaveDialog()
    {
        var list = new UiImageList();
        AddPage(list);
        var exporter = Substitute.For<IExportController>();

        await MakeActions(list, exporter).ExtractSelectedAsPdf();

        await exporter.DidNotReceiveWithAnyArgs().ExtractPdf(default!, default!);
        Assert.Single(list.Images);
    }

    [Fact]
    public async Task ExtractFromMainWindow_ExportsAllSelectedPages()
    {
        var list = new UiImageList();
        var first = AddPage(list);
        var second = AddPage(list);
        list.UpdateSelection(ListSelection.From(new[] { first, second }));
        var exporter = Substitute.For<IExportController>();
        exporter.ExtractPdf(Arg.Any<ICollection<UiImage>>(), Arg.Any<ISaveNotify>())
            .Returns(Task.FromResult(true));

        await MakeActions(list, exporter).ExtractSelectedAsPdf();

        await exporter.Received(1).ExtractPdf(
            Arg.Is<ICollection<UiImage>>(pages => pages.Count == 2 &&
                pages.Contains(first) && pages.Contains(second)),
            Arg.Any<ISaveNotify>());
        Assert.Empty(list.Images);
    }
}
