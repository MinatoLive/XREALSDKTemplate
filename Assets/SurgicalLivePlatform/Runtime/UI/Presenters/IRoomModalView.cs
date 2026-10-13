using System;
using System.Collections.Generic;

namespace SurgicalLivePlatform.Runtime.UI
{
    public class RoomFormData
    {
        public FormMode mode;
        public RoomCreateMode createMode;
        public string roomId;
        public string name;
        public string description;
        public string coverUrl;
        public string department;
        public int durationMinutes;
    }

    /// <summary>
    /// 房间创建/编辑模态弹窗视图抽象接口 (MVP 模式 View 契约)
    /// </summary>
    public interface IRoomModalView : IUIView
    {
        event Action<RoomFormData> OnSubmitRequested;
        event Action OnCloseRequested;

        void SetDepartments(List<string> departments);
        void SetSubmitting(bool isSubmitting);
        void ShowErrorMessage(string message);
        void ClearForm();
    }
}
