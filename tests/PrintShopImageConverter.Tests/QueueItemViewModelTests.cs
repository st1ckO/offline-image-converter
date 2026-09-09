using PrintShopImageConverter.Conversion;
using PrintShopImageConverter.ViewModels;
using Xunit;

namespace PrintShopImageConverter.Tests;

public sealed class QueueItemViewModelTests
{
    [Fact]
    public void Details_labels_pdf_entries_as_pages()
    {
        var viewModel = new QueueItemViewModel(new SourceItem
        {
            Path = @"C:\customer.pdf",
            DisplayName = "customer.pdf",
            Format = "PDF",
            Width = 2_550,
            Height = 3_300,
            FrameCount = 2
        });

        Assert.Contains("2 pages", viewModel.Details);
        Assert.DoesNotContain("frames", viewModel.Details);
    }
}
