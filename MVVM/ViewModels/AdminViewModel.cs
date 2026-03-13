using ProjectDish.Core;
using ProjectDish.MVVM.Models;
using ProjectDish.MVVM.Views;
using ProjectDish.Services;
using System.Collections.ObjectModel;
using System.Data;
using System.Windows.Threading;
using System.Windows;
using System.ComponentModel;

namespace ProjectDish.MVVM.ViewModels
{
    public class AdminViewModel : ViewModelBase
    {
        private RecipeModel _selectedRecipe;
        private string _searchText;
        private int _selectedSortIndex;
        private DispatcherTimer _timer;

        // Коллекция рецептов для привязки к интерфейсу
        public ObservableCollection<RecipeModel> Recipes { get; set; } = new ObservableCollection<RecipeModel>();

        // Выбранный рецепт
        public RecipeModel SelectedRecipe
        {
            get => _selectedRecipe;
            set
            {
                _selectedRecipe = value;
                OnPropertyChanged();
                EditRecipeCommand.RaiseCanExecuteChanged();
                DeleteRecipeCommand.RaiseCanExecuteChanged();
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
                Logger.Info("Search filter applied", new { query = _searchText });
                _ = LoadData();
            }
        }

        // Индекс сортировки (0 - без, 1 - рейтинг убыв, 2 - рейтинг возр, 3 - А-Я)
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

        // Команды
        public RelayCommand AddRecipeCommand { get; }
        public RelayCommand EditRecipeCommand { get; }
        public RelayCommand DeleteRecipeCommand { get; }
        public RelayCommand OpenRequestsCommand { get; }
        public RelayCommand OpenUsersCommand { get; }
        public RelayCommand OpenDetailsCommand { get; }
        public RelayCommand LogoutCommand { get; }

        public AdminViewModel()
        {
            // Защита от дизайнера
            if (DesignerProperties.GetIsInDesignMode(new DependencyObject()))
                return;

            Logger.Info("Admin dashboard initialized");

            // Инициализация команд
            AddRecipeCommand = new RelayCommand(o =>
            {
                Logger.Info("Opening 'Add Recipe' form");
                var form = new RecipeView();
                form.ShowDialog();
                _ = LoadData();
            });

            EditRecipeCommand = new RelayCommand(o =>
            {
                if (SelectedRecipe != null)
                {
                    Logger.Info("Opening 'Edit Recipe' form", new { recipe_id = SelectedRecipe.Id, recipe_name = SelectedRecipe.Name });
                    var form = new RecipeView(SelectedRecipe.Id);
                    form.ShowDialog();
                    _ = LoadData();
                }
            }, o => SelectedRecipe != null);

            DeleteRecipeCommand = new RelayCommand(async o => await DeleteRecipe(), o => SelectedRecipe != null);

            OpenDetailsCommand = new RelayCommand(o =>
            {
                if (o is int id)
                {
                    Logger.Info("Opening recipe details", new { recipe_id = id });
                    MessageBox.Show($"Детали рецепта ID: {id}");
                }
            });

            OpenRequestsCommand = new RelayCommand(o =>
            {
                Logger.Info("Opening User Requests form");
                MessageBox.Show("Открытие заявок (UserRequestsForm)");
            });

            OpenUsersCommand = new RelayCommand(o =>
            {
                Logger.Info("Opening Users List form");
                MessageBox.Show("Открытие списка пользователей (UsersForm)");
            });

            LogoutCommand = new RelayCommand(o =>
            {
                Logger.Info("Admin logging out");
                new LoginView().Show();
                CloseCurrentWindow();
            });

            _ = LoadData();

            _timer = new DispatcherTimer();
            _timer.Interval = TimeSpan.FromSeconds(10);
            _timer.Tick += async (s, e) => await LoadData();
            _timer.Start();
        }

        private async Task LoadData()
        {
            try
            {
                int? savedId = SelectedRecipe?.Id;

                DataTable dt;

                if (string.IsNullOrWhiteSpace(SearchText))
                {
                    dt = await DatabaseHelper.ExecuteQuery("get_all_recipes");
                }
                else
                {
                    var rpcParams = new { p_key = SearchText };
                    dt = await DatabaseHelper.ExecuteQuery("search_recipes", rpcParams);
                }

                var newItems = new System.Collections.Generic.List<RecipeModel>();
                foreach (DataRow row in dt.Rows)
                {
                    string imgUrl = null;
                    if (row.Table.Columns.Contains("image_url") && row["image_url"] != DBNull.Value)
                    {
                        imgUrl = row["image_url"].ToString();
                        if (string.IsNullOrWhiteSpace(imgUrl)) imgUrl = null;
                    }

                    newItems.Add(new RecipeModel
                    {
                        Id = Convert.ToInt32(row["recipe_id"]),
                        Name = row["recipe_name"].ToString(),
                        ImageUrl = imgUrl,
                        Rating = row.Table.Columns.Contains("average_rating") && row["average_rating"] != DBNull.Value
                                 ? Convert.ToDecimal(row["average_rating"]) : 0
                    });
                }

                switch (SelectedSortIndex)
                {
                    case 1: newItems = newItems.OrderByDescending(x => x.Rating).ToList(); break;
                    case 2: newItems = newItems.OrderBy(x => x.Rating).ToList(); break;
                    case 3: newItems = newItems.OrderBy(x => x.Name).ToList(); break;
                }
                Recipes.Clear();
                foreach (var item in newItems) Recipes.Add(item);
                if (savedId.HasValue)
                {
                    var restoredRecipe = Recipes.FirstOrDefault(r => r.Id == savedId.Value);
                    if (restoredRecipe != null)
                    {
                        SelectedRecipe = restoredRecipe;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to load recipes data", ex);
                System.Diagnostics.Debug.WriteLine($"Ошибка обновления: {ex.Message}");
            }
        }

        private async Task DeleteRecipe()
        {
            var recipeToDelete = SelectedRecipe;

            if (recipeToDelete == null) return;

            _timer.Stop();

            Logger.Info("Delete recipe requested", new { recipe_id = recipeToDelete.Id });

            var result = MessageBox.Show($"Вы уверены, что хотите удалить {recipeToDelete.Name}?",
                "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                var rpcParams = new { p_recipe = recipeToDelete.Id };
                bool ok = await DatabaseHelper.ExecuteNonQuery("delete_recipe", rpcParams);

                if (ok)
                {
                    Logger.Info("Recipe deleted successfully", new { recipe_id = recipeToDelete.Id });
                    MessageBox.Show("Рецепт удален.", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);

                    SelectedRecipe = null;

                    await LoadData();
                }
                else
                {
                    Logger.Error("Failed to delete recipe", null, new { recipe_id = recipeToDelete.Id });
                    MessageBox.Show("Ошибка удаления.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }

            _timer.Start();
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
