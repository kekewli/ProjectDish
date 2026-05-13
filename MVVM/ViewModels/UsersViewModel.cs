using System.Collections.ObjectModel;
using System.Data;
using System.ComponentModel;
using System.Windows;
using ProjectDish.Core;
using ProjectDish.MVVM.Models;
using ProjectDish.Services;
using System.Linq;
using ProjectDish.MVVM.Views;

namespace ProjectDish.MVVM.ViewModels
{
    class UsersViewModel : ViewModelBase
    {
        private readonly UserRepository _userRepository;
        private UserModel _selectedUser;
        private string _searchText;
        private bool _isBusy;

        public bool IsBusy { get => _isBusy; set { _isBusy = value; OnPropertyChanged(); } }

        public ObservableCollection<UserModel> Users { get; set; } = new ObservableCollection<UserModel>();

        public UserModel SelectedUser
        {
            get => _selectedUser;
            set
            {
                _selectedUser = value;
                OnPropertyChanged();
                UpdateUserCommand.RaiseCanExecuteChanged();
                DeleteUserCommand.RaiseCanExecuteChanged();
                ToggleAdminRightsCommand.RaiseCanExecuteChanged();
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

        public RelayCommand UpdateUserCommand { get; }
        public RelayCommand DeleteUserCommand { get; }
        public RelayCommand ToggleAdminRightsCommand { get; }
        public RelayCommand CloseCommand { get; }

        public UsersViewModel()
        {
            if (DesignerProperties.GetIsInDesignMode(new DependencyObject())) return;
            _userRepository = UserRepository.Instance;
            Logger.Info("Users list window opened");

            UpdateUserCommand = new RelayCommand(async o => await UpdateUser(), o => SelectedUser != null);
            DeleteUserCommand = new RelayCommand(async o => await DeleteUser(), o => SelectedUser != null);
            ToggleAdminRightsCommand = new RelayCommand(async o => await ToggleAdminRights(), o => SelectedUser != null);
            CloseCommand = new RelayCommand(o => (o as Window)?.Close());

            _ = LoadData();
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
            var sourceDict = source.ToDictionary(s => s.Id);
            var targetIds = Users.Select(t => t.Id).ToList();
            foreach (var id in targetIds)
            {
                if (!sourceDict.ContainsKey(id))
                {
                    Application.Current.Dispatcher.Invoke(() => Users.Remove(Users.First(u => u.Id == id)));
                }
            }
            foreach (var sourceItem in source)
            {
                var targetItem = Users.FirstOrDefault(t => t.Id == sourceItem.Id);
                if (targetItem == null)
                {
                    Application.Current.Dispatcher.Invoke(() => Users.Add(sourceItem));
                }
                else
                {
                    targetItem.Username = sourceItem.Username;
                    targetItem.Email = sourceItem.Email;
                    targetItem.RoleId = sourceItem.RoleId;
                    targetItem.RoleName = sourceItem.RoleName;
                }
            }
        }

        // Метод для обновления пользователя
        private async Task UpdateUser()
        {
            var userToUpdate = SelectedUser;
            if (userToUpdate == null) return;

            Logger.Info("Opening user edit form", new { user_id = userToUpdate.Id });

            var editForm = new UpdateUserView(userToUpdate.Id);
            editForm.ShowDialog();

            _userRepository.InvalidateCache();
            await LoadData(true);
        }

        private async Task DeleteUser()
        {
            var userToDelete = SelectedUser;
            if (userToDelete == null) return;

            if (App.CurrentUser != null && userToDelete.Id == App.CurrentUser.Id)
            {
                MessageBox.Show("Вы не можете удалить свой собственный аккаунт из этой панели.", "Действие запрещено", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (MessageBox.Show($"Удалить пользователя {userToDelete.Username}?", "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;

            Logger.Info("Deleting user", new { user_id = userToDelete.Id });
            var rpcParams = new { p_user = userToDelete.Id };
            bool ok = await DatabaseHelper.ExecuteNonQuery("delete_user_and_recipes", rpcParams);

            if (ok)
            {
                MessageBox.Show("Пользователь удален.", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                _userRepository.InvalidateCache();
                await LoadData(true);
            }
            else
            {
                MessageBox.Show("Ошибка удаления.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task ToggleAdminRights()
        {
            var userToToggle = SelectedUser;
            if (userToToggle == null) return;

            if (App.CurrentUser != null && userToToggle.Id == App.CurrentUser.Id && userToToggle.RoleId == 1)
            {
                MessageBox.Show("Вы не можете снять с себя права администратора.", "Действие запрещено", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            int newRole = (userToToggle.RoleId == 1) ? 2 : 1;
            var rpcParams = new { p_user_id = userToToggle.Id, p_new_role_id = newRole };
            Logger.Info("Toggling admin rights", new { user_id = userToToggle.Id, new_role = newRole });

            bool ok = await DatabaseHelper.ExecuteNonQuery("set_admin_rights", rpcParams);
            if (ok)
            {
                MessageBox.Show("Права обновлены.", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                _userRepository.InvalidateCache();
                await LoadData(true);
            }
            else
            {
                MessageBox.Show("Ошибка обновления прав.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
