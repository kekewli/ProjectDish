using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Animation;
namespace ProjectDish.Core
{
    public static class FadeWindowBehavior
    {
        public static void Attach(Window window)
        {
            window.Opacity = 0;
            window.Loaded += (_, _) =>
            {
                var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(250))
                {
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                };

                window.BeginAnimation(Window.OpacityProperty, fadeIn);
            };

            window.Closing += async (s, e) =>
            {
                if (window.Tag is bool closing && closing)
                    return;

                e.Cancel = true;
                window.Tag = true;

                await FadeOutAndCloseAsync(window);
            };
        }

        private static Task FadeOutAndCloseAsync(Window window)
        {
            var tcs = new TaskCompletionSource<bool>();

            var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(200))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
            };

            fadeOut.Completed += (_, _) =>
            {
                window.BeginAnimation(Window.OpacityProperty, null);
                window.Close();
                tcs.TrySetResult(true);
            };

            window.BeginAnimation(Window.OpacityProperty, fadeOut);
            return tcs.Task;
        }
    }
}
