using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Mozakra.App.Services;

/// <summary>
/// Show: slide in from the right edge + fade in, then a small "stuck" shake (rotate ±2° twice).
/// Hide: slide out to the right + fade out. No animation when Windows "Show animations" is off.
/// The window needs AllowsTransparency="True" for the fade; the shake rotates the window's root content.
/// </summary>
public sealed class NoteAnimator
{
    private static readonly Duration SlideDuration = TimeSpan.FromMilliseconds(450);
    private static readonly Duration HideDuration = TimeSpan.FromMilliseconds(300);

    private static bool AnimationsEnabled => SystemParameters.ClientAreaAnimation;

    public async Task ShowAsync(Window window)
    {
        if (!AnimationsEnabled)
        {
            window.Opacity = 1;
            window.Show();
            return;
        }

        var target = window.Left;
        window.Opacity = 0;
        window.Left = OffscreenLeft(window);
        window.Show();

        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        await Task.WhenAll(
            AnimateAsync(window, Window.LeftProperty, target, SlideDuration, ease),
            AnimateAsync(window, UIElement.OpacityProperty, 1, SlideDuration, ease));
        await ShakeAsync(window);
    }

    public async Task HideAsync(Window window)
    {
        if (!AnimationsEnabled)
        {
            window.Hide();
            return;
        }

        var home = window.Left;
        var ease = new CubicEase { EasingMode = EasingMode.EaseIn };
        await Task.WhenAll(
            AnimateAsync(window, Window.LeftProperty, OffscreenLeft(window), HideDuration, ease),
            AnimateAsync(window, UIElement.OpacityProperty, 0, HideDuration, ease));
        window.Hide();
        window.Left = home;   // the next show slides back to the same place
        window.Opacity = 1;
    }

    // Just past the right edge of the primary work area (the note lives on the primary screen).
    private static double OffscreenLeft(Window window) => SystemParameters.WorkArea.Right + 8;

    private static Task ShakeAsync(Window window)
    {
        if (window.Content is not FrameworkElement root) return Task.CompletedTask;

        var rotate = new RotateTransform();
        root.RenderTransformOrigin = new Point(0.5, 0);   // pinned at the top, like a sticky note
        root.RenderTransform = rotate;

        var shake = new DoubleAnimationUsingKeyFrames { Duration = TimeSpan.FromMilliseconds(480) };
        double[] angles = [2, -2, 2, -2, 0];
        for (var i = 0; i < angles.Length; i++)
        {
            var at = KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(96 * (i + 1)));
            shake.KeyFrames.Add(new EasingDoubleKeyFrame(angles[i], at, new SineEase()));
        }

        var done = new TaskCompletionSource();
        shake.Completed += (_, _) =>
        {
            root.RenderTransform = Transform.Identity;
            done.TrySetResult();
        };
        rotate.BeginAnimation(RotateTransform.AngleProperty, shake);
        return done.Task;
    }

    /// <summary>Animates a property, then removes the animation and keeps the final value (so it stays settable).</summary>
    private static Task AnimateAsync(UIElement element, DependencyProperty property, double to,
        Duration duration, IEasingFunction ease)
    {
        var done = new TaskCompletionSource();
        var animation = new DoubleAnimation(to, duration) { EasingFunction = ease };
        animation.Completed += (_, _) =>
        {
            element.BeginAnimation(property, null);
            element.SetValue(property, to);
            done.TrySetResult();
        };
        element.BeginAnimation(property, animation);
        return done.Task;
    }
}
