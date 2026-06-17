using ProjectDish.Core;
using ProjectDish.Services;
using System.ComponentModel;
using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;

namespace ProjectDish.MVVM.ViewModels
{
    internal class PasswordResetViewModel : ViewModelBase
    {
        // Поля
        private bool _isBusy;
        private int _currentStep = 1;
        private string _email;
        private string _resetCode;
        // Свойства для привязки
        public bool IsBusy { get => _isBusy; set { _isBusy = value; OnPropertyChanged(); } }
        public int CurrentStep { get => _currentStep; set { _currentStep = value; OnPropertyChanged(); } }
        public string Email { get => _email; set { _email = value; OnPropertyChanged(); } }
        public string ResetCode { get => _resetCode; set { _resetCode = value; OnPropertyChanged(); } }
        // Команды
        public RelayCommand SendCodeCommand { get; }
        public RelayCommand ResetPasswordCommand { get; }
        public RelayCommand GoBackCommand { get; }
        public RelayCommand CloseCommand { get; }
        public PasswordResetViewModel()
        {
            if (DesignerProperties.GetIsInDesignMode(new DependencyObject())) return;

            SendCodeCommand = new RelayCommand(async o => await ExecuteSendCode(), o => !string.IsNullOrWhiteSpace(Email));
            ResetPasswordCommand = new RelayCommand(async (o) => await ExecuteResetPassword(o));
            GoBackCommand = new RelayCommand(o => CurrentStep = 1);
            CloseCommand = new RelayCommand(o => (o as Window)?.Close());
        }
        // Отправка кода
        private async Task ExecuteSendCode()
        {
            IsBusy = true;
            Logger.Info("Password reset code requested", new { email = Email });
            try
            {
                var rpcParams = new { p_email = Email.ToLowerInvariant() };
                DataTable dt = await DatabaseHelper.ExecuteQuery("create_password_reset_token", rpcParams); // Вызов функции создания токена на смену пароля
                if (dt.Rows.Count > 0) // Отправка кода на выбранную почту
                {
                    string code = dt.Rows[0][0].ToString();
                    await EmailService.SendPasswordResetCodeAsync(Email, code);
                }
                AppDialog.Show("Если email зарегистрирован в системе, на него будет отправлен код восстановления.", "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
                CurrentStep = 2;
            }
            catch (Exception ex) 
            {
                Logger.Error("Failed to send password reset code", ex, new { email = Email });
                AppDialog.Show($"Произошла ошибка: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }
        // Сброс пароля
        private async Task ExecuteResetPassword(object parameter)
        {
            var passwords = parameter as object[];
            var passwordBox = passwords[0] as PasswordBox;
            var confirmPasswordBox = passwords[1] as PasswordBox;
            string password = passwordBox.Password;
            string confirm = confirmPasswordBox.Password;
            if (string.IsNullOrWhiteSpace(ResetCode) || string.IsNullOrWhiteSpace(password))
            {
                AppDialog.Show("Заполните все поля.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error); return;
            }
            if (password != confirm)
            {
                AppDialog.Show("Пароли не совпадают.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error); return;
            }
            if (!IsValidPassword(password))
            {
                AppDialog.Show("Пароль должен быть от 8 до 64 символов и содержать буквы и цифры.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error); return;
            }
            IsBusy = true;
            Logger.Info("Attempting to reset password", new { email = Email });
            try
            {
                var rpcParams = new
                {
                    p_email = Email,
                    p_token = ResetCode,
                    p_new_hash = HashPassword(password)
                };
                bool ok = await DatabaseHelper.ExecuteNonQuery("reset_password", rpcParams);
                if (ok)
                {
                    Logger.Info("Password reset successful", new { email = Email });
                    AppDialog.Show("Пароль успешно изменен. Теперь вы можете войти.", "Готово", MessageBoxButton.OK, MessageBoxImage.Information);
                    CloseCommand.Execute(Application.Current.Windows.OfType<Window>().SingleOrDefault(w => w.DataContext == this));
                }
                else
                {
                    Logger.Warn("Password reset failed: Invalid code or token expired", new { email = Email });
                    AppDialog.Show("Код неверный или устарел.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to reset password", ex, new { email = Email });
                AppDialog.Show($"Произошла ошибка: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }
        // Вспомогательные методы
        private bool IsValidPassword(string password)
        {
            if (string.IsNullOrWhiteSpace(password) || password.Length < 8 || password.Length > 64) return false;
            if (Regex.IsMatch(password, @"\p{Cs}")) return false;
            return Regex.IsMatch(password, @"\p{L}") && Regex.IsMatch(password, @"\p{Nd}");
        }
        private string HashPassword(string password)
        {
            using var sha256 = SHA256.Create();
            byte[] bytes = Encoding.UTF8.GetBytes(password);
            byte[] hash = sha256.ComputeHash(bytes);
            return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        }
    }
}
