using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using SurgicalLivePlatform.Runtime.DTOs;

namespace SurgicalLivePlatform.Runtime.UI
{
    /// <summary>
    /// 手术大厅视图控制器 (MVP Dumb View)
    /// 继承 BaseUIPanel，实现 IRoomLobbyView。
    /// 仅负责卡片对象池渲染、滚动条监听、状态标签切换及上报用户交互事件。
    /// 所有的网络请求、分页管理及弹窗协调均交由 LobbyPresenter 托管。
    /// </summary>
    public class RoomLobbyView : BaseUIPanel, IRoomLobbyView
    {
        [Header("UI References")]
        [SerializeField] private ScrollRect scrollRect;
        [SerializeField] private RectTransform contentPanel;
        [SerializeField] private GameObject roomCardPrefab;
        [SerializeField] private Button immediateLiveButton;
        [SerializeField] private Button scheduledLiveButton;
        [SerializeField] private RoomModalView roomModalView;
        [SerializeField] private Button refreshButton;

        [Header("Pagination (循环翻页组件)")]
        [SerializeField] private Button prevPageButton;
        [SerializeField] private Button nextPageButton;
        [SerializeField] private TMPro.TMP_Text pageIndicatorText;
        [SerializeField] private int pageSize = 8;

        public int PageSize
        {
            get => pageSize;
            set => pageSize = value;
        }

        public event Action OnRefreshRequested;
        public event Action OnPreviousPageRequested;
        public event Action OnNextPageRequested;
        public event Action<string> OnFilterChanged;
        public event Action OnImmediateLiveRequested;
        public event Action OnScheduledLiveRequested;
#pragma warning disable CS0067
        public event Action<string> OnJoinRoomRequested;
#pragma warning restore CS0067

        private LobbyPresenter _presenter;
        private readonly List<RoomCardItem> cardPool = new List<RoomCardItem>(32);
        private readonly List<RoomCardItem> activeCards = new List<RoomCardItem>(32);

        private bool isLoading = false;
        public Toggle filterAll;
        public Toggle filterLive;
        public Toggle filterCreated;
        public Toggle filterEnded;
        private string currentFilterStatus = ""; // "" 表示全部

        private void Start()
        {
            InitializeView();
        }

        protected override void OnInit()
        {
            // 彻底禁用拖动功能：锁死 ScrollRect 纵向与横向拖动，杜绝手势滑动与位置更新抖动
            if (scrollRect != null)
            {
                scrollRect.vertical = false;
                scrollRect.horizontal = false;
                scrollRect.movementType = ScrollRect.MovementType.Clamped;
            }
            
            if (immediateLiveButton != null)
            {
                immediateLiveButton.onClick.AddListener(() => OnImmediateLiveRequested?.Invoke());
            }
            if (scheduledLiveButton != null)
            {
                scheduledLiveButton.onClick.AddListener(() => OnScheduledLiveRequested?.Invoke());
            }
            if (refreshButton != null)
            {
                refreshButton.onClick.AddListener(() => OnRefreshRequested?.Invoke());
            }

            BindFilterToggles();
            ResolvePaginationControls();

            if (prevPageButton != null)
            {
                prevPageButton.onClick.AddListener(() => OnPreviousPageRequested?.Invoke());
            }
            if (nextPageButton != null)
            {
                nextPageButton.onClick.AddListener(() => OnNextPageRequested?.Invoke());
            }

            // 自愈关联 Presenter
            if (_presenter == null)
            {
                _presenter = new LobbyPresenter(this, roomModalView);
            }
        }

        protected override void OnShow()
        {
            // 严格禁止在 OnShow 隐式触发房间列表网络刷新，防止进出房间路由切换时重复调用接口
        }

        private void OnDestroy()
        {
            if (refreshButton != null)
            {
                refreshButton.onClick.RemoveAllListeners();
            }
            _presenter?.Dispose();
            _presenter = null;
        }

        public void AttachPresenter(LobbyPresenter presenter)
        {
            _presenter?.Dispose();
            _presenter = presenter;
        }

        #region IRoomLobbyView Implementation

        public void SetRoomList(List<RoomItem> rooms, bool append)
        {
            if (rooms == null)
            {
                ClearActiveCards();
                UpdateFilterCounts();
                return;
            }

            if (append)
            {
                int startIndex = activeCards.Count;
                for (int i = 0; i < rooms.Count; i++)
                {
                    var card = GetCardFromPool();
                    _ = card.BindData(rooms[i], startIndex + i + 1);
                    card.gameObject.SetActive(true);
                    if (!activeCards.Contains(card)) activeCards.Add(card);
                }
            }
            else
            {
                // 翻页切片模式：采用原地就地复用 (In-Place Diff & Reuse)，彻底消除每帧关闭/开启造成的卡片疯狂闪烁与布局抖动
                int targetCount = rooms.Count;

                // 1. 复用或创建前 targetCount 个卡片并原地更新数据
                for (int i = 0; i < targetCount; i++)
                {
                    RoomCardItem card;
                    if (i < cardPool.Count)
                    {
                        card = cardPool[i];
                    }
                    else
                    {
                        var newObj = Instantiate(roomCardPrefab, contentPanel);
                        card = newObj.GetComponent<RoomCardItem>();
                        cardPool.Add(card);
                    }

                    if (card != null)
                    {
                        _ = card.BindData(rooms[i], i + 1);
                        if (!card.gameObject.activeSelf)
                        {
                            card.gameObject.SetActive(true);
                        }
                    }
                }

                // 2. 超出 targetCount 的卡片安全隐藏（保证没有旧残留卡片）
                for (int i = targetCount; i < cardPool.Count; i++)
                {
                    if (cardPool[i] != null && cardPool[i].gameObject.activeSelf)
                    {
                        cardPool[i].gameObject.SetActive(false);
                    }
                }

                // 3. 同步 activeCards 容器
                activeCards.Clear();
                for (int i = 0; i < targetCount; i++)
                {
                    if (cardPool[i] != null) activeCards.Add(cardPool[i]);
                }
            }

            UpdateFilterCounts();
        }

        public void SetCanCreateRoom(bool canCreate)
        {
            if (immediateLiveButton != null) immediateLiveButton.gameObject.SetActive(canCreate);
            if (scheduledLiveButton != null) scheduledLiveButton.gameObject.SetActive(canCreate);
        }

        public void SetLoading(bool loading)
        {
            isLoading = loading;
        }

        public void SetPaginationState(int currentPage, int totalPages, int totalItemCount)
        {
            if (pageIndicatorText != null)
            {
                if (totalItemCount == 0 || totalPages == 0)
                {
                    pageIndicatorText.text = "0 / 0";
                }
                else
                {
                    pageIndicatorText.text = $"{currentPage} / {totalPages}";
                }
            }

            // 循环翻页模式下，只有在完全无数据时禁用翻页按钮
            bool hasData = totalItemCount > 0;
            if (prevPageButton != null) prevPageButton.interactable = hasData;
            if (nextPageButton != null) nextPageButton.interactable = hasData;
        }

        public void ShowErrorMessage(string message)
        {
            Debug.LogError($"[RoomLobbyView] {message}");
            Toast.Show(message, 2.5f);
        }

        #endregion

        /// <summary>
        /// 供外部 (如路由/刷新按钮) 触发的标准刷新接口
        /// </summary>
        public void RefreshRooms()
        {
            OnRefreshRequested?.Invoke();
        }

        public void PreviousPage()
        {
            OnPreviousPageRequested?.Invoke();
        }

        public void NextPage()
        {
            OnNextPageRequested?.Invoke();
        }

        public void LoadNextPage()
        {
            OnNextPageRequested?.Invoke();
        }

        private void BindFilterToggles()
        {
            var marks = GetComponentsInChildren<UIMark>(true);
            foreach (var m in marks)
            {
                if (m.markName == "Toggle_FilterAll") filterAll = m.GetComponent<Toggle>();
                else if (m.markName == "Toggle_FilterLive") filterLive = m.GetComponent<Toggle>();
                else if (m.markName == "Toggle_FilterCreated") filterCreated = m.GetComponent<Toggle>();
                else if (m.markName == "Toggle_FilterEnded") filterEnded = m.GetComponent<Toggle>();
            }

            var toggleGroup = gameObject.GetComponent<ToggleGroup>();
            if (toggleGroup == null) toggleGroup = gameObject.AddComponent<ToggleGroup>();
            toggleGroup.allowSwitchOff = false;

            if (filterAll != null) 
            {
                filterAll.group = toggleGroup;
                filterAll.onValueChanged.AddListener(isOn => { if (isOn) ApplyFilter(""); });
            }
            if (filterLive != null) 
            {
                filterLive.group = toggleGroup;
                filterLive.onValueChanged.AddListener(isOn => { if (isOn) ApplyFilter("LIVE"); });
            }
            if (filterCreated != null) 
            {
                filterCreated.group = toggleGroup;
                filterCreated.onValueChanged.AddListener(isOn => { if (isOn) ApplyFilter("CREATED"); });
            }
            if (filterEnded != null) 
            {
                filterEnded.group = toggleGroup;
                filterEnded.onValueChanged.AddListener(isOn => { if (isOn) ApplyFilter("ENDED"); });
            }
        }

        private void ApplyFilter(string status)
        {
            currentFilterStatus = status;
            OnFilterChanged?.Invoke(status);

            if (_presenter == null)
            {
                foreach (var card in activeCards)
                {
                    if (card == null || card.BoundRoom == null) continue;
                    bool match = string.IsNullOrEmpty(currentFilterStatus) || card.BoundRoom.status == currentFilterStatus;
                    card.gameObject.SetActive(match);
                }
            }
        }

        private void UpdateFilterCounts()
        {
            int liveCount = 0;
            int createdCount = 0;
            int endedCount = 0;
            int allCount = 0;

            if (_presenter != null && _presenter.CachedRooms != null && _presenter.CachedRooms.Count > 0)
            {
                foreach (var room in _presenter.CachedRooms)
                {
                    if (room == null) continue;
                    allCount++;
                    switch (room.status)
                    {
                        case "LIVE": liveCount++; break;
                        case "CREATED": createdCount++; break;
                        case "ENDED": endedCount++; break;
                    }
                }
            }
            else
            {
                foreach (var card in activeCards)
                {
                    if (card == null || card.BoundRoom == null) continue;
                    allCount++;
                    switch (card.BoundRoom.status)
                    {
                        case "LIVE": liveCount++; break;
                        case "CREATED": createdCount++; break;
                        case "ENDED": endedCount++; break;
                    }
                }
            }

            SetToggleText(filterAll, $"全部 ({allCount})");
            SetToggleText(filterLive, $"直播中 ({liveCount})");
            SetToggleText(filterCreated, $"已预约 ({createdCount})");
            SetToggleText(filterEnded, $"已结束 ({endedCount})");
        }

        private void ResolvePaginationControls()
        {
            if (prevPageButton == null || nextPageButton == null || pageIndicatorText == null)
            {
                var marks = GetComponentsInChildren<UIMark>(true);
                foreach (var m in marks)
                {
                    if (prevPageButton == null && (m.markName == "Btn_PrevPage" || m.markName == "Btn_LobbyPrev"))
                    {
                        prevPageButton = m.GetComponent<Button>();
                    }
                    else if (nextPageButton == null && (m.markName == "Btn_NextPage" || m.markName == "Btn_LobbyNext"))
                    {
                        nextPageButton = m.GetComponent<Button>();
                    }
                    else if (pageIndicatorText == null && (m.markName == "Text_PageIndicator" || m.markName == "Text_LobbyPage"))
                    {
                        pageIndicatorText = m.GetComponent<TMPro.TMP_Text>();
                    }
                    else if (refreshButton == null && (m.markName == "Btn_LobbyRefresh" || m.markName == "Btn_Refresh"))
                    {
                        refreshButton = m.GetComponent<Button>();
                        if (refreshButton != null)
                        {
                            refreshButton.onClick.AddListener(() => OnRefreshRequested?.Invoke());
                        }
                    }
                }
            }

            // 若场景 Hierarchy 中尚未预制左右翻页组件，则在当前大厅底部全自动动态生成高品质翻页栏
            if (prevPageButton == null || nextPageButton == null)
            {
                CreateDefaultPaginationBar();
            }
        }
        [ContextMenu("生成")]
        private void CreateDefaultPaginationBar()
        {
            Transform existingBar = transform.Find("PaginationBar");
            GameObject barObj;
            if (existingBar != null)
            {
                barObj = existingBar.gameObject;
            }
            else
            {
                barObj = new GameObject("PaginationBar", typeof(RectTransform), typeof(Image), typeof(HorizontalLayoutGroup));
                barObj.transform.SetParent(this.transform, false);
            }

            var barRect = barObj.GetComponent<RectTransform>();
            barRect.anchorMin = new Vector2(0.5f, 0f);
            barRect.anchorMax = new Vector2(0.5f, 0f);
            barRect.pivot = new Vector2(0.5f, 0.5f);
            barRect.anchoredPosition = new Vector2(0f, 36f);
            barRect.sizeDelta = new Vector2(380f, 50f);

            var barImg = barObj.GetComponent<Image>();
            barImg.color = new Color(0.12f, 0.16f, 0.24f, 0.88f);
            barImg.raycastTarget = false;

            var layout = barObj.GetComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.spacing = 16f;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            var msyhFont = Resources.Load<TMPro.TMP_FontAsset>("Fonts & Materials/MSYH SDF");

            // 1. 左翻页按钮 (◀ 上一页)
            if (prevPageButton == null)
            {
                var btnObj = new GameObject("Btn_PrevPage", typeof(RectTransform), typeof(Image), typeof(Button), typeof(UIMark));
                btnObj.transform.SetParent(barObj.transform, false);
                var btnRect = btnObj.GetComponent<RectTransform>();
                btnRect.sizeDelta = new Vector2(106f, 38f);

                var img = btnObj.GetComponent<Image>();
                img.color = new Color(0.2f, 0.26f, 0.38f, 0.95f);
                img.raycastTarget = true;

                var btn = btnObj.GetComponent<Button>();
                btn.targetGraphic = img;
                var colors = btn.colors;
                colors.normalColor = new Color(0.2f, 0.26f, 0.38f, 0.95f);
                colors.highlightedColor = new Color(0.28f, 0.4f, 0.6f, 1f);
                colors.pressedColor = new Color(0.14f, 0.18f, 0.26f, 1f);
                colors.selectedColor = new Color(0.24f, 0.32f, 0.46f, 1f);
                colors.disabledColor = new Color(0.12f, 0.15f, 0.2f, 0.4f);
                btn.colors = colors;

                btnObj.GetComponent<UIMark>().markName = "Btn_PrevPage";

                var textObj = new GameObject("Text", typeof(RectTransform), typeof(TMPro.TextMeshProUGUI));
                textObj.transform.SetParent(btnObj.transform, false);
                var textRect = textObj.GetComponent<RectTransform>();
                textRect.anchorMin = Vector2.zero;
                textRect.anchorMax = Vector2.one;
                textRect.sizeDelta = Vector2.zero;
                var tmp = textObj.GetComponent<TMPro.TextMeshProUGUI>();
                if (msyhFont != null) tmp.font = msyhFont;
                tmp.text = "◀ 上一页";
                tmp.fontSize = 15;
                tmp.fontStyle = TMPro.FontStyles.Bold;
                tmp.alignment = TMPro.TextAlignmentOptions.Center;
                tmp.color = Color.white;
                tmp.raycastTarget = false;

                prevPageButton = btn;
            }

            // 2. 中间页码指示器 (1 / 1)
            if (pageIndicatorText == null)
            {
                var indObj = new GameObject("Text_PageIndicator", typeof(RectTransform), typeof(TMPro.TextMeshProUGUI), typeof(UIMark));
                indObj.transform.SetParent(barObj.transform, false);
                var indRect = indObj.GetComponent<RectTransform>();
                indRect.sizeDelta = new Vector2(90f, 38f);

                var tmp = indObj.GetComponent<TMPro.TextMeshProUGUI>();
                if (msyhFont != null) tmp.font = msyhFont;
                tmp.text = "1 / 1";
                tmp.fontSize = 16;
                tmp.fontStyle = TMPro.FontStyles.Bold;
                tmp.alignment = TMPro.TextAlignmentOptions.Center;
                tmp.color = new Color(0.9f, 0.94f, 1f, 0.95f);
                tmp.raycastTarget = false;

                indObj.GetComponent<UIMark>().markName = "Text_PageIndicator";
                pageIndicatorText = tmp;
            }

            // 3. 右翻页按钮 (下一页 ▶)
            if (nextPageButton == null)
            {
                var btnObj = new GameObject("Btn_NextPage", typeof(RectTransform), typeof(Image), typeof(Button), typeof(UIMark));
                btnObj.transform.SetParent(barObj.transform, false);
                var btnRect = btnObj.GetComponent<RectTransform>();
                btnRect.sizeDelta = new Vector2(106f, 38f);

                var img = btnObj.GetComponent<Image>();
                img.color = new Color(0.2f, 0.26f, 0.38f, 0.95f);
                img.raycastTarget = true;

                var btn = btnObj.GetComponent<Button>();
                btn.targetGraphic = img;
                var colors = btn.colors;
                colors.normalColor = new Color(0.2f, 0.26f, 0.38f, 0.95f);
                colors.highlightedColor = new Color(0.28f, 0.4f, 0.6f, 1f);
                colors.pressedColor = new Color(0.14f, 0.18f, 0.26f, 1f);
                colors.selectedColor = new Color(0.24f, 0.32f, 0.46f, 1f);
                colors.disabledColor = new Color(0.12f, 0.15f, 0.2f, 0.4f);
                btn.colors = colors;

                btnObj.GetComponent<UIMark>().markName = "Btn_NextPage";

                var textObj = new GameObject("Text", typeof(RectTransform), typeof(TMPro.TextMeshProUGUI));
                textObj.transform.SetParent(btnObj.transform, false);
                var textRect = textObj.GetComponent<RectTransform>();
                textRect.anchorMin = Vector2.zero;
                textRect.anchorMax = Vector2.one;
                textRect.sizeDelta = Vector2.zero;
                var tmp = textObj.GetComponent<TMPro.TextMeshProUGUI>();
                if (msyhFont != null) tmp.font = msyhFont;
                tmp.text = "下一页 ▶";
                tmp.fontSize = 15;
                tmp.fontStyle = TMPro.FontStyles.Bold;
                tmp.alignment = TMPro.TextAlignmentOptions.Center;
                tmp.color = Color.white;
                tmp.raycastTarget = false;

                nextPageButton = btn;
            }

            // 4. 调整 ScrollView 下边距并锁死拖拽，预留底部空间给翻页工具栏
            if (scrollRect != null)
            {
                scrollRect.vertical = false;
                scrollRect.horizontal = false;
                scrollRect.movementType = ScrollRect.MovementType.Clamped;

                var scrollTransform = scrollRect.GetComponent<RectTransform>();
                if (scrollTransform != null && scrollTransform.sizeDelta.y > -120f)
                {
                    scrollTransform.sizeDelta = new Vector2(scrollTransform.sizeDelta.x, -140f);
                    scrollTransform.anchoredPosition = new Vector2(scrollTransform.anchoredPosition.x, -10f);
                }
            }
        }

        private void SetToggleText(Toggle toggle, string text)
        {
            if (toggle == null) return;
            var tmp = toggle.GetComponentInChildren<TMPro.TMP_Text>(true);
            if (tmp != null) 
            {
                tmp.text = text;
                return;
            }
            var legacyTxt = toggle.GetComponentInChildren<Text>(true);
            if (legacyTxt != null) 
            {
                legacyTxt.text = text;
            }
        }

        private RoomCardItem GetCardFromPool()
        {
            foreach (var card in cardPool)
            {
                if (card != null && !card.gameObject.activeSelf)
                {
                    return card;
                }
            }

            var newObj = Instantiate(roomCardPrefab, contentPanel);
            var newCard = newObj.GetComponent<RoomCardItem>();
            cardPool.Add(newCard);
            return newCard;
        }

        private void ClearActiveCards()
        {
            foreach (var card in activeCards)
            {
                if (card != null) card.gameObject.SetActive(false);
            }
            activeCards.Clear();
        }
    }
}
