//     Copyright (c) Microsoft Corporation.  All rights reserved.

using System;
using System.IO;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Dash.Common.Utils;
using StackExchange.Redis;

namespace Microsoft.Dash.Common.Cache
{
    public class CacheStore
    {
        private static Lazy<ConnectionMultiplexer> _lazyConnection;
        private readonly string _connectionString;

        private IDatabase GetDatabase()
        {
            return _lazyConnection.Value.GetDatabase();
        }

        public CacheStore(string redisEndpointUrl = null, string redisPassword = null, bool useSsl = true)
        {
            redisEndpointUrl = redisEndpointUrl ?? AzureUtils.GetConfigSetting("CacheRedisEndpointUrl", String.Empty);
            if (String.IsNullOrEmpty(redisEndpointUrl))
            {
                throw new ArgumentNullException("redisEndpointUrl");
            }

            redisPassword = redisPassword ?? AzureUtils.GetConfigSetting("CacheRedisPassword", String.Empty);
            if (String.IsNullOrEmpty(redisPassword))
            {
                throw new ArgumentNullException("redisEndpointUrl");
            }

            _connectionString = String.Format("{0},abortConnect=false,ssl={1},password={2},allowAdmin=true", redisEndpointUrl, useSsl, redisPassword);
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

        internal string Serialize<T>(T obj)
        {
            var serializer = new DataContractJsonSerializer(typeof(T));

            using (var memoryStream = new MemoryStream())
            {
                serializer.WriteObject(memoryStream, obj);
                var bytes = memoryStream.ToArray();
                return Encoding.UTF8.GetString(bytes, 0, bytes.Length);
            }
        }

        internal T Deserialize<T>(string json)
        {
            if (json == null)
            {
                return default(T);
            }

            var serializer = new DataContractJsonSerializer(typeof(T));
            using (var memoryStream = new MemoryStream(Encoding.ASCII.GetBytes(json)))
            {
                return (T)serializer.ReadObject(memoryStream);
            }
        }
    }
}
