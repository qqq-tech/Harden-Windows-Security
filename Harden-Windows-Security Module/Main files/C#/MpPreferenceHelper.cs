using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Management;

namespace HardeningModule
{
    public static class MpPreferenceHelper
    {
        // Get the MpPreference from the MSFT_MpPreference WMI class and returns it as a dictionary
        public static Dictionary<string, object> GetMpPreference()
        {
            try
            {
                // Defining the WMI query to retrieve the MpPreference
                string namespaceName = "ROOT\\Microsoft\\Windows\\Defender";
                string className = "MSFT_MpPreference";
                string queryString = $"SELECT * FROM {className}";

                // Execute the query
                ManagementObjectSearcher searcher = new ManagementObjectSearcher(namespaceName, queryString);
                ManagementObjectCollection results = searcher.Get();

                // Return the first result if there are any
                if (results.Count > 0)
                {
                    var result = results.Cast<ManagementBaseObject>().FirstOrDefault();
                    return ConvertToDictionary(result);
                }
                else
                {
                    return null;
                }
            }
            catch (ManagementException ex)
            {
                string errorMessage = $"WMI query for 'MSFT_MpPreference' failed: {ex.Message}";
                throw new HardeningModule.PowerShellExecutionException(errorMessage, ex);
            }
        }

        // Convert the ManagementBaseObject to a dictionary
        private static Dictionary<string, object> ConvertToDictionary(ManagementBaseObject managementObject)
        {
            // Creating a dictionary to store the properties of the ManagementBaseObject
            Dictionary<string, object> dictionary = new Dictionary<string, object>();

            // Iterating through the properties of the ManagementBaseObject and adding them to the dictionary
            foreach (var property in managementObject.Properties)
            {
                // Check if the value of the property is in DMTF datetime format
                // Properties such as SignatureScheduleTime use that format
                if (property.Type == CimType.DateTime && property.Value is string dmtfTime)
                {
                    // Convert DMTF datetime format to TimeSpan
                    dictionary[property.Name] = ConvertDmtfToTimeSpan(dmtfTime);
                }
                else
                {
                    // Add the property to the dictionary as is if it's not DMTF
                    dictionary[property.Name] = property.Value;
                }
            }

            return dictionary;
        }

        private static TimeSpan ConvertDmtfToTimeSpan(string dmtfTime)
        {
            // DMTF datetime format: yyyymmddHHMMSS.mmmmmmsUUU
            // We only need HHMMSS part for this case
            if (dmtfTime.Length >= 15)
            {
                string hhmmss = dmtfTime.Substring(8, 6);
                if (TimeSpan.TryParseExact(hhmmss, "HHmmss", CultureInfo.InvariantCulture, out TimeSpan timeSpan))
                {
                    return timeSpan;
                }
            }
            return TimeSpan.Zero;
        }
    }
}
