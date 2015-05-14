using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using StackExchange.Redis;
using System;
using System.Threading.Tasks;

namespace Microsoft.Dash.Common.Utils
{
    public class CacheStore : IDisposable
    {
        private static Lazy<ConnectionMultiplexer> _lazyConnection;
        private readonly string _connectionString;
        private readonly Func<IDatabase> _getDatabase = () => Connection.GetDatabase();

        private static ConnectionMultiplexer Connection
        {
            get
            {
                return _lazyConnection.Value;
            }
        }

        public CacheStore(string redisEndpointUrl = null, string redisPassword = null, bool useSsl = true)
        {
            if (String.IsNullOrEmpty(redisEndpointUrl))
            {
                redisEndpointUrl = AzureUtils.GetConfigSetting("CacheRedisEndpointUrl", String.Empty);
            }

            if (String.IsNullOrEmpty(redisPassword))
            {
                redisPassword = AzureUtils.GetConfigSetting("CacheRedisPassword", String.Empty);
            }

            _connectionString = String.Format("{0},abortConnect=false,ssl={1},password={2},allowAdmin=true", redisEndpointUrl, useSsl, redisPassword);

            _lazyConnection = new Lazy<ConnectionMultiplexer>(() => ConnectionMultiplexer.Connect(_connectionString));
        }

        internal CacheStore(Func<IDatabase> getDatabase)
        {
            this._getDatabase = getDatabase;
        }

        public async Task<T> GetOrSetAsync<T>(string key, TimeSpan expiredIn, Func<Task<T>> method)
        {
            if (await ExistsAsync(key))
            {
                return await GetAsync<T>(key);
            }

            var item = await method();

            await SetAsync(key, item, expiredIn);

            return item;
        }

        public async Task<bool> ExistsAsync(string key)
        {
            return await _getDatabase().KeyExistsAsync(key);
        }

        public async Task<T> GetAsync<T>(string key)
        {
            var byteStream = await _getDatabase().StringGetAsync(key);
            return Deserialize<T>(byteStream);
        }

        public async Task<bool> SetAsync<T>(string key, T value, TimeSpan expiredIn)
        {
            return await _getDatabase().StringSetAsync(key, Serialize(value), expiredIn);
        }

        public async Task<bool> DeleteAsync(string key)
        {
            return await _getDatabase().KeyDeleteAsync(key);
        }

        public void Dispose()
        {
            if (Connection != null)
            {
                Connection.Dispose();
            }
        }

        internal byte[] Serialize(object o)
        {
            if (o == null)
            {
                return null;
            }

            var binaryFormatter = new BinaryFormatter();
            using (var memoryStream = new MemoryStream())
            {
                binaryFormatter.Serialize(memoryStream, o);
                byte[] objectDataAsStream = memoryStream.ToArray();
                return objectDataAsStream;
            }
        }

        internal T Deserialize<T>(byte[] stream)
        {
            if (stream == null)
            {
                return default(T);
            }

            var binaryFormatter = new BinaryFormatter();
            using (var memoryStream = new MemoryStream())
            {
                var result = (T) binaryFormatter.Deserialize(memoryStream);
                return result;
            }
        }
    }
}
