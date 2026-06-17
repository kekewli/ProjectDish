using ProjectDish.MVVM.Views.Common;
using System.Windows;
namespace ProjectDish.Services
{
    public static class AppDialog
    {
        public static MessageBoxResult Show(
            string message,
            string title,
            MessageBoxButton buttons = MessageBoxButton.OK,
            MessageBoxImage image = MessageBoxImage.None)
        {
            var window = new AppDialogWindow(message, title, buttons, image);
            var owner = Application.Current?.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive);
            owner ??= Application.Current?.MainWindow;
            if (owner != null && owner.IsVisible)
            {
                window.Owner = owner;
                window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            }
            else
            {

                window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            }
            window.ShowDialog();
            return window.Result;
        }
    }
}
