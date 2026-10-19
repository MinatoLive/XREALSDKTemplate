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
        private string _currentFilterStatus = "LIVE";
        private bool _isLoading = false;
        private System.Threading.CancellationTokenSource _autoRefreshCts;

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

            StartAutoRefreshLoop();
        }

        public void Dispose()
        {
            StopAutoRefreshLoop();

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

        private void StartAutoRefreshLoop()
        {
            StopAutoRefreshLoop();
            _autoRefreshCts = new System.Threading.CancellationTokenSource();
            _ = AutoRefreshLoopAsync(_autoRefreshCts.Token);
        }

        private void StopAutoRefreshLoop()
        {
            if (_autoRefreshCts != null)
            {
                _autoRefreshCts.Cancel();
                _autoRefreshCts.Dispose();
                _autoRefreshCts = null;
            }
        }

        private async Task AutoRefreshLoopAsync(System.Threading.CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(10000, token);
                }
                catch (System.OperationCanceledException)
                {
                    break;
                }

                if (token.IsCancellationRequested) break;

                // 仅在满足自动刷新条件时执行：未在加载中、处于登录态、且未进入房间
                if (_isLoading || RoomJoinController.IsInRoom || RoomJoinController.IsJoining)
                {
                    continue;
                }

                var auth = _authService ?? SurgicalLiveSDK.Auth;
                if (auth == null || SurgicalLiveSDK.StateManager == null ||
                    SurgicalLiveSDK.StateManager.CurrentState != UserAppState.LoggedIn)
                {
                    continue;
                }

                await SilentRefreshCheckAsync();
            }
        }

        /// <summary>
        /// 10 秒静默轮询：静默拉取后台最新列表，对比数据变动，仅在不一致时触发 UI 重新渲染
        /// </summary>
        public async Task SilentRefreshCheckAsync()
        {
            var auth = _authService ?? SurgicalLiveSDK.Auth;
            if (auth == null) return;

            try
            {
                // 静默拉取最新房间数据，不显示全屏转圈 loading
                var roomResponse = await auth.GetRoomInfosAsync(page: 1, size: 100);
                if (roomResponse == null || roomResponse.list == null) return;

                var incomingList = new List<RoomItem>();
                foreach (var r in roomResponse.list)
                {
                    if (r != null) incomingList.Add(r);
                }

                // 对比当前缓存数据与远端新数据
                if (AreRoomListsEquivalent(_allRoomsCache, incomingList))
                {
                    // 数据完全一致，跳过 UI 渲染，杜绝卡顿与闪烁
                    return;
                }

                // 数据发生变更，补齐预设场景并平滑更新
                List<RoomPresetItem> presets = null;
                try
                {
                    presets = await auth.GetRoomPresetsAsync();
                }
                catch
                {
                    // 容错降级
                }

                _allRoomsCache.Clear();
                foreach (var room in incomingList)
                {
                    EnrichRoomPreviewWithPresets(room, presets);
                    _allRoomsCache.Add(room);
                }

                RenderCurrentPageSlice();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[LobbyPresenter] 自动刷新静默拉取异常 (不阻塞前台): {ex.Message}");
            }
        }

        public static bool AreRoomListsEquivalent(List<RoomItem> oldList, List<RoomItem> newList)
        {
            if (ReferenceEquals(oldList, newList)) return true;
            if (oldList == null || newList == null) return false;
            if (oldList.Count != newList.Count) return false;

            for (int i = 0; i < oldList.Count; i++)
            {
                var o = oldList[i];
                var n = newList[i];
                if (o == null && n == null) continue;
                if (o == null || n == null) return false;

                if (o.id != n.id ||
                    o.status != n.status ||
                    o.participant_count != n.participant_count ||
                    o.name != n.name ||
                    o.stream_type != n.stream_type ||
                    o.cover_image_url != n.cover_image_url ||
                    o.description != n.description)
                {
                    return false;
                }
            }

            return true;
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

                // 2.1 同步拉取 2.1.1 预设场景列表用于元数据补齐 (容错处理，失败不阻断房间展示)
                List<RoomPresetItem> presets = null;
                try
                {
                    presets = await auth.GetRoomPresetsAsync();
                }
                catch (Exception presetEx)
                {
                    Debug.LogWarning($"[LobbyPresenter] 获取手术室预设场景接口(2.1.1)未就绪或异常: {presetEx.Message}");
                }

                _allRoomsCache.Clear();
                if (roomResponse?.list != null)
                {
                    foreach (var room in roomResponse.list)
                    {
                        if (room != null)
                        {
                            EnrichRoomPreviewWithPresets(room, presets);
                            _allRoomsCache.Add(room);
                        }
                    }
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

        /// <summary>
        /// 针对未返回 preview 结构体的历史或未完善房间，结合 2.1.1 预设元数据进行智能补齐
        /// </summary>
        public static void EnrichRoomPreviewWithPresets(RoomItem room, List<RoomPresetItem> presets)
        {
            if (room == null) return;

            // 如果已有完整的 preview，补充 direct_url 即可
            if (room.preview != null && !string.IsNullOrEmpty(room.preview.image_url))
            {
                if (string.IsNullOrEmpty(room.preview.direct_url) && !string.IsNullOrEmpty(room.id))
                {
                    room.preview.direct_url = $"/api/v1/rooms/{room.id}/preview";
                }
                return;
            }

            string cover = room.cover_image_url ?? "";
            string name = room.name ?? "";

            RoomPresetItem matchedPreset = null;
            if (presets != null && presets.Count > 0)
            {
                matchedPreset = presets.Find(p =>
                    (!string.IsNullOrEmpty(p.image_url) && cover.IndexOf(p.key, StringComparison.OrdinalIgnoreCase) >= 0) ||
                    (!string.IsNullOrEmpty(p.title) && name.IndexOf(p.title, StringComparison.OrdinalIgnoreCase) >= 0) ||
                    (!string.IsNullOrEmpty(p.key) && name.IndexOf(p.key, StringComparison.OrdinalIgnoreCase) >= 0));
            }

            // 本地语义兜底及历史 preset:X 格式映射
            if (matchedPreset == null)
            {
                if (cover == "preset:0" || cover.StartsWith("preset:laparoscopic", StringComparison.OrdinalIgnoreCase) ||
                    cover.Contains("laparoscopic") || name.Contains("腔镜") || name.Contains("达芬奇"))
                {
                    matchedPreset = new RoomPresetItem
                    {
                        key = "laparoscopic",
                        title = "达芬奇腔镜机器人间",
                        subtitle = "4K 3D 腹腔镜微创主视野 · 内窥镜立体视界",
                        image_url = "/presets/laparoscopic.png"
                    };
                }
                else if (cover == "preset:1" || cover.StartsWith("preset:orthopedic", StringComparison.OrdinalIgnoreCase) ||
                         cover.Contains("orthopedic") || name.Contains("骨科") || name.Contains("天玑"))
                {
                    matchedPreset = new RoomPresetItem
                    {
                        key = "orthopedic",
                        title = "骨科天玑机器人间",
                        subtitle = "脊柱胸腰段导航 · 钛合金椎弓根螺钉光电定位",
                        image_url = "/presets/orthopedic.png"
                    };
                }
                else if (cover == "preset:2" || cover.StartsWith("preset:neurosurgery", StringComparison.OrdinalIgnoreCase) ||
                         cover.Contains("neurosurgery") || name.Contains("神经") || name.Contains("显微"))
                {
                    matchedPreset = new RoomPresetItem
                    {
                        key = "neurosurgery",
                        title = "神经外科显微手术室",
                        subtitle = "前交通动脉瘤夹闭 · 术野显微高倍镜",
                        image_url = "/presets/neurosurgery.png"
                    };
                }
                else if (cover == "preset:3" || cover.StartsWith("preset:hybrid", StringComparison.OrdinalIgnoreCase) ||
                         cover.Contains("hybrid") || name.Contains("血管") || name.Contains("介入"))
                {
                    matchedPreset = new RoomPresetItem
                    {
                        key = "hybrid",
                        title = "杂交血管介入手术室",
                        subtitle = "DSA C-Arm 血管造影透视 · 数字减影与介入栓塞",
                        image_url = "/presets/hybrid_angiography.png"
                    };
                }
                else if (!string.IsNullOrEmpty(cover) && cover.StartsWith("/presets/"))
                {
                    matchedPreset = new RoomPresetItem
                    {
                        key = "custom",
                        title = name,
                        subtitle = room.description ?? "",
                        image_url = cover
                    };
                }
                else
                {
                    // 封面为空或无匹配特征词的测试房间（如测试23333），默认赋予高精度达芬奇腔镜机器人预设，确保卡片视觉饱满绝不留白
                    matchedPreset = new RoomPresetItem
                    {
                        key = "laparoscopic",
                        title = "达芬奇腔镜机器人间",
                        subtitle = "4K 3D 腹腔镜微创主视野 · 内窥镜立体视界",
                        image_url = "/presets/laparoscopic.png"
                    };
                }
            }

            if (matchedPreset != null)
            {
                room.preview = new RoomPreview
                {
                    image_url = matchedPreset.image_url,
                    direct_url = !string.IsNullOrEmpty(room.id) ? $"/api/v1/rooms/{room.id}/preview" : null,
                    preset_key = matchedPreset.key,
                    preset_title = matchedPreset.title,
                    preset_subtitle = matchedPreset.subtitle
                };
            }
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
