using System.Diagnostics;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using Microsoft.Dash.Common.Utils;
using StackExchange.Redis;
using System;
using System.Threading.Tasks;

namespace Microsoft.Dash.Common.Cache
{
    public class CacheStore : IDisposable
    {
        private static Lazy<ConnectionMultiplexer> _lazyConnection;
        private readonly string _connectionString;
        internal Func<IDatabase> GetDatabase = () => Connection.GetDatabase();

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
            Debug.WriteLine(_connectionString);
            _lazyConnection = new Lazy<ConnectionMultiplexer>(() => ConnectionMultiplexer.Connect(_connectionString));
        }

        public async Task<bool> ExistsAsync(string key)
        {
            return await GetDatabase().KeyExistsAsync(key);
        }

        public async Task<T> GetAsync<T>(string key)
        {
            var byteStream = await GetDatabase().StringGetAsync(key);
            return Deserialize<T>(byteStream);
        }

        public async Task<bool> SetAsync<T>(string key, T value, TimeSpan expiredIn)
        {
            return await GetDatabase().StringSetAsync(key, Serialize(value), expiredIn);
        }

        public async Task<bool> DeleteAsync(string key)
        {
            return await GetDatabase().KeyDeleteAsync(key);
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
            using (var memoryStream = new MemoryStream(stream))
            {
                var result = (T) binaryFormatter.Deserialize(memoryStream);
                return result;
            }
        }
    }
}
