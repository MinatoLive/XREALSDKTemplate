using UnityEngine;

namespace SurgicalLivePlatform.Runtime.UI
{
    /// <summary>
    /// 标准 UI 面板抽象基类，实现 IUIView 生命周期契约。
    /// 统一管理界面的初始化、显隐调度及与 XR 物理挂载节点的兼容逻辑。
    /// </summary>
    public abstract class BaseUIPanel : MonoBehaviour, IUIView
    {
        [Header("View Settings")]
        [SerializeField] private string viewId;

        public string ViewId
        {
            get => string.IsNullOrEmpty(viewId) ? gameObject.name : viewId;
            set => viewId = value;
        }

        public bool IsVisible => gameObject != null && gameObject.activeSelf;

        private bool _isInitialized = false;

        public void InitializeView()
        {
            if (_isInitialized) return;
            OnInit();
            _isInitialized = true;
        }

        public virtual void Show()
        {
            InitializeView();
            SetPanelHierarchyActive(true);
            OnShow();
        }

        public virtual void Hide()
        {
            OnHide();
            SetPanelHierarchyActive(false);
        }

        /// <summary>
        /// 初次初始化生命周期回调 (仅执行一次)
        /// </summary>
        protected virtual void OnInit() { }

        /// <summary>
        /// 面板进入/显示生命周期回调
        /// </summary>
        protected virtual void OnShow() { }

        /// <summary>
        /// 面板离开/隐藏生命周期回调
        /// </summary>
        protected virtual void OnHide() { }

        /// <summary>
        /// 处理面板及其物理层级节点的激活状态
        /// </summary>
        protected virtual void SetPanelHierarchyActive(bool active)
        {
            if (gameObject != null)
            {
                gameObject.SetActive(active);

                // 兼容场景中包裹的 Rigidbody 物理节点
                if (transform.parent != null && transform.parent.parent != null)
                {
                    var rb = transform.parent.parent.GetComponent<Rigidbody>();
                    if (rb != null)
                    {
                        rb.gameObject.SetActive(active);
                    }
                }
            }
        }
    }
}
