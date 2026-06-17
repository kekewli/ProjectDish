using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
namespace ProjectDish.MVVM.Views.Common
{
    public partial class AppDialogWindow : Window, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        private readonly MessageBoxButton _buttons;
        private MessageBoxResult _result = MessageBoxResult.None;

        public string HeaderTitle { get; }
        public string HeaderIcon { get; }
        public string MessageText { get; }

        public string PrimaryButtonText { get; }
        public string SecondaryButtonText { get; }
        public string CancelButtonText { get; } = "Отмена";

        public Visibility SingleButtonVisibility { get; }
        public Visibility MultiButtonVisibility { get; }
        public Visibility CancelButtonVisibility { get; }

        public MessageBoxResult Result => _result;

        public AppDialogWindow(string message, string title, MessageBoxButton buttons, MessageBoxImage image)
        {
            InitializeComponent();

            _buttons = buttons;

            HeaderTitle = GetHeaderTitle(title, image);
            HeaderIcon = GetHeaderIcon(image);
            MessageText = message ?? string.Empty;

            (PrimaryButtonText, SecondaryButtonText) = GetButtonTexts(buttons);

            SingleButtonVisibility = buttons == MessageBoxButton.OK ? Visibility.Visible : Visibility.Collapsed;
            MultiButtonVisibility = buttons == MessageBoxButton.OK ? Visibility.Collapsed : Visibility.Visible;
            CancelButtonVisibility = (buttons == MessageBoxButton.OKCancel || buttons == MessageBoxButton.YesNoCancel)
                ? Visibility.Visible
                : Visibility.Collapsed;

            DataContext = this;
        }

        private static string GetHeaderTitle(string title, MessageBoxImage image)
        {
            if (!string.IsNullOrWhiteSpace(title))
                return title;

            return image switch
            {
                MessageBoxImage.Error => "Ошибка",
                MessageBoxImage.Warning => "Предупреждение",
                MessageBoxImage.Information => "Успех",
                MessageBoxImage.Question => "Вопрос",
                _ => "Сообщение"
            };
        }

        private static string GetHeaderIcon(MessageBoxImage image)
        {
            return image switch
            {
                MessageBoxImage.Error => "✖",
                MessageBoxImage.Warning => "⚠",
                MessageBoxImage.Information => "✔",
                MessageBoxImage.Question => "?",
                _ => "i"
            };
        }

        private static (string primary, string secondary) GetButtonTexts(MessageBoxButton buttons)
        {
            return buttons switch
            {
                MessageBoxButton.OK => ("Ок", string.Empty),
                MessageBoxButton.OKCancel => ("Ок", "Отмена"),
                MessageBoxButton.YesNo => ("Да", "Нет"),
                MessageBoxButton.YesNoCancel => ("Да", "Нет"),
                _ => ("Ок", string.Empty)
            };
        }

        private void PrimaryButton_Click(object sender, RoutedEventArgs e)
        {
            _result = _buttons switch
            {
                MessageBoxButton.OK => MessageBoxResult.OK,
                MessageBoxButton.OKCancel => MessageBoxResult.OK,
                MessageBoxButton.YesNo => MessageBoxResult.Yes,
                MessageBoxButton.YesNoCancel => MessageBoxResult.Yes,
                _ => MessageBoxResult.OK
            };

            DialogResult = true;
            Close();
        }

        private void SecondaryButton_Click(object sender, RoutedEventArgs e)
        {
            _result = _buttons switch
            {
                MessageBoxButton.OKCancel => MessageBoxResult.Cancel,
                MessageBoxButton.YesNo => MessageBoxResult.No,
                MessageBoxButton.YesNoCancel => MessageBoxResult.No,
                _ => MessageBoxResult.No
            };

            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            _result = MessageBoxResult.Cancel;
            DialogResult = true;
            Close();
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter || e.Key == Key.Space)
            {
                e.Handled = true;
                PrimaryButton_Click(sender, e);
                return;
            }

            if (e.Key == Key.Escape)
            {
                e.Handled = true;
                if (_buttons == MessageBoxButton.OK)
                {
                    PrimaryButton_Click(sender, e);
                }
                else
                {
                    CancelButton_Click(sender, e);
                }
            }
        }
    }
}
