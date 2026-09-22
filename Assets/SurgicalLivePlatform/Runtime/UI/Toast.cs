using System;

namespace SurgicalLivePlatform.Runtime.UI
{
    /// <summary>
    /// 全局提示 (Toast) 的解耦接口。
    /// 具体的 UI 表现由上层应用（如 Example 里的 ToastManager）通过监听事件来实现。
    /// </summary>
    public static class Toast
    {
        public static Action<string, float> OnShow;
        public static Action<string> OnShowPersistent;
        public static Action OnHide;

        public static void Show(string message, float duration = 2.0f)
        {
            OnShow?.Invoke(message, duration);
        }

        public static void ShowPersistent(string message)
        {
            OnShowPersistent?.Invoke(message);
        }

        public static void Hide()
        {
            OnHide?.Invoke();
        }
    }
}
