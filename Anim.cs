using System;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Island;

// Small helpers so code-driven transitions all share the same feel.
internal static class Anim
{
    public static readonly IEasingFunction Out = Frozen(new CubicEase { EasingMode = EasingMode.EaseOut });
    public static readonly IEasingFunction OutQuart = Frozen(new QuarticEase { EasingMode = EasingMode.EaseOut });
    public static readonly IEasingFunction Spring = Frozen(new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.3 });

    private static readonly ConditionalWeakTable<TextBlock, string> PendingText = new();

    private static IEasingFunction Frozen(EasingFunctionBase ease)
    {
        ease.Freeze();
        return ease;
    }

    public static Duration Ms(double ms) => new(TimeSpan.FromMilliseconds(ms));

    // Animates from whatever value is current (including mid-animation), so interruptions stay smooth.
    public static void To(IAnimatable target, DependencyProperty property, double to, double ms,
                          IEasingFunction? ease = null, double delayMs = 0, EventHandler? completed = null)
    {
        var animation = new DoubleAnimation(to, Ms(ms))
        {
            EasingFunction = ease ?? Out,
            BeginTime = TimeSpan.FromMilliseconds(delayMs),
        };
        if (completed != null) animation.Completed += completed;
        target.BeginAnimation(property, animation);
    }

    // Fades a brush property (Background, Foreground...) to a new color.
    public static void Brush(DependencyObject target, DependencyProperty property, Color to, double ms = 200)
    {
        if (target.GetValue(property) is not SolidColorBrush brush || brush.IsFrozen)
        {
            var from = (target.GetValue(property) as SolidColorBrush)?.Color ?? to;
            brush = new SolidColorBrush(from);
            target.SetValue(property, brush);
        }
        brush.BeginAnimation(SolidColorBrush.ColorProperty, new ColorAnimation(to, Ms(ms)) { EasingFunction = Out });
    }

    // Fade out, swap the text, fade back in. The latest call wins if several overlap.
    public static void SwapText(TextBlock block, string text)
    {
        PendingText.AddOrUpdate(block, text);
        if (block.Text == text && block.Opacity == 1) return;
        To(block, UIElement.OpacityProperty, 0, 90, completed: (_, _) =>
        {
            if (!PendingText.TryGetValue(block, out var latest) || latest != text) return;
            block.Text = text;
            To(block, UIElement.OpacityProperty, 1, 160);
        });
    }
}
