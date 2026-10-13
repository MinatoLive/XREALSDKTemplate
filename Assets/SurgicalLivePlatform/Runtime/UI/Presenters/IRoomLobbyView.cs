using System;
using System.Collections.Generic;
using SurgicalLivePlatform.Runtime.DTOs;

namespace SurgicalLivePlatform.Runtime.UI
{
    /// <summary>
    /// 手术大厅视图抽象接口 (MVP 模式 View 契约)
    /// </summary>
    public interface IRoomLobbyView : IUIView
    {
        event Action OnRefreshRequested;
        event Action OnPreviousPageRequested;
        event Action OnNextPageRequested;
        event Action OnImmediateLiveRequested;
        event Action OnScheduledLiveRequested;
        event Action<string> OnJoinRoomRequested;
        event Action<string> OnFilterChanged;

        int PageSize { get; set; }

        void SetRoomList(List<RoomItem> rooms, bool append);
        void SetCanCreateRoom(bool canCreate);
        void SetLoading(bool isLoading);
        void SetPaginationState(int currentPage, int totalPages, int totalItemCount);
        void ShowErrorMessage(string message);
    }
}
