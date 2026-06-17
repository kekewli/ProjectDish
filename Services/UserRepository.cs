using ProjectDish.MVVM.Models;
using System.Data;

namespace ProjectDish.Services
{
    internal class UserRepository
    {
        private static readonly Lazy<UserRepository> _instance = new Lazy<UserRepository>(() => new UserRepository());
        private const string UsersCacheKey = "AllUsers";
        public static UserRepository Instance => _instance.Value;
        private UserRepository() { }
        public async Task<List<UserModel>> GetUsersAsync(bool forceRefresh = false)
        {
            if (!forceRefresh)
            {
                var cachedUsers = CacheService.Instance.Get<List<UserModel>>(UsersCacheKey);
                if (cachedUsers != null)
                {
                    Logger.Info("Users loaded from cache.");
                    return cachedUsers;
                }
            }
            Logger.Info($"Fetching users from database (forceRefresh={forceRefresh})");
            var usersFromDb = await FetchFromDatabaseAsync();
            if (usersFromDb != null)
            {
                CacheService.Instance.Set(UsersCacheKey, usersFromDb, 5);
                Logger.Info($"Users cached. Count: {usersFromDb.Count}");
            }
            return usersFromDb;
        }
        public void InvalidateCache()
        {
            CacheService.Instance.Remove(UsersCacheKey);
            Logger.Info("Users cache invalidated.");
        }
        private static string GetString(DataRow row, string columnName)
        {
            if (!row.Table.Columns.Contains(columnName) || row[columnName] == DBNull.Value)
                return string.Empty;

            return row[columnName]?.ToString() ?? string.Empty;
        }
        private static int GetInt(DataRow row, string columnName, int defaultValue = 0)
        {
            if (!row.Table.Columns.Contains(columnName) || row[columnName] == DBNull.Value)
                return defaultValue;

            return Convert.ToInt32(row[columnName]);
        }
        private async Task<List<UserModel>> FetchFromDatabaseAsync()
        {
            try
            {
                Logger.Info("Fetching users from database...");
                DataTable dt = await DatabaseHelper.ExecuteQuery("get_all_users", new { });
                var items = new List<UserModel>();
                if (dt == null || dt.Rows.Count == 0)
                {
                    Logger.Warn("get_all_users returned empty result.");
                    return items;
                }
                foreach (DataRow row in dt.Rows)
                {
                    try
                    {
                        items.Add(new UserModel
                        {
                            Id = GetInt(row, "user_id"),
                            Username = GetString(row, "user_name"),
                            Email = GetString(row, "email"),
                            RoleId = GetInt(row, "role_id"),
                            RoleName = GetString(row, "role_name"),
                            IsBlocked = GetInt(row, "is_blocked", 0)
                        });
                    }
                    catch (Exception rowEx)
                    {
                        Logger.Error("Failed to map user row", rowEx);
                    }
                }
                Logger.Info($"Successfully fetched {items.Count} users from DB.");
                return items;
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to fetch users from database", ex);
                return null;
            }
        }
    }
}
