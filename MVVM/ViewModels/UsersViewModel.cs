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
            Logger.Info("LoadData started", new { forceRefresh, searchText = SearchText });
            IsBusy = true;

            try
            {
                Logger.Info("Fetching users from repository...");
                var allUsers = await _userRepository.GetUsersAsync(forceRefresh);

                if (allUsers == null)
                {
                    Logger.Warn("LoadData failed to get users list, UI was not updated.");
                    MessageBox.Show("Не удалось загрузить список пользователей.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                Logger.Info($"Repository returned {allUsers.Count} users");

                var filteredUsers = ApplySearch(allUsers);
                Logger.Info($"After search filter: {filteredUsers.Count} users");

                UpdateUsersCollection(filteredUsers);
                Logger.Info($"UI updated with {Users.Count} users");
            }
            catch (Exception ex)
            {
                Logger.Error("Unhandled exception in LoadData", ex, new { });
                MessageBox.Show($"Ошибка загрузки: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
                Logger.Info("LoadData completed");
            }
        }

        private List<UserModel> ApplySearch(List<UserModel> users)
        {
            if (string.IsNullOrWhiteSpace(SearchText))
                return users;

            Logger.Info("Applying search filter", new { query = SearchText });

            return users.Where(u =>
                u.Username.IndexOf(SearchText, StringComparison.OrdinalIgnoreCase) >= 0 ||
                u.Email.IndexOf(SearchText, StringComparison.OrdinalIgnoreCase) >= 0)
                .ToList();
        }

        private void UpdateUsersCollection(List<UserModel> source)
        {
            Logger.Info("UpdateUsersCollection started", new { sourceCount = source.Count, currentCount = Users.Count });

            var sourceDict = source.ToDictionary(s => s.Id);
            var targetIds = Users.Select(t => t.Id).ToList();

            // Удаляем отсутствующих
            foreach (var id in targetIds)
            {
                if (!sourceDict.ContainsKey(id))
                {
                    var itemToRemove = Users.First(u => u.Id == id);
                    Application.Current.Dispatcher.Invoke(() => Users.Remove(itemToRemove));
                    Logger.Info($"Removed user from UI", new { user_id = id });
                }
            }

            // Добавляем новых или обновляем существующих
            foreach (var sourceItem in source)
            {
                var targetItem = Users.FirstOrDefault(t => t.Id == sourceItem.Id);
                if (targetItem == null)
                {
                    Application.Current.Dispatcher.Invoke(() => Users.Add(sourceItem));
                    Logger.Info($"Added new user to UI", new { user_id = sourceItem.Id });
                }
                else
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        targetItem.Username = sourceItem.Username;
                        targetItem.Email = sourceItem.Email;
                        targetItem.RoleId = sourceItem.RoleId;
                        targetItem.RoleName = sourceItem.RoleName;
                    });
                    Logger.Info($"Updated existing user in UI", new { user_id = sourceItem.Id });
                }
            }

            Logger.Info("UpdateUsersCollection completed", new { finalCount = Users.Count });
        }

        private async Task UpdateUser()
        {
            var userToUpdate = SelectedUser;
            if (userToUpdate == null) return;

            Logger.Info("Opening user edit form", new { user_id = userToUpdate.Id });

            var editForm = new UpdateUserView(userToUpdate.Id);
            editForm.ShowDialog();

            Logger.Info("Edit form closed");

            _userRepository.InvalidateCache();

            await LoadData(true);

            Logger.Info("User list refreshed after edit");
        }

        private async Task DeleteUser()
        {
            var userToDelete = SelectedUser;
            if (userToDelete == null) return;

            if (userToDelete.Id == App.CurrentUser.Id)
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
                Logger.Info("User deleted successfully", new { user_id = userToDelete.Id });
                MessageBox.Show("Пользователь удален.", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                _userRepository.InvalidateCache();
                await LoadData(true);
            }
            else
            {
                Logger.Warn("Delete user RPC returned false", new { user_id = userToDelete.Id });
                MessageBox.Show("Ошибка удаления.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task ToggleAdminRights()
        {
            var userToToggle = SelectedUser;
            if (userToToggle == null) return;

            if (userToToggle.Id == App.CurrentUser.Id && userToToggle.RoleId == 1)
            {
                MessageBox.Show("Вы не можете снять с себя права администратора.", "Действие запрещено", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            int newRole = (userToToggle.RoleId == 1) ? 2 : 1;
            string newRoleName = newRole == 1 ? "Administrator" : "User";

            var rpcParams = new { p_user_id = userToToggle.Id, p_new_role_id = newRole };
            Logger.Info("Toggling admin rights", new { user_id = userToToggle.Id, new_role = newRole });

            bool ok = await DatabaseHelper.ExecuteNonQuery("set_admin_rights", rpcParams);

            if (ok)
            {
                Logger.Info("Admin rights toggled successfully", new { user_id = userToToggle.Id, new_role = newRole });

                Application.Current.Dispatcher.Invoke(() =>
                {
                    userToToggle.RoleId = newRole;
                    userToToggle.RoleName = newRoleName;
                });
                _userRepository.InvalidateCache();
            }
            else
            {
                Logger.Warn("Toggle admin rights RPC returned false", new { user_id = userToToggle.Id });
                MessageBox.Show("Ошибка обновления прав.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
