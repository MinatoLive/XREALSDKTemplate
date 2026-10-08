using UnityEngine;
using TMPro;

namespace SurgicalLivePlatform.Runtime.UI
{
    public class DebugLogPanel : MonoBehaviour
    {
        public TextMeshProUGUI logText;
        private int maxChars = 2000;

        void OnEnable()
        {
            Application.logMessageReceived += HandleLog;
            if (logText != null) logText.text = "<color=green>Debug Logger Started...</color>\n";
        }

        void OnDisable()
        {
            Application.logMessageReceived -= HandleLog;
        }

        void HandleLog(string logString, string stackTrace, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Warning)
            {
                // Filter out the affordance spam
                if (logString.Contains("Affordance") || logString.Contains("index (0) is out of bounds")) return;
                // Filter out the hand tracking log
                if (logString.Contains("HandDetTrackCalculator")) return;
                // Filter out the PowerHAL log
                if (logString.Contains("PowerHAL")) return;
                
                if (logText != null)
                {
                    logText.text += "\n<color=" + GetColor(type) + ">" + logString + "</color>";
                    if (logText.text.Length > maxChars)
                    {
                        logText.text = logText.text.Substring(logText.text.Length - maxChars);
                    }
                }
            }
        }

        string GetColor(LogType type)
        {
            switch (type) {
                case LogType.Error: return "red";
                case LogType.Exception: return "red";
                case LogType.Warning: return "yellow";
                default: return "white";
            }
        }
    }
}
