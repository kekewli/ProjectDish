using ProjectDish.Core;
using ProjectDish.Services;
using System.ComponentModel;
using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Linq;
using System.Windows;
using System.Text.RegularExpressions; 

namespace ProjectDish.MVVM.ViewModels
{
    class UpdateUserViewModel : ViewModelBase
    {
        private readonly int _userId;
        private string _userName;
        private string _email;
        private bool _isBusy;

        // Список разрешенных доменов
        private static readonly string[] AllowedEmailDomains =
        {
            "mail.ru", "gmail.com", "yandex.ru", "ya.ru", "outlook.com", "hotmail.com"
        };

        public bool IsBusy
        {
            get => _isBusy;
            set { _isBusy = value; OnPropertyChanged(); }
        }

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

        public RelayCommand SaveCommand { get; }
        public RelayCommand CancelCommand { get; }

        public UpdateUserViewModel(int userId)
        {
            if (DesignerProperties.GetIsInDesignMode(new DependencyObject())) return;

            _userId = userId;
            Logger.Info("UpdateUserViewModel initialized", new { user_id = _userId });

            SaveCommand = new RelayCommand(async o => await SaveUser(o), o => !IsBusy);
            CancelCommand = new RelayCommand(o => (o as Window)?.Close());

            _ = LoadUserData();
        }

        private async Task LoadUserData()
        {
            Logger.Info("Loading user data", new { user_id = _userId });
            IsBusy = true;

            try
            {
                var rpcParams = new { p_user = _userId };
                DataTable dt = await DatabaseHelper.ExecuteQuery("get_user_by_id", rpcParams);

                Logger.Info($"get_user_by_id returned {dt?.Rows.Count ?? 0} rows", new { user_id = _userId });

                if (dt != null && dt.Rows.Count > 0)
                {
                    UserName = dt.Rows[0]["user_name"].ToString();
                    Email = dt.Rows[0]["email"].ToString();
                    Logger.Info("User data loaded successfully", new { user_id = _userId, username = UserName });
                }
                else
                {
                    Logger.Warn("User not found", new { user_id = _userId });
                    AppDialog.Show("Пользователь не найден.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    CancelCommand.Execute(null);
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to load user data", ex, new { user_id = _userId });
                AppDialog.Show($"Ошибка загрузки данных: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                CancelCommand.Execute(null);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task SaveUser(object parameter)
        {
            if (IsBusy) return;

            // 1. Проверка на пустоту
            if (string.IsNullOrEmpty(UserName) || string.IsNullOrEmpty(Email))
            {
                AppDialog.Show("Заполните имя пользователя и email.", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // 2. Валидация логина
            if (!IsValidUserName(UserName))
            {
                string msg = "Некорректное имя пользователя. Используйте только буквы, цифры и символы - _ .";
                Logger.Warn("Validation failed: Invalid username format in update", new { username = UserName });
                AppDialog.Show(msg, "Ошибка валидации", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // 3. Валидация Email
            if (!IsValidEmail(Email))
            {
                Logger.Warn("Validation failed: Invalid email format or domain in update", new { email = Email });
                AppDialog.Show("Введите корректный Email с поддерживаемым доменом (mail.ru, gmail.com и др).",
                                "Ошибка валидации", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var passwordBox = parameter as System.Windows.Controls.PasswordBox;
            string newPassword = passwordBox?.Password;

            // 4. Валидация пароля (ТОЛЬКО если пользователь решил его сменить)
            if (!string.IsNullOrEmpty(newPassword))
            {
                if (!IsValidPassword(newPassword))
                {
                    string msg = "Пароль слишком простой. Требуется: 8-64 символа, буквы и цифры.";
                    Logger.Warn("Validation failed: Weak password in update", new { username = UserName });
                    AppDialog.Show(msg, "Ошибка валидации", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
            }

            IsBusy = true;
            try
            {
                string hashedPassword = string.IsNullOrEmpty(newPassword) ? null : HashPassword(newPassword);

                var rpcParams = new
                {
                    p_user_id = _userId,
                    p_name = UserName,
                    p_pass = hashedPassword,
                    p_em = Email
                };

                Logger.Info("Saving user data", new { user_id = _userId, username = UserName, email = Email, passwordChanged = !string.IsNullOrEmpty(hashedPassword) });

                bool success = await DatabaseHelper.ExecuteNonQuery("update_user", rpcParams);

                if (success)
                {
                    Logger.Info("User data updated successfully", new { user_id = _userId });

                    UserRepository.Instance.InvalidateCache();
                    Logger.Info("User cache invalidated after update", new { user_id = _userId });

                    if (App.CurrentUser != null)
                    {
                        App.CurrentUser.Username = UserName;
                        App.CurrentUser.Email = Email;
                    }

                    var window = Application.Current.Windows.OfType<Window>().SingleOrDefault(w => w.DataContext == this);
                    CancelCommand.Execute(window);
                }
                else
                {
                    Logger.Warn("Update user RPC returned false", new { user_id = _userId });
                    AppDialog.Show("Ошибка обновления. Возможно, такой логин или почта уже заняты.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to save user data", ex, new { user_id = _userId });
                AppDialog.Show($"Ошибка: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }
        private string HashPassword(string password)
        {
            using var sha256 = SHA256.Create();
            byte[] hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
            var sb = new StringBuilder();
            foreach (var b in hash) sb.Append(b.ToString("x2"));
            return sb.ToString();
        }

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
