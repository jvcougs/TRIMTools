using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Configuration;

namespace TRIMAuditExtract
{
    internal class ConfigTools
    {
        public static string GetAppSetting(string key)
        {
            string result = "Not found";
            try
            {
                var appSettings = ConfigurationManager.AppSettings;
                result = appSettings[key] ?? "Not found";
            }
            catch
            {
                Console.WriteLine("Error reading app settings for key: " + key);
            }
            return result;
        }
    }
}
