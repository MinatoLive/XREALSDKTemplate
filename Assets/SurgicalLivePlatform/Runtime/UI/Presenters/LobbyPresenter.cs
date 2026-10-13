using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SurgicalLivePlatform.Runtime.Auth;
using SurgicalLivePlatform.Runtime.DTOs;
using SurgicalLivePlatform.Runtime.Models;
using UnityEngine;

namespace SurgicalLivePlatform.Runtime.UI
{
    /// <summary>
    /// 手术大厅业务中介 (Lobby Presenter)
    /// 纯 C# 实现，负责驱动房间列表拉取、分页加载、权限校验与弹窗协调。
    /// </summary>
    public class LobbyPresenter : IDisposable
    {
        private readonly IRoomLobbyView _view;
        private readonly RoomModalView _modalView;
        private readonly IAuthService _authService;
        private readonly UILayerManager _layerManager;

        private readonly List<RoomItem> _allRoomsCache = new List<RoomItem>(128);
        private int _currentPage = 1;
        private int _pageSize = 8;
        private string _currentFilterStatus = "";
        private bool _isLoading = false;

        public int CurrentPage => _currentPage;
        public int PageSize
        {
            get => _pageSize;
            set
            {
                if (value <= 0) return;
                _pageSize = value;
                if (_view != null) _view.PageSize = value;
                RenderCurrentPageSlice();
            }
        }

        public int TotalPages
        {
            get
            {
                var filtered = GetFilteredRooms();
                if (filtered.Count == 0) return 0;
                return Mathf.CeilToInt((float)filtered.Count / _pageSize);
            }
        }

        public IReadOnlyList<RoomItem> CachedRooms => _allRoomsCache;

        public LobbyPresenter(
            IRoomLobbyView view,
            RoomModalView modalView = null,
            IAuthService authService = null,
            UILayerManager layerManager = null)
        {
            _view = view ?? throw new ArgumentNullException(nameof(view));
            _modalView = modalView;
            _authService = authService ?? SurgicalLiveSDK.Auth;
            _layerManager = layerManager ?? UILayerManager.Instance;

            if (_view.PageSize > 0)
            {
                _pageSize = _view.PageSize;
            }

            _view.OnRefreshRequested += HandleRefreshRequested;
            _view.OnPreviousPageRequested += HandlePreviousPageRequested;
            _view.OnNextPageRequested += HandleNextPageRequested;
            _view.OnFilterChanged += HandleFilterChanged;
            _view.OnImmediateLiveRequested += HandleImmediateLiveRequested;
            _view.OnScheduledLiveRequested += HandleScheduledLiveRequested;
            _view.OnJoinRoomRequested += HandleJoinRoomRequested;
        }

        public void Dispose()
        {
            if (_view != null)
            {
                _view.OnRefreshRequested -= HandleRefreshRequested;
                _view.OnPreviousPageRequested -= HandlePreviousPageRequested;
                _view.OnNextPageRequested -= HandleNextPageRequested;
                _view.OnFilterChanged -= HandleFilterChanged;
                _view.OnImmediateLiveRequested -= HandleImmediateLiveRequested;
                _view.OnScheduledLiveRequested -= HandleScheduledLiveRequested;
                _view.OnJoinRoomRequested -= HandleJoinRoomRequested;
            }
        }

        private async void HandleRefreshRequested()
        {
            await RefreshRoomsAsync();
        }

        private void HandlePreviousPageRequested()
        {
            PreviousPage();
        }

        private void HandleNextPageRequested()
        {
            NextPage();
        }

        private void HandleFilterChanged(string status)
        {
            ApplyFilter(status);
        }

        /// <summary>
        /// 一次性拉取全量数据集并缓存，后续翻页基于本地切片
        /// </summary>
        public async Task RefreshRoomsAsync()
        {
            if (_isLoading) return;

            var auth = _authService ?? SurgicalLiveSDK.Auth;
            if (auth == null || SurgicalLiveSDK.StateManager == null ||
                SurgicalLiveSDK.StateManager.CurrentState != UserAppState.LoggedIn)
            {
                return;
            }

            _isLoading = true;
            _view.SetLoading(true);

            try
            {
                // 1. 检查建房权限
                await CheckPermissionsAsync(auth);

                // 2. 一次性全量拉取房间数据 (设置上限为 100)
                var roomResponse = await auth.GetRoomInfosAsync(page: 1, size: 100);
                _allRoomsCache.Clear();
                if (roomResponse?.list != null)
                {
                    _allRoomsCache.AddRange(roomResponse.list);
                }

                _currentPage = 1;
                RenderCurrentPageSlice();
            }
            catch (Exception ex)
            {
                _view.ShowErrorMessage($"刷新房间列表失败: {ex.Message}");
            }
            finally
            {
                _isLoading = false;
                _view.SetLoading(false);
            }
        }

        /// <summary>
        /// 向后循环翻页 (末页跳转至首页 1>2>3>1)
        /// 纯本地切片，不发起任何网络请求
        /// </summary>
        public void NextPage()
        {
            var filtered = GetFilteredRooms();
            int totalCount = filtered.Count;
            if (totalCount == 0) return;

            int totalPages = Mathf.CeilToInt((float)totalCount / _pageSize);
            if (_currentPage >= totalPages)
            {
                // 首尾循环衔接：最后一页跳回第一页
                _currentPage = 1;
            }
            else
            {
                _currentPage++;
            }

            RenderCurrentPageSlice();
        }

        /// <summary>
        /// 向前循环翻页 (首页跳转至末页 1<3<2<1)
        /// 纯本地切片，不发起任何网络请求
        /// </summary>
        public void PreviousPage()
        {
            var filtered = GetFilteredRooms();
            int totalCount = filtered.Count;
            if (totalCount == 0) return;

            int totalPages = Mathf.CeilToInt((float)totalCount / _pageSize);
            if (_currentPage <= 1)
            {
                // 首尾循环衔接：第一页向前跳到最后一页
                _currentPage = totalPages;
            }
            else
            {
                _currentPage--;
            }

            RenderCurrentPageSlice();
        }

        /// <summary>
        /// 筛选变更 (全部/直播中/预约中/已结束)
        /// 基于本地缓存直接过滤并归位第一页，无需重复网络请求
        /// </summary>
        public void ApplyFilter(string status)
        {
            _currentFilterStatus = status ?? "";
            _currentPage = 1;
            RenderCurrentPageSlice();
        }

        /// <summary>
        /// 根据当前页码与 PageSize 进行内存切片并同步 View 视图
        /// </summary>
        public void RenderCurrentPageSlice()
        {
            var filtered = GetFilteredRooms();
            int totalCount = filtered.Count;
            int totalPages = totalCount == 0 ? 0 : Mathf.CeilToInt((float)totalCount / _pageSize);

            if (totalCount == 0)
            {
                _currentPage = 1;
                _view.SetRoomList(new List<RoomItem>(), append: false);
                _view.SetPaginationState(0, 0, 0);
                return;
            }

            if (_currentPage < 1) _currentPage = 1;
            if (_currentPage > totalPages) _currentPage = totalPages;

            int skip = (_currentPage - 1) * _pageSize;
            int count = Math.Min(_pageSize, totalCount - skip);
            var pageSlice = filtered.GetRange(skip, count);

            _view.SetRoomList(pageSlice, append: false);
            _view.SetPaginationState(_currentPage, totalPages, totalCount);
        }

        private List<RoomItem> GetFilteredRooms()
        {
            if (string.IsNullOrEmpty(_currentFilterStatus))
            {
                return _allRoomsCache;
            }
            return _allRoomsCache.FindAll(r => r != null && r.status == _currentFilterStatus);
        }

#if UNITY_INCLUDE_TESTS || UNITY_EDITOR
        public void SetCachedRoomsForTesting(IEnumerable<RoomItem> rooms)
        {
            _allRoomsCache.Clear();
            if (rooms != null) _allRoomsCache.AddRange(rooms);
            _currentPage = 1;
            RenderCurrentPageSlice();
        }
#endif

        private async Task CheckPermissionsAsync(IAuthService auth)
        {
            try
            {
                var user = await auth.GetUserInfoAsync();
                bool canCreate = user != null && user.permissions != null && Array.Exists(user.permissions, p => p == "room.create");
                _view.SetCanCreateRoom(canCreate);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[LobbyPresenter] 权限校验异常: {e.Message}");
                _view.SetCanCreateRoom(false);
            }
        }

        private void HandleImmediateLiveRequested()
        {
            if (_modalView == null) return;

            _modalView.Configure(RoomCreateMode.Immediate);
            var lm = _layerManager ?? UILayerManager.Instance;
            if (lm != null)
            {
                lm.OpenModal(_modalView);
            }
            else
            {
                _modalView.Show();
            }
        }

        private void HandleScheduledLiveRequested()
        {
            if (_modalView == null) return;

            _modalView.Configure(RoomCreateMode.Scheduled);
            var lm = _layerManager ?? UILayerManager.Instance;
            if (lm != null)
            {
                lm.OpenModal(_modalView);
            }
            else
            {
                _modalView.Show();
            }
        }

        private void HandleJoinRoomRequested(string roomId)
        {
            if (string.IsNullOrEmpty(roomId)) return;

            if (RoomJoinController.Instance != null)
            {
                RoomJoinController.Instance.JoinRoom(roomId);
            }
            else
            {
                Debug.LogError("[LobbyPresenter] 场景中找不到 RoomJoinController 单例！");
            }
        }
    }
}
