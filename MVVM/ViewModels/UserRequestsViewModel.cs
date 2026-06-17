using System.Collections.ObjectModel;
using System.Data;
using System.ComponentModel;
using System.Windows;
using ProjectDish.Core;
using ProjectDish.MVVM.Models;
using ProjectDish.Services;
using ProjectDish.MVVM.Views;

namespace ProjectDish.MVVM.ViewModels
{
    class UserRequestsViewModel : ViewModelBase
    {
        private RequestModel _selectedRequest;
        private bool _isBusy;
        public ObservableCollection<RequestModel> Requests { get; set; } = new ObservableCollection<RequestModel>();
        public RequestModel SelectedRequest
        {
            get => _selectedRequest;
            set
            {
                _selectedRequest = value;
                OnPropertyChanged();
                ApproveRequestCommand.RaiseCanExecuteChanged();
                RejectRequestCommand.RaiseCanExecuteChanged();
            }
        }
        public bool IsBusy
        {
            get => _isBusy;
            set { _isBusy = value; OnPropertyChanged(); }
        }
        public RelayCommand ApproveRequestCommand { get; }
        public RelayCommand RejectRequestCommand { get; }
        public RelayCommand CloseCommand { get; }
        public RelayCommand OpenDetailsCommand { get; }
        public UserRequestsViewModel()
        {
            if (DesignerProperties.GetIsInDesignMode(new DependencyObject()))
                return;

            Logger.Info("User requests window opened");

            // Инициализация команд
            ApproveRequestCommand = new RelayCommand(async o => await ApproveRequest(), o => SelectedRequest != null);
            RejectRequestCommand = new RelayCommand(async o => await RejectRequest(), o => SelectedRequest != null);
            CloseCommand = new RelayCommand(o => (o as Window)?.Close());
            OpenDetailsCommand = new RelayCommand(o =>
            {
                if (o is RequestModel request)
                {
                    Logger.Info("Opening request details", new { request_id = request.Id });
                    var detailsView = new RequestDetailsView(request.Id);
                    detailsView.ShowDialog();
                    _ = LoadRequests();
                }
            });
            _ = LoadRequests();
        }
        private async Task LoadRequests()
        {
            IsBusy = true;
            try
            {
                Logger.Info("Loading user recipe requests...");
                var dt = await DatabaseHelper.ExecuteQuery("get_user_recipe_requests");
                Logger.Info($"Database returned {dt.Rows.Count} rows");
                Requests.Clear();
                foreach (DataRow row in dt.Rows)
                {
                    var request = new RequestModel
                    {
                        Id = Convert.ToInt32(row["request_id"]),
                        RecipeName = row["recipe_name"].ToString(),
                        ImageUrl = row.Table.Columns.Contains("image_url") && row["image_url"] != DBNull.Value
                            ? row["image_url"].ToString()
                            : null,
                        UserId = Convert.ToInt32(row["user_id"]),
                        UserName = row.Table.Columns.Contains("user_name") ? row["user_name"].ToString() : "Unknown"
                    };
                    Requests.Add(request);
                }
                Logger.Info($"Loaded {Requests.Count} user requests");

                if (Requests.Count == 0)
                {
                    Logger.Info("No pending requests found");
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to load user requests", ex, new { });
                AppDialog.Show($"Ошибка загрузки запросов: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }
        private async Task ApproveRequest()
        {
            var requestToProcess = SelectedRequest;
            if (requestToProcess == null) return;
            if (AppDialog.Show($"Одобрить запрос на рецепт \"{requestToProcess.RecipeName}\"?",
                "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;
            IsBusy = true;
            try
            {
                var rpcParams = new { p_request = requestToProcess.Id };
                bool ok = await DatabaseHelper.ExecuteNonQuery("approve_request", rpcParams);

                if (ok)
                {
                    Logger.Info("Request approved", new { request_id = requestToProcess.Id });
                    await LoadRequests();
                }
                else
                {
                    Logger.Warn("Failed to approve request - RPC returned false", new { request_id = requestToProcess.Id });
                    AppDialog.Show("Не удалось одобрить запрос.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                Logger.Error("ApproveRequest exception", ex, new { request_id = requestToProcess.Id });
                AppDialog.Show($"Ошибка: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }
        private async Task RejectRequest()
        {
            var requestToProcess = SelectedRequest;
            if (requestToProcess == null) return;

            if (AppDialog.Show($"Отклонить рецепт \"{requestToProcess.RecipeName}\"?",
                "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                return;

            IsBusy = true;
            try
            {
                var rpcParams = new { p_request = requestToProcess.Id };
                bool ok = await DatabaseHelper.ExecuteNonQuery("delete_user_request", rpcParams);

                if (ok)
                {
                    Logger.Info("Request rejected and deleted", new { request_id = requestToProcess.Id });
                    await LoadRequests();
                }
                else
                {
                    Logger.Warn("Failed to delete request - RPC returned false", new { request_id = requestToProcess.Id });
                    AppDialog.Show("Не удалось отклонить запрос.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                Logger.Error("RejectRequest exception", ex, new { request_id = requestToProcess.Id });
                AppDialog.Show($"Ошибка: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }
    }
}
