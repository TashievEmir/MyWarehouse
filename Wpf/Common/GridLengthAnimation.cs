using System.Windows;
using System.Windows.Media.Animation;

namespace Wpf.Common;

public class GridLengthAnimation : AnimationTimeline
{
    public override Type TargetPropertyType => typeof(GridLength);

    public GridLength From
    {
        get => (GridLength)GetValue(FromProperty);
        set => SetValue(FromProperty, value);
    }

    public static readonly DependencyProperty FromProperty =
        DependencyProperty.Register(nameof(From), typeof(GridLength), typeof(GridLengthAnimation));

    public GridLength To
    {
        get => (GridLength)GetValue(ToProperty);
        set => SetValue(ToProperty, value);
    }

    public static readonly DependencyProperty ToProperty =
        DependencyProperty.Register(nameof(To), typeof(GridLength), typeof(GridLengthAnimation));

    protected override Freezable CreateInstanceCore()
    {
        return new GridLengthAnimation();
    }

    public override object GetCurrentValue(object defaultOriginValue, object defaultDestinationValue, AnimationClock clock)
    {
        double from = From.Value;
        double to = To.Value;

        double progress = clock.CurrentProgress ?? 0;

        double value = from + (to - from) * progress;

        return new GridLength(value, GridUnitType.Pixel);
    }
}