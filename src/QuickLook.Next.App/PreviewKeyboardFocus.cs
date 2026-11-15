using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;

namespace QuickLook.Next.App;

internal static class PreviewKeyboardFocus
{
    // Containers are focusable for scrolling/selection but do not own the preview toggle.
    public static bool UsesSpace(object? element)
        => element is TextBox or PasswordBox or AutoSuggestBox or RichEditBox
            or ButtonBase or ToggleSwitch or ComboBox or Slider or NumberBox
            or CalendarDatePicker or DatePicker or TimePicker;
}
