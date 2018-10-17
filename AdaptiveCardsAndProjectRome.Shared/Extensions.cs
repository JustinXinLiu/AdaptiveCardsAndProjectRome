using System.Collections.Generic;
using Windows.Foundation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Media;

namespace AdaptiveCardsAndProjectRome.Shared
{
    public static class Extensions
    {
        public static List<FrameworkElement> Children(this DependencyObject parent)
        {
            var list = new List<FrameworkElement>();

            for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);

                if (child is FrameworkElement element)
                {
                    list.Add(element);
                }

                list.AddRange(Children(child));
            }

            return list;
        }

        public static Point RelativePosition(this UIElement element, UIElement other) =>
            element.TransformToVisual(other).TransformPoint(new Point(0, 0));
    }
}