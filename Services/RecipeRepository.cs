using ProjectDish.MVVM.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;

namespace ProjectDish.Services
{
    internal class RecipeRepository
    {
        private static readonly Lazy<RecipeRepository> _instance = new Lazy<RecipeRepository>(() => new RecipeRepository());
        private const string RecipesCacheKey = "AllRecipes";

        public static RecipeRepository Instance => _instance.Value;

        private RecipeRepository() { }

        // Основной метод для получения рецептов
        public async Task<List<RecipeModel>> GetRecipesAsync(bool forceRefresh = false)
        {
            if (!forceRefresh)
            {
                var cachedRecipes = CacheService.Instance.Get<List<RecipeModel>>(RecipesCacheKey);
                if (cachedRecipes != null)
                {
                    Logger.Info("Recipes loaded from cache.");
                    return cachedRecipes;
                }
            }

            var recipesFromDb = await FetchFromDatabaseAsync();

            if (recipesFromDb != null)
            {
                CacheService.Instance.Set(RecipesCacheKey, recipesFromDb);
            }

            return recipesFromDb;
        }

        // Метод для принудительной очистки кэша 
        public void InvalidateCache()
        {
            CacheService.Instance.Remove(RecipesCacheKey);
        }

        // Внутренний метод для загрузки из БД 
        private async Task<List<RecipeModel>> FetchFromDatabaseAsync()
        {
            try
            {
                Logger.Info("Fetching recipes from database...");
                DataTable dt = await DatabaseHelper.ExecuteQuery("get_all_recipes");

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
                Logger.Info($"Successfully fetched {items.Count} recipes from DB.");
                return items;
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to fetch recipes from database", ex);
                return null;
            }
        }
    }
}
