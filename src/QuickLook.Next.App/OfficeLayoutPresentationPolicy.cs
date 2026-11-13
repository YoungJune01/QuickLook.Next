namespace QuickLook.Next.App;

internal static class OfficeLayoutPresentationPolicy
{
    public static double Scale(string layoutKind, double pageWidth, double maxPageWidth)
    {
        // Spreadsheet fonts and grid geometry must use the same readable scale. Wide
        // sheets scroll horizontally instead of squeezing normal rows below a text line.
        if (layoutKind.Equals("workbook", StringComparison.OrdinalIgnoreCase))
            return 1.0;

        return Math.Clamp(maxPageWidth / Math.Max(1, pageWidth), 0.35, 1.0);
    }
}
