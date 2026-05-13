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
    public class UserViewModel : ViewModelBase
    {
        private readonly int _userId;
        private RecipeModel _selectedRecipe;
        private string _searchText;
        private int _selectedSortIndex;
        private DispatcherTimer _timer;
        public string UserName => App.CurrentUser?.Username;
        public ObservableCollection<RecipeModel> Recipes { get; set; } = new ObservableCollection<RecipeModel>();
        public RecipeModel SelectedRecipe
        {
            get => _selectedRecipe;
            set
            {
                _selectedRecipe = value;
                OnPropertyChanged();
                AddToFavoritesCommand.RaiseCanExecuteChanged();
            }
        }
        public string SearchText
        {
            get => _searchText;
            set
            {
                _searchText = value;
                OnPropertyChanged();
                Logger.Info("User search filter applied", new { query = _searchText, user_id = _userId });
                _ = LoadData();
            }
        }
        public int SelectedSortIndex
        {
            get => _selectedSortIndex;
            set
            {
                _selectedSortIndex = value;
                OnPropertyChanged();
                Logger.Info("Sort order changed", new { sort_index = _selectedSortIndex });
                _ = LoadData();
            }
        }
        public RelayCommand OpenDetailsCommand { get; }
        public RelayCommand AddToFavoritesCommand { get; }
        public RelayCommand OpenFavoritesCommand { get; }
        public RelayCommand CreateRecipeRequestCommand { get; }
        public RelayCommand LogoutCommand { get; }
        public UserViewModel(int userId)
        {
            if (DesignerProperties.GetIsInDesignMode(new DependencyObject())) return;
            _userId = userId;
            Logger.Info("User dashboard initialized", new { user_id = _userId });
            OpenDetailsCommand = new RelayCommand(o =>
            {
                if (o is int recipeId)
                {
                    Logger.Info("User opening recipe details", new { recipe_id = recipeId, user_id = _userId });
                    var form = new RecipeDetailsView(recipeId, _userId, false);
                    form.ShowDialog();
                    RecipeRepository.Instance.InvalidateCache();
                    _ = LoadData();
                }
            });
            AddToFavoritesCommand = new RelayCommand(async o => await AddToFavorites(), o => SelectedRecipe != null);
            OpenFavoritesCommand = new RelayCommand(o => OpenFavorites());
            CreateRecipeRequestCommand = new RelayCommand(o => CreateRecipeRequest());
            LogoutCommand = new RelayCommand(o =>
            {
                Logger.Info("User logging out", new { user_id = _userId });
                App.CurrentUser = null;
                new LoginView().Show();
                CloseCurrentWindow();
            });
            _ = LoadData();
            // Таймер для автообновления 
            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(60)
            };
            _timer.Tick += async (s, e) => await LoadData();
            _timer.Start();
        }
        private async Task LoadData()
        {
            try
            {
                List<RecipeModel> newItems;

                // Поиск не кэшируем, загружаем напрямую из БД
                if (!string.IsNullOrWhiteSpace(SearchText))
                {
                    newItems = await SearchRecipesAsync(SearchText);
                }
                else
                {
                    // Обычная загрузка с кэшем через RecipeRepository
                    newItems = await RecipeRepository.Instance.GetRecipesAsync();
                }

                if (newItems == null)
                {
                    Logger.Warn("No recipes loaded.");
                    return;
                }
                // Сортировка
                switch (SelectedSortIndex)
                {
                    case 1: newItems = newItems.OrderByDescending(x => x.Rating).ToList(); break;
                    case 2: newItems = newItems.OrderBy(x => x.Rating).ToList(); break;
                    case 3: newItems = newItems.OrderBy(x => x.Name).ToList(); break;
                }
                // Сохранение выделенного элемента
                int? savedId = SelectedRecipe?.Id;
                Recipes.Clear();
                foreach (var item in newItems) Recipes.Add(item);
                if (savedId.HasValue)
                {
                    SelectedRecipe = Recipes.FirstOrDefault(r => r.Id == savedId.Value);
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to load recipes data for user", ex, new { user_id = _userId });
            }
        }

        // Поиск рецептов 
        private async Task<List<RecipeModel>> SearchRecipesAsync(string searchText)
        {
            try
            {
                Logger.Info("Searching recipes...", new { query = searchText });
                var rpcParams = new { p_key = searchText };
                DataTable dt = await DatabaseHelper.ExecuteQuery("search_recipes", rpcParams);

                var items = new List<RecipeModel>();
                foreach (DataRow row in dt.Rows)
                {
                    items.Add(new RecipeModel
                    {
                        Id = Convert.ToInt32(row["recipe_id"]),
                        Name = row["recipe_name"].ToString(),
                        ImageUrl = row.Table.Columns.Contains("image_url") && row["image_url"] != DBNull.Value ? row["image_url"].ToString() : null,
                        Rating = row.Table.Columns.Contains("average_rating") && row["average_rating"] != DBNull.Value ? Convert.ToDecimal(row["average_rating"]) : 0
                    });
                }
                Logger.Info($"Successfully found {items.Count} recipes.");
                return items;
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to search recipes", ex);
                return null;
            }
        }
        private async Task AddToFavorites()
        {
            if (SelectedRecipe == null)
            {
                Logger.Warn("AddToFavorites called with no selected recipe", new { user_id = _userId });
                return;
            }

            var rpcParams = new { p_user = _userId, p_recipe = SelectedRecipe.Id };
            Logger.Info("User adding recipe to favorites", new { user_id = _userId, recipe_id = SelectedRecipe.Id, recipe_name = SelectedRecipe.Name });

            try
            {
                bool added = await DatabaseHelper.ExecuteNonQuery("add_recipe_to_user_storage", rpcParams);

                if (added)
                {
                    Logger.Info("Recipe added to favorites successfully", new { user_id = _userId, recipe_id = SelectedRecipe.Id });
                    MessageBox.Show("Рецепт добавлен в избранное.", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    Logger.Warn("Recipe already in favorites or RPC returned false", new { user_id = _userId, recipe_id = SelectedRecipe.Id });
                    MessageBox.Show("Этот рецепт уже находится в вашем избранном.", "Внимание", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to add recipe to favorites", ex, new { user_id = _userId, recipe_id = SelectedRecipe.Id });
                MessageBox.Show($"Ошибка при добавлении: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void OpenFavorites()
        {
            Logger.Info("User opening favorites", new { user_id = _userId });
            var storageForm = new UserStorageView(_userId);
            storageForm.ShowDialog();
        }
        private void CreateRecipeRequest()
        {
            Logger.Info("User opening 'create recipe request' form", new { user_id = _userId });
            var requestForm = new RecipeView(recipeId: -1, userId: _userId, isRequest: true);
            requestForm.ShowDialog();
        }
        private void CloseCurrentWindow()
        {
            foreach (Window window in Application.Current.Windows)
            {
                if (window.DataContext == this)
                {
                    window.Close();
                    break;
                }
            }
        }
    }
}
