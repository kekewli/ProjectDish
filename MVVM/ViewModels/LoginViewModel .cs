using System.Text;
using ProjectDish.Core;
using ProjectDish.Services;
using ProjectDish.MVVM.Views;
using System.Windows.Controls;
using System.Windows;
using System.Data;
using System.Security.Cryptography;
namespace ProjectDish.MVVM.ViewModels
{
    public class LoginViewModel : ViewModelBase
    {
        private string _userName;
        private bool _isBusy;

        // Логин пользователя
        public string UserName
        {
            get => _userName;
            set { _userName = value; OnPropertyChanged(); }
        }

        public bool IsBusy
        {
            get => _isBusy;
            set { _isBusy = value; OnPropertyChanged(); }
        }

        // Команды
        public RelayCommand LoginCommand { get; set; }
        public RelayCommand NavigateToRegisterCommand { get; set; }
        public RelayCommand ForgotPasswordCommand { get; set; }
        public RelayCommand ExitCommand { get; set; }

        public LoginViewModel()
        {
            // Кнопка войти
            LoginCommand = new RelayCommand(async (o) => await ExecuteLogin(o));

            // Переход на регистрацию
            NavigateToRegisterCommand = new RelayCommand(o => OpenWindowAndCloseCurrent(new RegisterView()));

            // Восстановления пароля
            ForgotPasswordCommand = new RelayCommand(o => new PasswordResetView().ShowDialog());

            // Выход
            ExitCommand = new RelayCommand(o => Application.Current.Shutdown());
        }

        // Логика входа
        private async Task ExecuteLogin(object parameter)
        {
            var passwordBox = parameter as PasswordBox;
            string password = passwordBox?.Password;

            if (string.IsNullOrEmpty(UserName) || string.IsNullOrEmpty(password))
            {
                MessageBox.Show("Введите логин и пароль.", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            IsBusy = true;

            Logger.Info("Login attempt initiated", new { username = UserName });

            try
            {
                string hashedPassword = HashPassword(password);
                var rpcParams = new { p_name = UserName, p_pass = hashedPassword };

                DataTable result = await DatabaseHelper.ExecuteQuery("login_user", rpcParams);

                if (result.Rows.Count > 0)
                {
                    int roleId = Convert.ToInt32(result.Rows[0]["role_id"]);
                    int userId = Convert.ToInt32(result.Rows[0]["user_id"]);

                    Logger.Info("Login successful", new { user_id = userId, role_id = roleId, username = UserName });

                    if (roleId == 1)
                    {
                        // Панель администратора
                        OpenWindowAndCloseCurrent(new AdminView());
                    }
                    else
                    {
                        // Панель пользователя
                        OpenWindowAndCloseCurrent(new UserView(userId));
                    }
                }
                else
                {
                    Logger.Warn("Login failed: Invalid credentials", new { username = UserName });
                    MessageBox.Show("Неверный логин или пароль.", "Ошибка авторизации", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                Logger.Error("System error during login process", ex, new { username = UserName });
            }
            finally
            {
                IsBusy = false;
            }
        }

        // Метод хеширования
        private string HashPassword(string password)
        {
            using (var sha256 = SHA256.Create())
            {
                byte[] bytes = Encoding.UTF8.GetBytes(password);
                byte[] hash = sha256.ComputeHash(bytes);
                var sb = new StringBuilder();
                foreach (var b in hash)
                    sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }

        // Вспомогательный метод для смены окон
        private void OpenWindowAndCloseCurrent(Window newWindow)
        {
            newWindow.Show();

            foreach (Window win in Application.Current.Windows)
            {
                if (win.DataContext == this)
                {
                    win.Close();
                    break;
                }
            }
        }
    }
}
