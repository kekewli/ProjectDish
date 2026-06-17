using System.Runtime.Caching;
namespace ProjectDish.Services
{
    public class CacheService
    {
        private readonly MemoryCache _cache = MemoryCache.Default;
        private static readonly Lazy<CacheService> _instance = new Lazy<CacheService>(() => new CacheService());
        public static CacheService Instance => _instance.Value;
        private CacheService() { }
        public T Get<T>(string key) where T : class
        {
            return _cache[key] as T;
        }
        public void Set<T>(string key, T value, int expirationMinutes = 5) where T : class
        {
            if (value == null)
                return;

            var policy = new CacheItemPolicy
            {
                AbsoluteExpiration = DateTimeOffset.Now.AddMinutes(expirationMinutes)
            };
            _cache.Set(key, value, policy);
        }
        public void Remove(string key)
        {
            _cache.Remove(key);
        }
    }
}
