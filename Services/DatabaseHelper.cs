using System.Configuration;
using Supabase;
using System.Net.NetworkInformation;
using System.Windows;
using System.Data;
using Newtonsoft.Json;

namespace ProjectDish.Services
{
    public static class DatabaseHelper
    {
        public static string SupabaseUrl { get; private set; }
        public static string SupabaseKey { get; private set; }
        public static Supabase.Client Client { get; private set; }

        private static bool _offlineMessageShown = false;
        public static bool ForceEnsureClientInitFailureForTests { get; set; } = false;
        public static bool SuppressUiDialogsForTests { get; set; } = false;
        static DatabaseHelper()
        {
            try
            {
                SupabaseUrl = ConfigurationManager.AppSettings["SupabaseUrl"];
                SupabaseKey = ConfigurationManager.AppSettings["SupabaseKey"];

                if (string.IsNullOrEmpty(SupabaseUrl) || string.IsNullOrEmpty(SupabaseKey))
                {
                    Logger.Error("Configuration error: Supabase keys missing in App.config", null, new { key_status = "Missing" });
                }
                else
                {
                    Logger.Info("Configuration loaded successfully", new { source = "App.config" });
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to read App.config", ex);
            }
        }
        public static void SetCredentials(string url, string key)
        {
            SupabaseUrl = url;
            SupabaseKey = key;
        }
        public static Supabase.Client GetClient() => Client;
        // Проверка интернет-соединения
        private static bool HasInternet()
        {
            if (!NetworkInterface.GetIsNetworkAvailable())
                return false;

            try
            {
                using (var ping = new Ping())
                {
                    var reply = ping.Send("8.8.8.8", 3000);
                    if (reply.Status == IPStatus.Success)
                        return true;
                }
            }
            catch
            {
            }

            try
            {
                if (string.IsNullOrEmpty(SupabaseUrl)) return false;

                using (var client = new System.Net.Http.HttpClient())
                {
                    client.Timeout = TimeSpan.FromSeconds(3);
                    var response = client.GetAsync(SupabaseUrl).GetAwaiter().GetResult();
                    return response.IsSuccessStatusCode;
                }
            }
            catch
            {
                return false;
            }
        }
        public static void ResetForTests()
        {
            Client = null;
            _offlineMessageShown = false;
            ForceEnsureClientInitFailureForTests = false;
            SuppressUiDialogsForTests = true;
        }
        // Инициализация клиента Supabase
        private static async Task<bool> EnsureClientAsync()
        {
            if (System.ComponentModel.DesignerProperties.GetIsInDesignMode(new DependencyObject()))
                return false;

            if (string.IsNullOrEmpty(SupabaseUrl) || string.IsNullOrEmpty(SupabaseKey))
            {
                if (!_offlineMessageShown)
                {
                    string msg = "Ошибка конфигурации: Не найдены ключи подключения к БД.";
                    if (!SuppressUiDialogsForTests)
                        AppDialog.Show(msg, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    Logger.Error("Connection aborted: Missing configuration keys");
                    _offlineMessageShown = true;
                }
                return false;
            }

            if (Client != null) return true;

            if (!HasInternet())
            {
                if (!_offlineMessageShown)
                {
                    if (!SuppressUiDialogsForTests)
                        AppDialog.Show("Отсутствует подключение к интернету.", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                    Logger.Warn("Connection aborted: No internet access");
                    _offlineMessageShown = true;
                }
                return false;
            }
            try
            {
                if (ForceEnsureClientInitFailureForTests)
                    throw new InvalidOperationException("Forced failure for tests");

                var options = new Supabase.SupabaseOptions
                {
                    AutoRefreshToken = true,
                    AutoConnectRealtime = true
                };

                Client = new Supabase.Client(SupabaseUrl, SupabaseKey, options);
                await Client.InitializeAsync();

                Logger.Info("Supabase client initialized successfully", new { url = SupabaseUrl });
                _offlineMessageShown = false;
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error("CRITICAL: Failed to initialize Supabase client", ex);
                if (!SuppressUiDialogsForTests)
                    AppDialog.Show($"Ошибка подключения к серверу: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                Client = null;
                return false;
            }
        }
        // Вызов RPC функции без возвращаемого значения
        public static async Task<bool> ExecuteNonQuery(string functionName, object parameters = null)
        {
            if (!await EnsureClientAsync()) return false;

            try
            {
                await Client.Rpc(functionName, parameters);
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error("RPC ExecuteNonQuery failed", ex, new { rpc_function = functionName });
                return false;
            }
        }
        // Вызов RPC функции с возвратом таблицы данных
        public static async Task<DataTable> ExecuteQuery(string functionName, object parameters = null)
        {
            var dt = new DataTable();
            if (!await EnsureClientAsync()) return dt;
            try
            {
                var result = await Client.Rpc(functionName, parameters);
                var content = result.Content;

                if (string.IsNullOrEmpty(content)) return dt;

                dt = JsonConvert.DeserializeObject<DataTable>(content) ?? new DataTable();
            }
            catch (Exception ex)
            {
                Logger.Error("RPC ExecuteQuery failed", ex, new { rpc_function = functionName });
            }
            return dt;
        }
        // Вызов RPC функции с возвратом дробного числа
        public static async Task<decimal?> ExecuteRpcScalarAsync(string functionName, object parameters = null)
        {
            if (!await EnsureClientAsync()) return null;

            try
            {
                var result = await Client.Rpc(functionName, parameters);

                if (string.IsNullOrEmpty(result.Content)) return null;

                return JsonConvert.DeserializeObject<decimal?>(result.Content);
            }
            catch (Exception ex)
            {
                Logger.Error("RPC ExecuteRpcScalarAsync failed", ex, new { rpc_function = functionName });
                return null;
            }
        }
        // Вызов RPC функции с возвратом целого числа
        public static async Task<int> ExecuteNonQueryWithReturnValueAsync(string functionName, object parameters = null)
        {
            if (!await EnsureClientAsync()) return -1;
            try
            {
                var result = await Client.Rpc(functionName, parameters);

                if (string.IsNullOrEmpty(result.Content)) return -1;

                if (int.TryParse(result.Content, out int val))
                {
                    return val;
                }
                return JsonConvert.DeserializeObject<int>(result.Content);
            }
            catch (Exception ex)
            {
                Logger.Error("RPC ExecuteNonQueryWithReturnValueAsync failed", ex, new { rpc_function = functionName });
                return -1;
            }
        }
    }
}
