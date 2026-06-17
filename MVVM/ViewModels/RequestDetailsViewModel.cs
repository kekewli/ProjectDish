using ProjectDish.Core;
using ProjectDish.Services;
using System.ComponentModel;
using System.Windows;
namespace ProjectDish.MVVM.ViewModels
{
    class RequestDetailsViewModel : ViewModelBase
    {
        private readonly int _requestId;
        private bool _isBusy;
        private string _recipeName;
        private string _categoryName;
        private string _description;
        private string _ingredients;
        private string _imageUrl;
        public bool IsBusy
        {
            get => _isBusy;
            set { _isBusy = value; OnPropertyChanged(); }
        }
        public string RecipeName
        {
            get => _recipeName;
            set { _recipeName = value; OnPropertyChanged(); }
        }
        public string CategoryName
        {
            get => _categoryName;
            set { _categoryName = value; OnPropertyChanged(); }
        }
        public string Description
        {
            get => _description;
            set { _description = value; OnPropertyChanged(); }
        }
        public string Ingredients
        {
            get => _ingredients;
            set { _ingredients = value; OnPropertyChanged(); }
        }
        public string ImageUrl
        {
            get => _imageUrl;
            set { _imageUrl = value; OnPropertyChanged(); }
        }
        public RelayCommand CloseCommand { get; }
        public RequestDetailsViewModel(int requestId)
        {
            if (DesignerProperties.GetIsInDesignMode(new DependencyObject())) return;

            _requestId = requestId;
            Logger.Info("Opening request details", new { request_id = _requestId });
            CloseCommand = new RelayCommand(o => (o as Window)?.Close());
            _ = LoadDetails();
        }
        private async Task LoadDetails()
        {
            Logger.Info("LoadDetails started", new { request_id = _requestId });
            IsBusy = true;
            try
            {
                var rpcParams = new { p_req_id = _requestId };
                Logger.Info("Calling get_request_details RPC", new { request_id = _requestId });
                var dt = await DatabaseHelper.ExecuteQuery("get_request_details", rpcParams);
                Logger.Info($"RPC returned {dt?.Rows.Count ?? 0} rows", new { request_id = _requestId });
                if (dt != null && dt.Rows.Count > 0)
                {
                    var row = dt.Rows[0];
                    RecipeName = row["recipe_name"].ToString();
                    CategoryName = $"Категория: {row["category_name"]}";
                    Description = row["description"].ToString();
                    Ingredients = row["ingredients"].ToString();
                    ImageUrl = row.Table.Columns.Contains("image_url") && row["image_url"] != DBNull.Value
                        ? row["image_url"].ToString()
                        : null;

                    Logger.Info("Request details loaded successfully", new { request_id = _requestId });
                }
                else
                {
                    Logger.Warn("No data found for request", new { request_id = _requestId });
                    AppDialog.Show("Информация о запросе не найдена.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    CloseCommand.Execute(null);
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to load request details", ex, new { request_id = _requestId });
                AppDialog.Show($"Ошибка загрузки данных: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                Logger.Info("LoadDetails completed", new { request_id = _requestId });
                IsBusy = false;
            }
        }
    }
}
