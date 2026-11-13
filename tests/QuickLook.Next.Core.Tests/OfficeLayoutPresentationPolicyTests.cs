using QuickLook.Next.App;
using Xunit;

namespace QuickLook.Next.Core.Tests;

public sealed class OfficeLayoutPresentationPolicyTests
{
    [Theory]
    [InlineData(600, 1200)]
    [InlineData(4800, 1200)]
    [InlineData(4800, 360)]
    public void Wide_workbooks_keep_normal_row_and_font_geometry(double width, double viewport)
    {
        double scale = OfficeLayoutPresentationPolicy.Scale("workbook", width, viewport);
        Assert.Equal(1.0, scale);
        Assert.True(20 * scale >= 12 + 2, "A normal row must fit the unscaled text and borders.");
    }

    [Theory]
    [InlineData("presentation", 1600, 800, 0.5)]
    [InlineData("document", 1600, 800, 0.5)]
    [InlineData("presentation", 1600, 300, 0.35)]
    [InlineData("document", 600, 1200, 1.0)]
    [InlineData("WORKBOOK", 4800, 360, 1.0)]
    public void Other_office_layouts_preserve_fit_and_bounds(string kind, double width, double viewport, double expected)
        => Assert.Equal(expected, OfficeLayoutPresentationPolicy.Scale(kind, width, viewport));
}
