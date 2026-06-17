using ProjectDish.Core;
using ProjectDish.MVVM.Models;
using ProjectDish.MVVM.Views;
using ProjectDish.Services;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.Windows;
using System.Windows.Threading;
namespace ProjectDish.MVVM.ViewModels
{
    public class UserStorageViewModel : ViewModelBase
    {
        private readonly int _userId;
        private RecipeModel _selectedRecipe;
        private string _searchText;
        private DispatcherTimer _timer;
        // Коллекция избранных рецептов
        public ObservableCollection<RecipeModel> Recipes { get; set; } = new ObservableCollection<RecipeModel>();
        // Выбранный рецепт
        public RecipeModel SelectedRecipe
        {
            get => _selectedRecipe;
            set
            {
                _selectedRecipe = value;
                OnPropertyChanged();
                DeleteFromFavoritesCommand.RaiseCanExecuteChanged();
            }
        }
        // Текст поиска
        public string SearchText
        {
            get => _searchText;
            set
            {
                _searchText = value;
                OnPropertyChanged();
                Logger.Info("Storage search filter applied", new { query = _searchText, user_id = _userId });
                _ = LoadUserRecipes();
            }
        }
        // Команды
        public RelayCommand OpenDetailsCommand { get; }
        public RelayCommand DeleteFromFavoritesCommand { get; }
        public RelayCommand CloseCommand { get; }
        public UserStorageViewModel(int userId)
        {
            if (DesignerProperties.GetIsInDesignMode(new DependencyObject())) return;
            _userId = userId;
            Logger.Info("User storage initialized", new { user_id = _userId });
            // Инициализация команд
            OpenDetailsCommand = new RelayCommand(o =>
            {
                if (o is int recipeId)
                {
                    Logger.Info("Opening recipe details from storage", new { recipe_id = recipeId, user_id = _userId });
                    var form = new RecipeDetailsView(recipeId, _userId, false);
                    form.ShowDialog();
                }
            });
            DeleteFromFavoritesCommand = new RelayCommand(async o => await DeleteRecipeFromStorage(), o => SelectedRecipe != null);
            CloseCommand = new RelayCommand(o => (o as Window)?.Close());
            _ = LoadUserRecipes();
            // Таймер автообновления
            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(10) };
            _timer.Tick += async (s, e) => await LoadUserRecipes();
            _timer.Start();
        }
        // Загрузка рецептов пользователя
        private async Task LoadUserRecipes()
        {
            try
            {
                int? savedId = SelectedRecipe?.Id;
                DataTable dt;

                if (string.IsNullOrWhiteSpace(SearchText))
                {
                    var rpcParams = new { p_user = _userId };
                    dt = await DatabaseHelper.ExecuteQuery("get_user_recipes", rpcParams);
                    Logger.Info($"get_user_recipes returned {dt.Rows.Count} rows", new { user_id = _userId });
                }
                else
                {
                    var rpcParams = new { p_user = _userId, p_key = SearchText };
                    dt = await DatabaseHelper.ExecuteQuery("search_user_recipes", rpcParams);
                    Logger.Info($"search_user_recipes returned {dt.Rows.Count} rows", new { user_id = _userId, query = SearchText });
                }
                var newItems = new List<RecipeModel>();
                foreach (DataRow row in dt.Rows)
                {
                    newItems.Add(new RecipeModel
                    {
                        Id = Convert.ToInt32(row["recipe_id"]),
                        Name = row["recipe_name"].ToString(),
                        ImageUrl = row.Table.Columns.Contains("image_url") && row["image_url"] != DBNull.Value ? row["image_url"].ToString() : null,
                        Rating = row.Table.Columns.Contains("average_rating") && row["average_rating"] != DBNull.Value ? Convert.ToDecimal(row["average_rating"]) : 0
                    });
                }

                Logger.Info($"Loaded {newItems.Count} recipes for user storage", new { user_id = _userId });

                Recipes.Clear();
                foreach (var item in newItems) Recipes.Add(item);

                if (savedId.HasValue)
                {
                    SelectedRecipe = Recipes.FirstOrDefault(r => r.Id == savedId.Value);
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to load user storage recipes", ex, new { user_id = _userId });
                AppDialog.Show($"Ошибка загрузки избранного: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        // Удаление из избранного
        private async Task DeleteRecipeFromStorage()
        {
            var recipeToDelete = SelectedRecipe;
            if (recipeToDelete == null) return;
            _timer.Stop();
            Logger.Info("Delete recipe from storage requested", new { recipe_id = recipeToDelete.Id, user_id = _userId });
            var result = AppDialog.Show($"Удалить '{recipeToDelete.Name}' из избранного?",
                "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes)
            {
                var rpcParams = new { p_user = _userId, p_recipe = recipeToDelete.Id };
                try
                {
                    bool ok = await DatabaseHelper.ExecuteNonQuery("delete_recipe_from_user_storage", rpcParams);
                    if (ok)
                    {
                        Logger.Info("Recipe removed from storage successfully", new { recipe_id = recipeToDelete.Id, user_id = _userId });
                        SelectedRecipe = null;
                        await LoadUserRecipes();
                    }
                    else
                    {
                        Logger.Warn("Failed to remove recipe from storage - RPC returned false", new { recipe_id = recipeToDelete.Id });
                        AppDialog.Show("Ошибка при удалении рецепта.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error("Exception during recipe removal from storage", ex, new { recipe_id = recipeToDelete.Id });
                    AppDialog.Show($"Ошибка: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            _timer.Start();
        }
        // Очистка таймера при закрытии окна
        public void OnWindowClosing()
        {
            _timer?.Stop();
            Logger.Info("User storage window closed", new { user_id = _userId });
        }
    }
}
