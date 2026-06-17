using System.Text;
using ProjectDish.MVVM.Views;
using ProjectDish.Core;
using ProjectDish.Services;
using System.Text.RegularExpressions;
using System.Windows.Controls;
using System.Windows;
using System.Security.Cryptography;
namespace ProjectDish.MVVM.ViewModels
{
    public class RegisterViewModel : ViewModelBase
    {
        private string _userName;
        private string _email;
        private bool _isBusy;
        public string UserName
        {
            get => _userName;
            set { _userName = value; OnPropertyChanged(); }
        }
        public string Email
        {
            get => _email;
            set { _email = value; OnPropertyChanged(); }
        }
        public bool IsBusy
        {
            get => _isBusy;
            set { _isBusy = value; OnPropertyChanged(); }
        }
        // Команды
        public RelayCommand RegisterCommand { get; set; }
        public RelayCommand NavigateToLoginCommand { get; set; }
        public RelayCommand ExitCommand { get; set; }
        public RegisterViewModel()
        {
            RegisterCommand = new RelayCommand(async (o) => await ExecuteRegister(o));
            NavigateToLoginCommand = new RelayCommand(ExecuteNavigateToLogin);
            ExitCommand = new RelayCommand(o => Application.Current.Shutdown());
        }
        // Список разрешенных доменов
        private static readonly string[] AllowedEmailDomains =
        {
            "mail.ru", "gmail.com", "yandex.ru", "ya.ru", "outlook.com", "hotmail.com"
        };
        // Основная логика регистрации
        private async Task ExecuteRegister(object parameter)
        {
            var passwordBox = parameter as PasswordBox;
            string password = passwordBox?.Password;
            Logger.Info("Registration process started", new { username = UserName, email = Email });
            if (string.IsNullOrEmpty(UserName) || string.IsNullOrEmpty(password) || string.IsNullOrEmpty(Email))
            {
                AppDialog.Show("Заполните все поля.", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (!IsValidUserName(UserName))
            {
                string msg = "Некорректное имя пользователя. Используйте только буквы, цифры и символы - _ .";
                Logger.Warn("Validation failed: Invalid username format", new { username = UserName });
                AppDialog.Show(msg, "Ошибка валидации", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (!IsValidPassword(password))
            {
                string msg = "Пароль слишком простой. Требуется: 8-64 символа, буквы и цифры.";
                Logger.Warn("Validation failed: Weak password", new { username = UserName });
                AppDialog.Show(msg, "Ошибка валидации", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (!IsValidEmail(Email))
            {
                Logger.Warn("Validation failed: Invalid email format or domain", new { email = Email });
                AppDialog.Show("Введите корректный Email с поддерживаемым доменом (mail.ru, gmail.com и др).",
                                "Ошибка валидации", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            IsBusy = true;
            try
            {
                string hashedPassword = HashPassword(password);
                var rpcParams = new
                {
                    p_name = UserName,
                    p_pass = hashedPassword,
                    p_email = Email
                };
                // Запрос к БД
                int result = await DatabaseHelper.ExecuteNonQueryWithReturnValueAsync("register_user", rpcParams);
                switch (result)
                {
                    case 0:
                        Logger.Info("User registered successfully", new { username = UserName, email = Email });
                        AppDialog.Show("Регистрация прошла успешно! Теперь вы можете войти.", "Успех",
                            MessageBoxButton.OK, MessageBoxImage.Information);
                        ExecuteNavigateToLogin(null);
                        break;
                    case 1:
                        Logger.Warn("Registration failed: Username already exists", new { username = UserName });
                        AppDialog.Show("Пользователь с таким именем уже существует.",
                            "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                        break;
                    case 2:
                        Logger.Warn("Registration failed: Email already exists", new { email = Email });
                        AppDialog.Show("Пользователь с таким email уже существует.",
                            "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                        break;
                    case 3:
                    case 4:
                    case 5:
                        Logger.Warn("Registration failed: Server validation rejected", new { error_code = result, username = UserName });
                        AppDialog.Show($"Данные не соответствуют требованиям сервера (Код ошибки: {result}).",
                                        "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                        break;
                    default:
                        Logger.Error("Registration failed: Unknown response code", null, new { error_code = result });
                        AppDialog.Show("Неизвестная ошибка регистрации.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                        break;
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Critical system error during registration", ex, new { username = UserName });
                AppDialog.Show("Произошла системная ошибка. Попробуйте позже.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }
        // Переход назад к авторизации
        private void ExecuteNavigateToLogin(object obj)
        {
            var loginView = new LoginView();
            loginView.Show();

            foreach (Window window in Application.Current.Windows)
            {
                if (window.DataContext == this)
                {
                    window.Close();
                    break;
                }
            }
        }
        // Хэширование
        private string HashPassword(string password)
        {
            using var sha256 = SHA256.Create();
            byte[] hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
            var sb = new StringBuilder();
            foreach (var b in hash) sb.Append(b.ToString("x2"));
            return sb.ToString();
        }
        // Валидация
        private bool IsValidUserName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return false;
            if (name.Length < 3 || name.Length > 32) return false;
            if (ContainsEmoji(name)) return false;
            var pattern = @"^[\p{L}\p{Nd}\s\-_.]+$";
            return Regex.IsMatch(name, pattern);
        }
        private bool IsValidPassword(string password)
        {
            if (string.IsNullOrWhiteSpace(password)) return false;
            if (password.Length < 8 || password.Length > 64) return false;
            if (ContainsEmoji(password)) return false;

            var pattern = @"^[\p{L}\p{Nd}\!\@\#\$\%\^\&\*\(\)\-_+\=\[\]\{\}\;\:\,\.\/\?\<\>\|\\""'`~]+$";
            if (!Regex.IsMatch(password, pattern)) return false;

            bool hasLetter = Regex.IsMatch(password, @"\p{L}");
            bool hasDigit = Regex.IsMatch(password, @"\p{Nd}");
            return hasLetter && hasDigit;
        }
        private bool IsValidEmailDomain(string email)
        {
            try
            {
                var atIndex = email.LastIndexOf('@');
                if (atIndex < 0 || atIndex == email.Length - 1) return false;

                var domain = email.Substring(atIndex + 1).ToLowerInvariant();
                foreach (var allowed in AllowedEmailDomains)
                {
                    if (domain == allowed) return true;
                }
                return false;
            }
            catch { return false; }
        }
        private bool IsValidEmail(string email)
        {
            const string pattern = @"^[^@\s]+@[^@\s]+\.[^@\s]+$";
            if (!Regex.IsMatch(email, pattern, RegexOptions.IgnoreCase)) return false;
            return IsValidEmailDomain(email);
        }
        private bool ContainsEmoji(string input)
        {
            return Regex.IsMatch(input, @"\p{Cs}");
        }
    }
}
