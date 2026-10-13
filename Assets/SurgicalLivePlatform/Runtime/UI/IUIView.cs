namespace SurgicalLivePlatform.Runtime.UI
{
    /// <summary>
    /// 标准 UI 视图/面板生命周期协议契约
    /// </summary>
    public interface IUIView
    {
        /// <summary>
        /// 视图唯一标识符 (通常对应 UIMark 标记名或 GameObject 名称)
        /// </summary>
        string ViewId { get; }

        /// <summary>
        /// 当前视图是否处于可见/激活状态
        /// </summary>
        bool IsVisible { get; }

        /// <summary>
        /// 初始化视图 (仅在首次加载或首次展示前执行一次)
        /// </summary>
        void InitializeView();

        /// <summary>
        /// 展示视图并触发生命周期
        /// </summary>
        void Show();

        /// <summary>
        /// 隐藏视图并触发生命周期
        /// </summary>
        void Hide();
    }
}
