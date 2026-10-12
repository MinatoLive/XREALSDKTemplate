using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using SurgicalLivePlatform.Runtime;
using SurgicalLivePlatform.Runtime.DTOs;

namespace SurgicalLivePlatform.Runtime.UI
{
    public class RoomLobbyView : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private ScrollRect scrollRect;
        [SerializeField] private RectTransform contentPanel;
        [SerializeField] private GameObject roomCardPrefab;
        [SerializeField] private Button immediateLiveButton;
        [SerializeField] private Button scheduledLiveButton;
       // [SerializeField] private GameObject loadingIndicator;
        [SerializeField] private RoomModalView roomModalView;

        [Header("Settings")]
        [SerializeField] private float loadMoreThreshold = 0.2f; // 80% scroll (1 - 0.8)

        private List<RoomCardItem> cardPool = new List<RoomCardItem>();
        private List<RoomCardItem> activeCards = new List<RoomCardItem>();

        private int currentPage = 1;
        private bool isLoading = false;
        private bool hasMoreData = true;
        private const int PAGE_SIZE = 20;

        private Toggle filterAll;
        private Toggle filterLive;
        private Toggle filterCreated;
        private Toggle filterEnded;
        private string currentFilterStatus = ""; // "" 表示全部

        private void OnEnable()
        {
            // 利用 Unity 原生的生命周期，当大厅面板被激活时，自动刷新数据
            RefreshRooms();
        }

        private void Start()
        {
            scrollRect.onValueChanged.AddListener(OnScrollValueChanged);
            
            if (immediateLiveButton != null)
            {
                immediateLiveButton.onClick.AddListener(OnImmediateLiveClicked);
            }
            if (scheduledLiveButton != null)
            {
                scheduledLiveButton.onClick.AddListener(OnScheduledLiveClicked);
            }

            BindFilterToggles();
            
            CheckCreatePermission();
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

            // 在代码层面强制接管互斥逻辑，防止在 Unity 面板中漏配 ToggleGroup
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

            // 强制初始化时激活“全部”，其他会自动被 ToggleGroup 设为 false
            if (filterAll != null)
            {
                filterAll.SetIsOnWithoutNotify(true);
            }
            ApplyFilter(""); // 强制触发一次全部状态
        }

        private void ApplyFilter(string status)
        {
            currentFilterStatus = status;
            UpdateCardVisibility();
        }

        private void UpdateCardVisibility()
        {
            foreach (var card in activeCards)
            {
                if (card.BoundRoom == null) continue;
                bool match = string.IsNullOrEmpty(currentFilterStatus) || card.BoundRoom.status == currentFilterStatus;
                card.gameObject.SetActive(match);
            }
        }

        private async void CheckCreatePermission()
        {
            bool hasPermission = false;
          
            try
            {
                if (SurgicalLiveSDK.Auth == null) return;
                string userId = SurgicalLiveSDK.Auth.CurrentUserId;
                if (!string.IsNullOrEmpty(userId))
                {
                    var userData = await SurgicalLiveSDK.Storage.GetUserAsync(userId);
                    if (userData != null && userData.user != null && userData.user.permissions != null)
                    {
                        var permissions = new List<string>(userData.user.permissions);
                        if (permissions.Contains("room.create"))
                        {
                            hasPermission = true;
                        }
                    }
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[RoomLobbyView] Failed to check permissions: {e.Message}");
            }
            
            if (immediateLiveButton != null) immediateLiveButton.gameObject.SetActive(hasPermission);
            if (scheduledLiveButton != null) scheduledLiveButton.gameObject.SetActive(hasPermission);
        }
        
        private void EnsureRoomModalView()
        {
            if (roomModalView == null)
            {
                roomModalView = Resources.FindObjectsOfTypeAll<RoomModalView>().FirstOrDefault();
                if (roomModalView == null)
                {
                    Debug.LogError("[RoomLobbyView] 找不到 RoomModalView 组件！");
                }
            }
        }

        private void OnImmediateLiveClicked()
        {
            EnsureRoomModalView();
            if (roomModalView == null) return;
            try { roomModalView.Open(RoomCreateMode.Immediate); }
            catch (System.Exception e) { Debug.LogError($"[RoomLobbyView] 打开立即直播面板异常: {e.Message}"); }
        }

        private void OnScheduledLiveClicked()
        {
            EnsureRoomModalView();
            if (roomModalView == null) return;
            try { roomModalView.Open(RoomCreateMode.Scheduled); }
            catch (System.Exception e) { Debug.LogError($"[RoomLobbyView] 打开预约直播面板异常: {e.Message}"); }
        }

        public async void RefreshRooms()
        {
            if (isLoading) return;

            currentPage = 1;
            hasMoreData = true;
            ClearActiveCards();
            
          //  loadingIndicator.SetActive(true);
            isLoading = true;

            try
            {
                var response = await SurgicalLiveSDK.Auth.GetRoomInfosAsync(currentPage, PAGE_SIZE, null, null, null);
                RenderRooms(response.list);
                
                if (response.list.Count < PAGE_SIZE)
                    hasMoreData = false;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Refresh failed: {e.Message}");
            }
            finally
            {
                isLoading = false;
              //  loadingIndicator.SetActive(false);
            }
        }

        public async void LoadNextPage()
        {
            if (isLoading || !hasMoreData) return;

            currentPage++;
            isLoading = true;
          //  loadingIndicator.SetActive(true);

            try
            {
                var response = await SurgicalLiveSDK.Auth.GetRoomInfosAsync(currentPage, PAGE_SIZE, null, null, null);
                RenderRooms(response.list);

                if (response.list.Count < PAGE_SIZE)
                    hasMoreData = false;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Load next page failed: {e.Message}");
                currentPage--; // Revert page on failure
            }
            finally
            {
                isLoading = false;
             //   loadingIndicator.SetActive(false);
            }
        }

        private void OnScrollValueChanged(Vector2 pos)
        {
            if (pos.y <= loadMoreThreshold && !isLoading && hasMoreData)
            {
                LoadNextPage();
            }
        }

        private void RenderRooms(List<RoomItem> rooms)
        {
            if (rooms == null) return;
            int index = 0;
            foreach (var room in rooms)
            {
                index++;
                var card = GetCardFromPool();
                // TODO: 待重构: RoomCardItem.BindData 已被移除，后续由用户重构数据填充逻辑
                card.BindData(room, index);
                
                bool match = string.IsNullOrEmpty(currentFilterStatus) || room.status == currentFilterStatus;
                card.gameObject.SetActive(match);
                activeCards.Add(card);

                if (!string.IsNullOrEmpty(room.creator_id))
                {
                    FetchAndUpdateCreatorInfo(card, room.creator_id);
                }
            }
            
            UpdateFilterCounts();
        }

        private void UpdateFilterCounts()
        {
            int liveCount = 0;
            int createdCount = 0;
            int endedCount = 0;
            int allCount = 0;

            foreach (var card in activeCards)
            {
                if (card.BoundRoom == null) continue;
                allCount++;
                switch (card.BoundRoom.status)
                {
                    case "LIVE": liveCount++; break;
                    case "CREATED": createdCount++; break;
                    case "ENDED": endedCount++; break;
                }
            }

            SetToggleText(filterAll, $"全部 ({allCount})");
            SetToggleText(filterLive, $"直播中 ({liveCount})");
            SetToggleText(filterCreated, $"已预约 ({createdCount})");
            SetToggleText(filterEnded, $"已结束 ({endedCount})");
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

        private async void FetchAndUpdateCreatorInfo(RoomCardItem card, string creatorId)
        {
           // Debug.Log($"[FetchAndUpdateCreatorInfo] Calling GetUserByIdAsync for creatorId: {creatorId}");
            try
            {
                var creatorInfo = await SurgicalLiveSDK.Auth.GetUserByIdAsync(creatorId);
                if (creatorInfo != null)
                {
                    Debug.Log($"[Room Creator Info] ID: {creatorInfo.id}, Name: {creatorInfo.name}, Username: {creatorInfo.username}, Phone: {creatorInfo.phone}, Hospital ID: {creatorInfo.hospital_id}, Department ID: {creatorInfo.department_id}");
                    // TODO: 待重构: RoomCardItem.UpdateCreatorInfo 已被移除，后续由用户重构创建者信息更新逻辑
                    // card.UpdateCreatorInfo(creatorInfo);
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"Failed to fetch creator info for {creatorId}: {e.Message}");
            }
        }

        private RoomCardItem GetCardFromPool()
        {
            foreach (var card in cardPool)
            {
                if (!card.gameObject.activeSelf)
                {
                    card.transform.SetAsLastSibling();
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
                card.gameObject.SetActive(false);
            }
            activeCards.Clear();
        }
    }
}
