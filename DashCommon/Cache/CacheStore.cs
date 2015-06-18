//     Copyright (c) Microsoft Corporation.  All rights reserved.

using System;
using System.Configuration;
using System.Threading.Tasks;
using Microsoft.Dash.Common.Diagnostics;
using Microsoft.Dash.Common.Utils;
using Newtonsoft.Json;
using StackExchange.Redis;

namespace Microsoft.Dash.Common.Cache
{
    public static class CacheStore
    {
        private static readonly ConnectionMultiplexer Connection;
        private const bool UseSsl = true;

        static CacheStore()
        {
            var redisEndpointUrl = AzureUtils.GetConfigSetting("CacheRedisEndpointUrl", String.Empty);
            if (String.IsNullOrEmpty(redisEndpointUrl))
            {
                throw new ConfigurationErrorsException("redisEndpointUrl");
            }

            var redisPassword = AzureUtils.GetConfigSetting("CacheRedisPassword", String.Empty);
            if (String.IsNullOrEmpty(redisPassword))
            {
                throw new ConfigurationErrorsException("redisEndpointUrl");
            }

            var connectionString = String.Format("{0},abortConnect=false,ssl={1},password={2},allowAdmin=true", redisEndpointUrl, UseSsl, redisPassword);
            DashTrace.TraceInformation(new TraceMessage
            {
                Message = "Redis ConnectionString =" + connectionString,
            });

            Connection = ConnectionMultiplexer.Connect(connectionString);
        }

        public static async Task<bool> ExistsAsync(string key)
        {
            return await Connection.GetDatabase().KeyExistsAsync(key);
        }

        public static async Task<T> GetAsync<T>(string key)
        {
            var json = await Connection.GetDatabase().StringGetAsync(key);
            return Deserialize<T>(json);
        }

        public static async Task<bool> SetAsync<T>(string key, T value, TimeSpan expiredIn)
        {
            return await Connection.GetDatabase().StringSetAsync(key, Serialize(value), expiredIn);
        }

        public static async Task<bool> DeleteAsync(string key)
        {
            return await Connection.GetDatabase().KeyDeleteAsync(key);
        }

        internal static string Serialize<T>(T obj)
        {
            return JsonConvert.SerializeObject(obj);
        }

        internal static T Deserialize<T>(string json)
        {
            if (String.IsNullOrEmpty(json))
            {
                return default(T);
            }

            var settings = new JsonSerializerSettings
            {
                MissingMemberHandling = MissingMemberHandling.Ignore
            };

            return JsonConvert.DeserializeObject<T>(json, settings);
        }
    }
}
