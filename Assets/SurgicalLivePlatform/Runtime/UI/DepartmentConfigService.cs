using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;

namespace SurgicalLivePlatform.Runtime.UI
{
    [System.Serializable]
    public class DepartmentItem
    {
        public string id;
        public string name;
    }

    [System.Serializable]
    public class DepartmentConfigData
    {
        public List<DepartmentItem> departments;
    }

    public static class DepartmentConfigService
    {
        private static string ConfigPath => Path.Combine(Application.streamingAssetsPath, "Config", "DepartmentConfig.json");

        public static async Task<List<string>> GetDepartmentNamesAsync()
        {
            var departments = new List<string>();

            // For future: if API is available, fetch from API here.
            
            // Read from local StreamingAssets JSON
            if (File.Exists(ConfigPath))
            {
                try
                {
                    string json = await File.ReadAllTextAsync(ConfigPath);
                    var data = JsonUtility.FromJson<DepartmentConfigData>(json);
                    if (data != null && data.departments != null)
                    {
                        foreach (var dept in data.departments)
                        {
                            departments.Add(dept.name);
                        }
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"[DepartmentConfigService] Failed to read config: {e.Message}");
                }
            }
            else
            {
                Debug.LogWarning($"[DepartmentConfigService] Config file not found at {ConfigPath}. Using fallbacks.");
                departments.AddRange(new string[] { "骨科", "神经外科", "心血管内科", "普外科", "泌尿外科" });
            }

            return departments;
        }
    }
}
