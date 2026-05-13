using ProjectDish.MVVM.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;

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

            Logger.Info("Fetching users from database (forceRefresh=" + forceRefresh + ")");
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

        private async Task<List<UserModel>> FetchFromDatabaseAsync()
        {
            try
            {
                Logger.Info("Fetching users from database...");
                DataTable dt = await DatabaseHelper.ExecuteQuery("get_all_users");

                var items = new List<UserModel>();
                foreach (DataRow row in dt.Rows)
                {
                    items.Add(new UserModel
                    {
                        Id = Convert.ToInt32(row["user_id"]),
                        Username = row["user_name"].ToString(),
                        Email = row["email"].ToString(),
                        RoleId = Convert.ToInt32(row["role_id"]),
                        RoleName = row["role_name"].ToString()
                    });
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
