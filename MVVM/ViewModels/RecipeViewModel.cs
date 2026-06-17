using Microsoft.Win32;
using ProjectDish.Core;
using ProjectDish.MVVM.Models;
using ProjectDish.Services;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.IO;
using System.Windows;
namespace ProjectDish.MVVM.ViewModels
{
    public class RecipeViewModel : ViewModelBase
    {
        private readonly int _recipeId;
        private readonly int _userId;
        private readonly bool _isRequestMode;
        private readonly StorageService _storageService;
        private const string BucketName = "recipeimages";
        private string _name;
        private string _description;
        private string _ingredients;
        private CategoryModel _selectedCategory;
        private string _imagePath;
        private string _localFilePath;
        private bool _isBusy;
        private string _windowTitle;
        private string _saveButtonText;
        public ObservableCollection<CategoryModel> Categories { get; set; } = new ObservableCollection<CategoryModel>();
        public string WindowTitle { get => _windowTitle; set { _windowTitle = value; OnPropertyChanged(); } }
        public string SaveButtonText { get => _saveButtonText; set { _saveButtonText = value; OnPropertyChanged(); } }
        public string Name { get => _name; set { _name = value; OnPropertyChanged(); } }
        public string Description { get => _description; set { _description = value; OnPropertyChanged(); } }
        public string Ingredients { get => _ingredients; set { _ingredients = value; OnPropertyChanged(); } }
        public CategoryModel SelectedCategory { get => _selectedCategory; set { _selectedCategory = value; OnPropertyChanged(); } }
        public string ImagePath { get => _imagePath; set { _imagePath = value; OnPropertyChanged(); } }
        public bool IsBusy { get => _isBusy; set { _isBusy = value; OnPropertyChanged(); } }
        public RelayCommand SaveCommand { get; }
        public RelayCommand UploadImageCommand { get; }
        public RelayCommand CancelCommand { get; }
        public RecipeViewModel(int recipeId = -1, int userId = -1, bool isRequest = false)
        {
            if (DesignerProperties.GetIsInDesignMode(new DependencyObject())) return;

            _recipeId = recipeId;
            _userId = userId;
            _isRequestMode = isRequest;
            _storageService = new StorageService();
            if (_isRequestMode)
            {
                WindowTitle = "Предложить новый рецепт";
                SaveButtonText = "Отправить на рассмотрение";
                Logger.Info("Opening Recipe Form in USER REQUEST mode", new { user_id = _userId });

                // Проверка авторизации
                if (_userId <= 0)
                {
                    Logger.Warn("User ID is invalid for recipe request", new { user_id = _userId });
                    AppDialog.Show("Ошибка: Пользователь не авторизован.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    CancelCommand.Execute(null);
                    return;
                }
            }
            else if (_recipeId > 0)
            {
                WindowTitle = "Редактирование рецепта";
                SaveButtonText = "Сохранить изменения";
                Logger.Info("Opening Recipe Form in ADMIN EDIT mode", new { recipe_id = _recipeId });
            }
            else
            {
                WindowTitle = "Новый рецепт";
                SaveButtonText = "Добавить рецепт";
                Logger.Info("Opening Recipe Form in ADMIN NEW mode");
            }

            SaveCommand = new RelayCommand(async o => await SaveRecipe());
            UploadImageCommand = new RelayCommand(o => PickImage());
            CancelCommand = new RelayCommand(o => CloseWindow(o as Window));

            _ = InitializeAsync();
        }
        private async Task InitializeAsync()
        {
            await LoadCategories();
            if (_recipeId > 0)
            {
                await LoadRecipeData();
            }
        }
        private async Task LoadCategories()
        {
            try
            {
                var dt = await DatabaseHelper.ExecuteQuery("get_categories");
                Categories.Clear();
                foreach (DataRow row in dt.Rows)
                {
                    Categories.Add(new CategoryModel
                    {
                        Id = Convert.ToInt32(row["category_id"]),
                        Name = row["category_name"].ToString()
                    });
                }
                if (_recipeId == -1 && Categories.Count > 0) SelectedCategory = Categories[0];
                Logger.Info($"Categories loaded: {Categories.Count}");
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to load categories", ex);
                AppDialog.Show("Не удалось загрузить категории.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private async Task LoadRecipeData()
        {
            IsBusy = true;
            try
            {
                var dt = await DatabaseHelper.ExecuteQuery("get_recipe_by_id", new { p_recipe_id = _recipeId });
                if (dt.Rows.Count > 0)
                {
                    var r = dt.Rows[0];
                    Name = r["recipe_name"].ToString();
                    Description = r["description"].ToString();
                    Ingredients = r["ingredients"].ToString();
                    ImagePath = r.Table.Columns.Contains("image_url") ? r["image_url"].ToString() : null;

                    if (r.Table.Columns.Contains("category_id") && r["category_id"] != DBNull.Value)
                    {
                        int catId = Convert.ToInt32(r["category_id"]);
                        SelectedCategory = Categories.FirstOrDefault(c => c.Id == catId);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to load recipe data", ex, new { recipe_id = _recipeId });
                AppDialog.Show("Не удалось загрузить данные рецепта.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally { IsBusy = false; }
        }
        public void HandleImageDrop(string[] files)
        {
            if (files.Length > 0) ProcessImageFile(files[0]);
        }
        private void PickImage()
        {
            var dlg = new OpenFileDialog { Filter = "Image Files|*.jpg;*.jpeg;*.png" };
            if (dlg.ShowDialog() == true) ProcessImageFile(dlg.FileName);
        }
        private void ProcessImageFile(string filePath)
        {
            var fi = new FileInfo(filePath);
            if (fi.Length > 50 * 1024 * 1024)
            {
                AppDialog.Show("Файл слишком большой (>50MB).", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error); return;
            }
            string ext = fi.Extension.ToLowerInvariant();
            if (ext != ".jpg" && ext != ".jpeg" && ext != ".png")
            {
                AppDialog.Show("Только JPG и PNG.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error); return;
            }
            _localFilePath = filePath;
            ImagePath = filePath;
            Logger.Info("Image selected", new { path = filePath, size = fi.Length });
        }
        // Сохранение рецепта
        private async Task SaveRecipe()
        {
            if (IsBusy) return;

            if (!ValidateInput()) return;

            if (string.IsNullOrEmpty(ImagePath) && string.IsNullOrEmpty(_localFilePath))
            {
                AppDialog.Show("Необходимо добавить изображение.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error); return;
            }
            IsBusy = true;
            try
            {
                string finalImageUrl = ImagePath;

                // Загрузка изображения
                if (!string.IsNullOrEmpty(_localFilePath) && !_localFilePath.StartsWith("http"))
                {
                    Logger.Info("Uploading image to storage...");
                    string ext = Path.GetExtension(_localFilePath);
                    string objectPath = $"images/recipes/{Guid.NewGuid():N}{ext}";

                    try
                    {
                        finalImageUrl = await _storageService.UploadFileAsync(_localFilePath, BucketName, objectPath);
                        Logger.Info("Image uploaded successfully", new { url = finalImageUrl });
                    }
                    catch (Exception uploadEx)
                    {
                        Logger.Error("Image upload failed", uploadEx);
                        AppDialog.Show($"Ошибка загрузки изображения: {uploadEx.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                }
                bool success = false;

                if (_isRequestMode)
                {
                    success = await SubmitUserRequest(finalImageUrl);
                }
                else if (_recipeId == -1)
                {
                    success = await AddRecipeAsAdmin(finalImageUrl);
                }
                else
                {
                    success = await UpdateRecipeAsAdmin(finalImageUrl);
                }

                if (success)
                {
                    CloseWindow(Application.Current.Windows.OfType<Window>().SingleOrDefault(w => w.DataContext == this));
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Critical error saving recipe/request", ex);
                AppDialog.Show($"Ошибка сохранения: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }
        private async Task<bool> SubmitUserRequest(string imageUrl)
        {
            try
            {
                var rpcParams = new
                {
                    p_user = _userId,
                    p_name = Name,
                    p_desc = Description,
                    p_ingr = Ingredients,
                    p_cat_id = SelectedCategory.Id,
                    p_image_url = imageUrl
                };

                Logger.Info("Submitting user recipe request", new { user_id = _userId, name = Name, category = SelectedCategory?.Id });

                bool result = await DatabaseHelper.ExecuteNonQuery("submit_user_recipe", rpcParams);

                if (result)
                {
                    Logger.Info("User recipe request submitted successfully");
                    AppDialog.Show("Рецепт отправлен на рассмотрение!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                    return true;
                }
                else
                {
                    Logger.Error("User recipe request failed - RPC returned false");
                    AppDialog.Show("Ошибка при отправке рецепта. Попробуйте позже.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    return false;
                }
            }
            catch (Exception ex)
            {
                Logger.Error("SubmitUserRequest exception", ex);
                AppDialog.Show($"Ошибка: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }
        private async Task<bool> AddRecipeAsAdmin(string imageUrl)
        {
            try
            {
                var rpcParams = new
                {
                    p_name = Name,
                    p_desc = Description,
                    p_ingr = Ingredients,
                    p_cat_id = SelectedCategory.Id,
                    p_image_url = imageUrl
                };
                bool result = await DatabaseHelper.ExecuteNonQuery("add_recipe", rpcParams);
                if (result)
                {
                    Logger.Info("New recipe added by admin", new { name = Name });
                    AppDialog.Show("Рецепт добавлен!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                    return true;
                }
                else
                {
                    Logger.Error("Add recipe failed - RPC returned false");
                    AppDialog.Show("Ошибка при добавлении рецепта.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    return false;
                }
            }
            catch (Exception ex)
            {
                Logger.Error("AddRecipeAsAdmin exception", ex);
                AppDialog.Show($"Ошибка: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }
        private async Task<bool> UpdateRecipeAsAdmin(string imageUrl)
        {
            try
            {
                var updateParams = new
                {
                    p_id = _recipeId,
                    p_name = Name,
                    p_desc = Description,
                    p_ingr = Ingredients,
                    p_cat = SelectedCategory.Id,
                    p_image_url = imageUrl
                };
                bool result = await DatabaseHelper.ExecuteNonQuery("update_recipe", updateParams);

                if (result)
                {
                    Logger.Info("Recipe updated by admin", new { id = _recipeId });
                    AppDialog.Show("Рецепт обновлен!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                    return true;
                }
                else
                {
                    Logger.Error("Update recipe failed - RPC returned false");
                    AppDialog.Show("Ошибка при обновлении рецепта.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    return false;
                }
            }
            catch (Exception ex)
            {
                Logger.Error("UpdateRecipeAsAdmin exception", ex);
                AppDialog.Show($"Ошибка: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }
        private bool ValidateInput()
        {
            if (string.IsNullOrWhiteSpace(Name))
            { AppDialog.Show("Введите название рецепта.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning); return false; }
            if (string.IsNullOrWhiteSpace(Description))
            { AppDialog.Show("Введите описание.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning); return false; }
            if (string.IsNullOrWhiteSpace(Ingredients))
            { AppDialog.Show("Введите ингредиенты.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning); return false; }
            if (SelectedCategory == null)
            { AppDialog.Show("Выберите категорию.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning); return false; }
            // Валидация -  проверка на пустоту и длину
            if (Name.Length > 200)
            { AppDialog.Show("Название слишком длинное (макс. 200 символов).", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning); return false; }

            return true;
        }
        private void CloseWindow(Window window) => window?.Close();
    }
}