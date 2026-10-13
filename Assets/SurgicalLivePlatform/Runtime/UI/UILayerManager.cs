using System;
using System.Collections.Generic;
using UnityEngine;

namespace SurgicalLivePlatform.Runtime.UI
{
    /// <summary>
    /// 统一 UI 层级与模态弹窗栈管理器
    /// 划分 Page 层（主页面）、Modal 层（悬浮模态弹窗）与 Top 层（Toast/Loading）
    /// 彻底废除弹窗与下层页面的反向硬编码耦合 (如 lobbyViewToHide)
    /// </summary>
    public class UILayerManager : MonoBehaviour
    {
        private static UILayerManager _instance;
        public static UILayerManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindFirstObjectByType<UILayerManager>();
                    if (_instance == null)
                    {
                        var go = new GameObject("UILayerManager");
                        _instance = go.AddComponent<UILayerManager>();
                    }
                }
                return _instance;
            }
        }

        private readonly List<IUIView> _modalStack = new List<IUIView>(4);

        [Header("Blocking Overlay (Optional)")]
        [SerializeField] private GameObject modalOverlay;

        public event Action<IUIView> OnModalOpened;
        public event Action<IUIView> OnModalClosed;

        public bool HasActiveModal => _modalStack.Count > 0;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
        }

        /// <summary>
        /// 打开模态弹窗并压栈
        /// </summary>
        public void OpenModal(IUIView modalView)
        {
            if (modalView == null) return;

            if (!_modalStack.Contains(modalView))
            {
                _modalStack.Add(modalView);
            }

            if (modalOverlay != null)
            {
                modalOverlay.SetActive(true);
            }

            modalView.Show();
            OnModalOpened?.Invoke(modalView);
        }

        /// <summary>
        /// 关闭指定模态弹窗并出栈
        /// </summary>
        public void CloseModal(IUIView modalView)
        {
            if (modalView == null) return;

            if (_modalStack.Contains(modalView))
            {
                _modalStack.Remove(modalView);
            }

            modalView.Hide();

            if (_modalStack.Count == 0 && modalOverlay != null)
            {
                modalOverlay.SetActive(false);
            }

            OnModalClosed?.Invoke(modalView);
        }

        /// <summary>
        /// 关闭最顶层模态弹窗 (通常用于返回键/取消操作)
        /// </summary>
        public bool CloseTopModal()
        {
            if (_modalStack.Count == 0) return false;

            int lastIdx = _modalStack.Count - 1;
            var top = _modalStack[lastIdx];
            CloseModal(top);
            return true;
        }

        /// <summary>
        /// 关闭所有当前激活的模态弹窗
        /// </summary>
        public void CloseAllModals()
        {
            for (int i = _modalStack.Count - 1; i >= 0; i--)
            {
                var modal = _modalStack[i];
                if (modal != null)
                {
                    modal.Hide();
                    OnModalClosed?.Invoke(modal);
                }
            }
            _modalStack.Clear();

            if (modalOverlay != null)
            {
                modalOverlay.SetActive(false);
            }
        }
    }
}
