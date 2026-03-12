using Microsoft.Win32;
using ProjectDish.Core;
using ProjectDish.MVVM.Models;
using ProjectDish.Services;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using Windows.Devices.Portable;

namespace ProjectDish.MVVM.ViewModels
{
    public class RecipeViewModel : ViewModelBase
    {
        private int _recipeId = -1;
        private string _name;
        private string _description;
        private string _ingredients;
        private CategoryModel _selectedCategory;
        private string _imagePath;
        private string _localFilePath;
        private bool _isBusy;
        private string _windowTitle = "Новый рецепт";

        private readonly StorageService _storageService;
        private const string BucketName = "recipeimages";

        // Категории
        public ObservableCollection<CategoryModel> Categories { get; set; } = new ObservableCollection<CategoryModel>();

        public string WindowTitle
        {
            get => _windowTitle;
            set { _windowTitle = value; OnPropertyChanged(); }
        }

        public string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(); }
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

        public CategoryModel SelectedCategory
        {
            get => _selectedCategory;
            set { _selectedCategory = value; OnPropertyChanged(); }
        }

        public string ImagePath
        {
            get => _imagePath;
            set { _imagePath = value; OnPropertyChanged(); }
        }

        public bool IsBusy
        {
            get => _isBusy;
            set { _isBusy = value; OnPropertyChanged(); }
        }

        public RelayCommand SaveCommand { get; }
        public RelayCommand UploadImageCommand { get; }
        public RelayCommand CancelCommand { get; }

        // Конструктор
        public RecipeViewModel(int recipeId = -1)
        {
            if (DesignerProperties.GetIsInDesignMode(new DependencyObject()))
                return;

            _recipeId = recipeId;
            _storageService = new StorageService();

            if (_recipeId > 0)
            {
                WindowTitle = "Редактирование рецепта";
                Logger.Info("Opening Recipe Form in EDIT mode", new { recipe_id = _recipeId });
            }
            else
            {
                Logger.Info("Opening Recipe Form in NEW mode");
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
        // Загрузка категорий
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
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to load categories", ex);
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
            }
            finally { IsBusy = false; }
        }

        public void HandleImageDrop(string[] files)
        {
            if (files.Length > 0)
            {
                Logger.Info("Image dropped", new { file = files[0] });
                ProcessImageFile(files[0]);
            }
        }

        private void PickImage()
        {
            var dlg = new OpenFileDialog { Filter = "Image Files|*.jpg;*.jpeg;*.png" };
            if (dlg.ShowDialog() == true)
            {
                Logger.Info("Image picked via dialog", new { file = dlg.FileName });
                ProcessImageFile(dlg.FileName);
            }
        }

        private void ProcessImageFile(string filePath)
        {
            var fi = new FileInfo(filePath);
            if (fi.Length > 50 * 1024 * 1024)
            {
                Logger.Warn("Image upload rejected: File too large", new { size_bytes = fi.Length });
                MessageBox.Show("Файл слишком большой (>50MB).");
                return;
            }
            string ext = fi.Extension.ToLowerInvariant();
            if (ext != ".jpg" && ext != ".jpeg" && ext != ".png")
            {
                Logger.Warn("Image upload rejected: Invalid extension", new { extension = ext });
                MessageBox.Show("Только JPG и PNG.");
                return;
            }

            _localFilePath = filePath;
            ImagePath = filePath;
        }
        // Сохранение рецепта
        private async Task SaveRecipe()
        {
            if (IsBusy) return;

            Logger.Info("Save recipe requested", new { name = Name, category = SelectedCategory?.Name });

            if (string.IsNullOrWhiteSpace(Name) || string.IsNullOrWhiteSpace(Description) || string.IsNullOrWhiteSpace(Ingredients))
            {
                Logger.Warn("Save failed: Empty required fields");
                MessageBox.Show("Заполните все поля!"); return;
            }
            if (!IsValidText(Name) || !IsValidText(Description) || !IsValidText(Ingredients))
            {
                Logger.Warn("Save failed: Regex validation error (invalid chars or emojis)");
                MessageBox.Show("Обнаружены недопустимые символы или эмодзи."); return;
            }
            if (SelectedCategory == null)
            {
                Logger.Warn("Save failed: No category selected");
                MessageBox.Show("Выберите категорию."); return;
            }

            // Проверка картинки
            if (string.IsNullOrEmpty(ImagePath) && string.IsNullOrEmpty(_localFilePath))
            {
                if (MessageBox.Show("Нет изображения. Добавить?", "Внимание", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                {
                    PickImage();
                    if (string.IsNullOrEmpty(_localFilePath))
                    {
                        Logger.Info("Save cancelled: User declined to add image");
                        return;
                    }
                }
                else
                {
                    Logger.Info("Save cancelled: Missing image");
                    return;
                }
            }

            IsBusy = true;

            try
            {
                string finalImageUrl = ImagePath;

                if (!string.IsNullOrEmpty(_localFilePath))
                {
                    Logger.Info("Uploading image to storage...");
                    string ext = Path.GetExtension(_localFilePath);
                    string objectPath = $"images/recipes/{Guid.NewGuid():N}{ext}";
                    finalImageUrl = await _storageService.UploadFileAsync(_localFilePath, BucketName, objectPath);
                    Logger.Info("Image uploaded successfully", new { url = finalImageUrl });
                }

                if (_recipeId == -1)
                {
                    var rpcParams = new
                    {
                        p_name = Name,
                        p_desc = Description,
                        p_ingr = Ingredients,
                        p_cat_id = SelectedCategory.Id,
                        p_image_url = finalImageUrl
                    };
                    await DatabaseHelper.ExecuteNonQuery("add_recipe", rpcParams);
                    Logger.Info("New recipe added to DB");
                    MessageBox.Show("Рецепт добавлен!");
                }
                else
                {
                    var updateParams = new
                    {
                        p_id = _recipeId,
                        p_name = Name,
                        p_desc = Description,
                        p_ingr = Ingredients,
                        p_cat = SelectedCategory.Id,
                        p_image_url = finalImageUrl
                    };
                    await DatabaseHelper.ExecuteNonQuery("update_recipe", updateParams);
                    Logger.Info("Recipe updated in DB", new { id = _recipeId });
                    MessageBox.Show("Рецепт обновлен!");
                }

                CloseWindow(Application.Current.Windows.OfType<Window>().SingleOrDefault(w => w.DataContext == this));
            }
            catch (Exception ex)
            {
                Logger.Error("Critical error saving recipe", ex);
                MessageBox.Show($"Ошибка сохранения: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        // Regex валидация
        private bool IsValidText(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return false;
            if (Regex.IsMatch(text, @"\p{Cs}")) return false;
            var pattern = @"^[\p{L}\p{Nd}\s\.\,\-\–\—\!\?\+\-\*\:\;\(\)\[\]\{\}""'`«»\/\\\+\=\%\&\#]+$";
            return Regex.IsMatch(text, pattern);
        }
        private void CloseWindow(Window window) => window?.Close();
    }
}
