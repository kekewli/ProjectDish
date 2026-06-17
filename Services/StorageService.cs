using System.Configuration;
using System.IO;
using System.Net.Http.Headers;
using System.Net.Http;
namespace ProjectDish.Services
{
    public class StorageService
    {
        private static readonly HttpClient _http = new HttpClient();
        private readonly string _supabaseUrl;
        private readonly string _apiKey;
        private readonly string _defaultBucket;
        public StorageService()
        {
            _supabaseUrl = ConfigurationManager.AppSettings["SupabaseUrl"]?.TrimEnd('/');
            _apiKey = ConfigurationManager.AppSettings["SupabaseKey"];
            _defaultBucket = ConfigurationManager.AppSettings["StorageBucket"] ?? "recipeimages";

            if (string.IsNullOrWhiteSpace(_supabaseUrl) || string.IsNullOrWhiteSpace(_apiKey))
                throw new InvalidOperationException("SupabaseUrl или SupabaseKey не настроены в App.config.");
        }
        public string DefaultBucket => _defaultBucket;
        public async Task<string> UploadFileAsync(string localFilePath, string bucket, string objectPath)
        {
            bucket ??= _defaultBucket;
            if (!File.Exists(localFilePath)) throw new FileNotFoundException(localFilePath);
            var requestUri = $"{_supabaseUrl}/storage/v1/object/{bucket}/{objectPath}";
            using var fs = File.OpenRead(localFilePath);
            using var content = new StreamContent(fs);
            content.Headers.ContentType = new MediaTypeHeaderValue(GetMimeType(localFilePath));
            using var request = new HttpRequestMessage(HttpMethod.Put, requestUri) { Content = content };
            request.Headers.Add("Authorization", $"Bearer {_apiKey}");
            request.Headers.Add("apikey", _apiKey);
            var resp = await _http.SendAsync(request);
            if (!resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync();
                throw new Exception($"Storage upload failed: {resp.StatusCode}: {body}");
            }
            var publicUrl = $"{_supabaseUrl}/storage/v1/object/public/{bucket}/{Uri.EscapeDataString(objectPath)}";
            return publicUrl;
        }
        public async Task<bool> DeleteFileAsync(string bucket, string objectPath)
        {
            var requestUri = $"{_supabaseUrl}/storage/v1/object/{bucket}/{objectPath}";
            using var req = new HttpRequestMessage(HttpMethod.Delete, requestUri);
            req.Headers.Add("Authorization", $"Bearer {_apiKey}");
            req.Headers.Add("apikey", _apiKey);

            var resp = await _http.SendAsync(req);
            return resp.IsSuccessStatusCode;
        }
        public string GetObjectPathFromPublicUrl(string publicUrl, string bucket = null)
        {
            if (string.IsNullOrWhiteSpace(publicUrl)) return null;
            bucket ??= _defaultBucket;
            var marker = $"/storage/v1/object/public/{bucket}/";
            var idx = publicUrl.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (idx < 0) return null;
            return Uri.UnescapeDataString(publicUrl.Substring(idx + marker.Length));
        }
        private static string GetMimeType(string file)
        {
            var ext = Path.GetExtension(file).ToLowerInvariant();
            return ext switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".gif" => "image/gif",
                ".bmp" => "image/bmp",
                _ => "application/octet-stream"
            };
        }
    }
}
