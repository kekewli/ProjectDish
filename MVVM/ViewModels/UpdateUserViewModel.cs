using ProjectDish.Core;
using ProjectDish.Services;
using System.ComponentModel;
using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Linq;
using System.Windows;

namespace ProjectDish.MVVM.ViewModels
{
    class UpdateUserViewModel : ViewModelBase
    {
        private readonly int _userId;
        private string _userName;
        private string _email;
        private bool _isBusy;

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
                    MessageBox.Show("Пользователь не найден.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    CancelCommand.Execute(null);
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to load user data", ex, new { user_id = _userId });
                MessageBox.Show($"Ошибка загрузки данных: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
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

            if (string.IsNullOrEmpty(UserName) || string.IsNullOrEmpty(Email))
            {
                MessageBox.Show("Введите имя и email.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!Email.Contains("@") || !Email.Contains("."))
            {
                MessageBox.Show("Введите корректный email.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            IsBusy = true;
            try
            {
                var passwordBox = parameter as System.Windows.Controls.PasswordBox;
                string newPassword = passwordBox?.Password;

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


                    var window = Application.Current.Windows.OfType<Window>().SingleOrDefault(w => w.DataContext == this);
                    CancelCommand.Execute(window);
                }
                else
                {
                    Logger.Warn("Update user RPC returned false", new { user_id = _userId });
                    MessageBox.Show("Ошибка обновления.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to save user data", ex, new { user_id = _userId });
                MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
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
    }
}
