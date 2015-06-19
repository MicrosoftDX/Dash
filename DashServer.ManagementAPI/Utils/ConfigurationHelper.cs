using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web.Configuration;

namespace DashServer.ManagementAPI.Utils
{
    public static class ConfigurationHelper
    {
        public static string GetSetting(string setting)
        {
            var configuration = WebConfigurationManager.OpenWebConfiguration("~/");
            return configuration.AppSettings.Settings[setting].Value;
        }
    }
}
