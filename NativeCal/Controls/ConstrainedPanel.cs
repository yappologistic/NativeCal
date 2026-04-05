using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Foundation;

namespace NativeCal.Controls;

/// <summary>
/// A panel that hosts a single child (the Frame) and constrains the
/// horizontal available size during the measure pass. This fixes the
/// WinUI 3 Frame infinite-width measurement bug that causes star-sized
/// Grid columns inside pages to expand based on content rather than
/// dividing the available space equally.
/// </summary>
public sealed class ConstrainedPanel : Panel
{
    protected override Size MeasureOverride(Size availableSize)
    {
        // Ensure the available width is finite. The parent Grid column
        // provides a finite width here (unlike Frame's internal content host).
        foreach (UIElement child in Children)
        {
            child.Measure(availableSize);
        }

        if (Children.Count > 0)
        {
            return Children[0].DesiredSize;
        }

        return new Size(0, 0);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        foreach (UIElement child in Children)
        {
            child.Arrange(new Rect(0, 0, finalSize.Width, finalSize.Height));
        }

        return finalSize;
    }
}
