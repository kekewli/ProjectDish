using ProjectDish.MVVM.Models;
using System;
using System.Data;
using System.Threading.Tasks;

namespace ProjectDish.Services
{
    internal class RecipeDetailsRepository
    {
        // Метод получения деталей рецепта с кэшированием
        public async Task<DataRow> GetRecipeDetailsAsync(int recipeId, bool forceRefresh = false)
        {
            string cacheKey = $"RecipeDetails_{recipeId}";

            if (!forceRefresh)
            {
                var cachedData = CacheService.Instance.Get<DataRow>(cacheKey);
                if (cachedData != null)
                {
                    Logger.Info("Recipe details loaded from cache", new { recipe_id = recipeId });
                    return cachedData;
                }
            }

            try
            {
                Logger.Info("Fetching recipe details from DB", new { recipe_id = recipeId });
                var dt = await DatabaseHelper.ExecuteQuery("get_recipe_details", new { p_id = recipeId });
                if (dt.Rows.Count > 0)
                {
                    var row = dt.Rows[0];
                    CacheService.Instance.Set(cacheKey, row, 1); 
                    return row;
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to fetch recipe details from DB", ex, new { recipe_id = recipeId });
            }
            return null;
        }

        // Метод для очистки кэша конкретного рецепта
        public void InvalidateCache(int recipeId)
        {
            CacheService.Instance.Remove($"RecipeDetails_{recipeId}");
        }
    }
}
