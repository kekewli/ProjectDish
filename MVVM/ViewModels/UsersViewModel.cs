using System.Collections.ObjectModel;
using System.Data;
using System.ComponentModel;
using System.Windows;
using ProjectDish.Core;
using ProjectDish.MVVM.Models;
using ProjectDish.Services;

namespace ProjectDish.MVVM.ViewModels
{
    class UsersViewModel : ViewModelBase
    {
        private readonly UserRepository _userRepository;
        private UserModel _selectedUser;
        private string _searchText;
        private bool _isBusy;
        private readonly int _currentUserId;
        public bool IsBusy
        {
            get => _isBusy;
            set
            {
                _isBusy = value;
                OnPropertyChanged();
            }
        }
        public ObservableCollection<UserModel> Users { get; set; } = new ObservableCollection<UserModel>();

        public UserModel SelectedUser
        {
            get => _selectedUser;
            set
            {
                _selectedUser = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ToggleBlockText));
                DeleteUserCommand.RaiseCanExecuteChanged();
                ToggleAdminRightsCommand.RaiseCanExecuteChanged();
                ToggleBlockCommand.RaiseCanExecuteChanged();
            }
        }
        public string SearchText
        {
            get => _searchText;
            set
            {
                _searchText = value;
                OnPropertyChanged();
                _ = LoadData();
            }
        }
        public string ToggleBlockText =>
                 SelectedUser == null
                     ? "⛔ Блокировать"
                     : (SelectedUser.IsBlocked == 1 ? "🔓 Разблокировать" : "⛔ Блокировать");
        public RelayCommand DeleteUserCommand { get; }
        public RelayCommand ToggleAdminRightsCommand { get; }
        public RelayCommand ToggleBlockCommand { get; }
        public RelayCommand CloseCommand { get; }

        public UsersViewModel(int currentUserId)
        {
            if (DesignerProperties.GetIsInDesignMode(new DependencyObject())) return;
            _currentUserId = currentUserId;
            _userRepository = UserRepository.Instance;
            Logger.Info("Users list window opened");
            DeleteUserCommand = new RelayCommand(async o => await DeleteUser(), o => SelectedUser != null);
            ToggleAdminRightsCommand = new RelayCommand(async o => await ToggleAdminRights(), o => SelectedUser != null);
            ToggleBlockCommand = new RelayCommand(async o => await ToggleBlockUser(), o => SelectedUser != null);
            CloseCommand = new RelayCommand(o => (o as Window)?.Close());

            _ = LoadData(true);
        }
        private async Task LoadData(bool forceRefresh = false)
        {
            IsBusy = true;
            try
            {
                var allUsers = await _userRepository.GetUsersAsync(forceRefresh);
                if (allUsers == null)
                {
                    Logger.Warn("LoadData failed to get users list, UI was not updated.");
                    return;
                }

                var filteredUsers = ApplySearch(allUsers);
                UpdateUsersCollection(filteredUsers);
            }
            catch (Exception ex)
            {
                Logger.Error("Unhandled exception in LoadData", ex);
            }
            finally
            {
                IsBusy = false;
            }
        }
        private List<UserModel> ApplySearch(List<UserModel> users)
        {
            if (string.IsNullOrWhiteSpace(SearchText))
                return users;

            return users.Where(u =>
                u.Username.IndexOf(SearchText, StringComparison.OrdinalIgnoreCase) >= 0 ||
                u.Email.IndexOf(SearchText, StringComparison.OrdinalIgnoreCase) >= 0)
                .ToList();
        }
        private void UpdateUsersCollection(List<UserModel> source)
        {
            int? selectedId = SelectedUser?.Id;

            Application.Current.Dispatcher.Invoke(() =>
            {
                Users.Clear();
                foreach (var item in source)
                {
                    Users.Add(item);
                }
            });

            if (selectedId.HasValue)
            {
                SelectedUser = Users.FirstOrDefault(u => u.Id == selectedId.Value);
            }
        }
        // Удаление пользователя
        private async Task DeleteUser()
        {
            var userToDelete = SelectedUser;
            if (userToDelete == null) return;

            if (userToDelete.Id == _currentUserId)
            {
                AppDialog.Show("Вы не можете удалить свой собственный аккаунт.", "Действие запрещено", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (AppDialog.Show($"Удалить пользователя {userToDelete.Username}?", "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;

            bool ok = await DatabaseHelper.ExecuteNonQuery("delete_user_and_recipes", new { p_user = userToDelete.Id });

            if (ok)
            {
                _userRepository.InvalidateCache();
                Users.Remove(userToDelete);
                AppDialog.Show("Пользователь удален.", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        // Смена прав доступа
        private async Task ToggleAdminRights()
        {
            var userToToggle = SelectedUser;
            if (userToToggle == null) return;

            // ЗАПРЕТ: Снятие прав с самого себя
            if (userToToggle.Id == _currentUserId)
            {
                AppDialog.Show("Вы не можете снять с себя права администратора.", "Действие запрещено", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            int newRole = (userToToggle.RoleId == 1) ? 2 : 1;

            bool ok = await DatabaseHelper.ExecuteNonQuery("set_admin_rights", new { p_user_id = userToToggle.Id, p_new_role_id = newRole });

            if (ok)
            {
                _userRepository.InvalidateCache();

                userToToggle.RoleId = newRole;
                userToToggle.RoleName = newRole == 1 ? "Administrator" : "User"; 

                AppDialog.Show("Права обновлены.", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        // Блокировка пользователя
        private async Task ToggleBlockUser()
        {
            var userToToggle = SelectedUser;
            if (userToToggle == null) return;
            if (userToToggle.Id == _currentUserId) // Запрет блокировки своего аккаунта
            {
                AppDialog.Show("Вы не можете заблокировать собственный аккаунт.", "Действие запрещено", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            bool newBlockedState = userToToggle.IsBlocked == 0; // Смена текста кноппки в зависимости от статуса блокировки
            string action = newBlockedState ? "заблокировать" : "разблокировать";
            if (AppDialog.Show($"Вы действительно хотите {action} пользователя {userToToggle.Username}?", "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;
            bool ok = await DatabaseHelper.ExecuteNonQuery("toggle_user_block", new { p_user_id = userToToggle.Id }); // Вызов функции блокировки пользователя
            if (ok)
            {
                _userRepository.InvalidateCache();
                userToToggle.IsBlocked = newBlockedState ? 1 : 0;
                OnPropertyChanged(nameof(ToggleBlockText));
                ToggleBlockCommand.RaiseCanExecuteChanged();
                AppDialog.Show($"Пользователь {(newBlockedState ? "заблокирован" : "разблокирован")}.", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                AppDialog.Show("Ошибка изменения статуса блокировки.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
