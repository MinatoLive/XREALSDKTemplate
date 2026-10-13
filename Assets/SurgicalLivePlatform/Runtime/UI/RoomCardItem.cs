using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using SurgicalLivePlatform.Runtime.DTOs;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SurgicalLivePlatform.Runtime.UI
{
    public class RoomCardItem : MonoBehaviour
    {
  
        // 运行时通过 UIMark 自动收集，不再暴露于面板
        private GameObject cover_image;
        private GameObject id_txt;
        private GameObject roomname_txt;
        private GameObject display_txt;
        private GameObject participant_txt;
        private GameObject status;
        private GameObject stream_type;
        private GameObject started_at;
        private Button Login_btn;

        [Header("Global Config")]
        [Tooltip("如果分配了此配置文件，运行后将优先加载配置文件的样式设置。")]
        [SerializeField] private RoomCardStyleConfig styleConfig;

        private RoomItem currentRoomItem;
        public RoomItem BoundRoom => currentRoomItem;
        
        private static RoomCardItem currentSelectedCard;

        private void Awake()
        {
            var uiNodes = new Dictionary<string, GameObject>();
            var marks = GetComponentsInChildren<UIMark>(true);
            foreach (var m in marks)
            {
                if (!string.IsNullOrEmpty(m.markName))
                {
                    uiNodes[m.markName] = m.gameObject;
                }
            }

            GameObject GetNode(string markName)
            {
                return uiNodes.TryGetValue(markName, out GameObject obj) ? obj : null;
            }

            cover_image = GetNode("cover_image");
            id_txt = GetNode("id_txt");
            display_txt = GetNode("display_txt");
            participant_txt = GetNode("participant_txt");
            status = GetNode("status");
            stream_type = GetNode("stream_type");
            started_at = GetNode("started_at");
            roomname_txt = GetNode("NameText");
            var loginBtnObj = GetNode("Login_btn");
            if (loginBtnObj != null)
            {
                Login_btn = loginBtnObj.GetComponent<Button>();
            }

            if (Login_btn != null)
            {
                Login_btn.onClick.RemoveAllListeners();
                Login_btn.onClick.AddListener(OnLoginButtonClicked);
            }
        }

        private void OnDestroy()
        {
            if (currentSelectedCard == this)
            {
                currentSelectedCard = null;
            }
        }

        private void OnLoginButtonClicked()
        {
            if (currentRoomItem == null) return;

            // 拦截不可进的房间（只允许 LIVE）
            if (currentRoomItem.status != "LIVE")
            {
                Debug.LogWarning($"[RoomCardItem] 房间状态为 {currentRoomItem.status}，当前不允许进入。");
            }
            else
            {
                if (RoomJoinController.Instance != null)
                {
                    RoomJoinController.Instance.JoinRoom(currentRoomItem.id);
                }
                else
                {
                    Debug.LogError("[RoomCardItem] 找不到 RoomJoinController 实例，无法发起入会请求。");
                }
            }

            // 执行单选互斥样式
            if (currentSelectedCard != null && currentSelectedCard != this)
            {
                currentSelectedCard.ApplyStyle("unclick");
            }
            currentSelectedCard = this;
            ApplyStyle("onclick");
        }

        public async Task BindData(RoomItem roomItem, int sequenceIndex = -1)
        {
            this.currentRoomItem = roomItem;
            if (roomItem == null) return;

            // 首先重置为未点击的基础样式，防止它覆盖下方动态计算的专属颜色和文本
            ApplyStyle("unclick");

            // 1. 序号 (顺序序号)
            if (sequenceIndex >= 0)
            {
                SetText(id_txt, (sequenceIndex).ToString("00"));
            }

            // 2. 创建人名称
            string displayName = "系统发起";
            if (roomItem.creator != null)
            {
                displayName = string.IsNullOrEmpty(roomItem.creator.display_name) 
                    ? roomItem.creator.username 
                    : roomItem.creator.display_name;
            }
            SetText(display_txt, displayName);

            SetText(roomname_txt, roomItem.name);

            // 3. 在会人数//


            /*
             (o)
(o)
单目实时直播
    live·默认高清摄像头推流

单目录制视频推流
    record·调取已有手术录像

双目实时直播
    liveStereo - 3D 双目立体直播

双目录制视频推流
    recordStereo - 3D 录像推流

             
             
             */
            SetText(participant_txt, $"🔥 {roomItem.participant_count} 人在席");

            // 4. 复合型 UI 渲染 (Status & StreamType)
            if (status != null)
            {
                string statusText = roomItem.status;
                Color statusBgColor = new Color(0.9f, 0.9f, 0.9f, 1f); // 默认浅灰底
                Color statusTextColor = new Color(0.3f, 0.3f, 0.3f, 1f); // 默认深灰字

                switch (roomItem.status)
                {
                    case "LIVE":
                        statusText = "直播中";
                        statusBgColor = new Color(0.85f, 1f, 0.85f, 1f); // 浅绿色底
                        statusTextColor = new Color(0.1f, 0.5f, 0.1f, 1f); // 深绿色字
                        break;
                    case "CREATED":
                        statusText = "预约中";
                        statusBgColor = new Color(0.85f, 0.9f, 1f, 1f); // 浅蓝色底
                        statusTextColor = new Color(0.1f, 0.4f, 0.8f, 1f); // 深蓝色字
                        break;
                    case "ENDED":
                        statusText = "已结束";
                        statusBgColor = new Color(0.9f, 0.9f, 0.9f, 1f); // 浅灰色底
                        statusTextColor = new Color(0.4f, 0.4f, 0.4f, 1f); // 深灰色字
                        break;
                }

                SetText(status, statusText);
                SetTextColor(status, statusTextColor);
                var statusBg = status.GetComponent<Image>();
                if (statusBg != null)
                {
                    statusBg.color = statusBgColor;
                }
            }

            if (stream_type != null)
            {
                var (stText, stBgColor, stTextColor) = ResolveStreamTypeBadge(roomItem.stream_type);
                SetText(stream_type, stText);
                SetTextColor(stream_type, stTextColor);
                var streamBg = stream_type.GetComponent<Image>();
                if (streamBg != null)
                {
                    streamBg.color = stBgColor;
                }
            }

            // 5. 开始时间格式化 (ISO 8601 -> MM/dd HH:mm)
            if (!string.IsNullOrEmpty(roomItem.created_at) && System.DateTime.TryParse(roomItem.created_at, out System.DateTime dt))
            {
                SetText(started_at, dt.ToString("MM/dd HH:mm"));
            }
            else
            {
                SetText(started_at, roomItem.created_at);
            }

            // 6. 房间图片
            if (cover_image != null && !string.IsNullOrEmpty(roomItem.cover_image_url))
            {
                // TODO: 待重构: 接入真实的图片异步加载逻辑
                var rawImage = cover_image.GetComponent<RawImage>();
                rawImage.texture = await ImageLoader.LoadImageAsync(roomItem.cover_image_url);
            }

        }

        /// <summary>
        /// 解析并映射流媒体类型角标 (liveStereo -> 3D双目立体直播, RECORD -> 双目录制 3D)
        /// </summary>
        public static (string text, Color bgColor, Color textColor) ResolveStreamTypeBadge(string rawStreamType)
        {
            if (string.IsNullOrWhiteSpace(rawStreamType))
            {
                return ("实时直播 (LIVE)", new Color(0.85f, 1f, 0.85f, 1f), new Color(0.1f, 0.5f, 0.1f, 1f));
            }

            string normalized = rawStreamType.Trim().ToUpperInvariant();
            switch (normalized)
            {
                case "LIVESTEREO":
                case "LIVE_STEREO":
                    // 3D双目立体直播：高科技紫底 + 蓝紫文字
                    return ("3D双目立体直播", new Color(0.92f, 0.88f, 1f, 1f), new Color(0.45f, 0.15f, 0.85f, 1f));

                case "RECORD":
                case "RECORD_STEREO":
                case "RECORDSTEREO":
                    // 双目录制 3D：浅蓝底 + 深蓝文字
                    return ("双目录制 3D (3D RECORD)", new Color(0.85f, 0.9f, 1f, 1f), new Color(0.1f, 0.4f, 0.8f, 1f));

                case "LIVE":
                    // 实时直播：浅绿底 + 深绿文字
                    return ("实时直播 (LIVE)", new Color(0.85f, 1f, 0.85f, 1f), new Color(0.1f, 0.5f, 0.1f, 1f));

                default:
                    return (rawStreamType, new Color(0.9f, 0.9f, 0.9f, 1f), new Color(0.3f, 0.3f, 0.3f, 1f));
            }
        }

        private void ApplyStyle(string statusName)
        {
            if (styleConfig == null || styleConfig.statusStyles == null) return;
            
            RoomStatusUIStyle matchedStyle = styleConfig.statusStyles.Find(x => x.status == statusName);
            if (matchedStyle == null) matchedStyle = styleConfig.defaultStyle;

            if (matchedStyle != null && matchedStyle.elementOverrides != null)
            {
                foreach (var config in matchedStyle.elementOverrides)
                {
                    Transform target = string.IsNullOrEmpty(config.targetPath) ? this.transform : transform.Find(config.targetPath);
                    if (target == null) continue;

                    if (config.modifyActive) target.gameObject.SetActive(config.isActive);
                    
                    if (config.modifyColor)
                    {
                        var graphic = target.GetComponent<Graphic>();
                        if (graphic != null) graphic.color = config.color;
                    }

                    if (config.modifyFontSize)
                    {
                        var tmp = target.GetComponent<TMP_Text>();
                        if (tmp != null) tmp.fontSize = config.fontSize;
                        else
                        {
                            var txt = target.GetComponent<Text>();
                            if (txt != null) txt.fontSize = (int)config.fontSize;
                        }
                    }

                    if (config.modifySprite)
                    {
                        var img = target.GetComponent<Image>();
                        if (img != null) img.sprite = config.sprite;
                    }
                }
            }
        }

        private void SetText(GameObject go, string text)
        {
            if (go == null) return;
            
            // 使用 GetComponentInChildren 可以同时兼容 Text 挂在自身或挂在子节点的情况
            var tmp = go.GetComponentInChildren<TMP_Text>(true);
            if (tmp != null) 
            { 
                tmp.text = text; 
                return; 
            }
            var legacyTxt = go.GetComponentInChildren<Text>(true);
            if (legacyTxt != null) 
            { 
                legacyTxt.text = text; 
            }
        }

        private void SetTextColor(GameObject go, Color color)
        {
            if (go == null) return;
            var tmp = go.GetComponentInChildren<TMP_Text>(true);
            if (tmp != null) tmp.color = color;
            var legacyTxt = go.GetComponentInChildren<Text>(true);
            if (legacyTxt != null) legacyTxt.color = color;
        }


#if UNITY_EDITOR



        [ContextMenu("将当前样式生成并直接写入配置文件 (Generate & Save to Config)")]
        private void GenerateAndSaveToConfig()
        {
            if (styleConfig == null)
            {
                Debug.LogError("请先在 styleConfig 槽位中分配一个 RoomCardStyleConfig 配置文件！");
                return;
            }

            UnityEditor.Undo.RecordObject(styleConfig, "Generate Style Template");

            RoomStatusUIStyle newStyle = new RoomStatusUIStyle { status = "NEW_STATUS_TEMPLATE" };
            newStyle.elementOverrides = new List<UIElementOverride>();

            var graphics = GetComponentsInChildren<Graphic>(true);
            foreach (var g in graphics)
            {
                UIElementOverride element = new UIElementOverride();
                element.targetPath = GetRelativePath(g.transform);
                element.componentName = g.gameObject.name;

                element.modifyActive = true;
                element.isActive = g.gameObject.activeSelf;

                element.modifyColor = true;
                element.color = g.color;

                if (g is TMP_Text tmp)
                {
                    element.modifyFontSize = true;
                    element.fontSize = tmp.fontSize;
                }
                else if (g is Text txt)
                {
                    element.modifyFontSize = true;
                    element.fontSize = txt.fontSize;
                }
                else if (g is Image img)
                {
                    element.modifySprite = true;
                    element.sprite = img.sprite;
                }

                newStyle.elementOverrides.Add(element);
            }

            if (styleConfig.statusStyles == null) styleConfig.statusStyles = new List<RoomStatusUIStyle>();
            styleConfig.statusStyles.Add(newStyle);

            UnityEditor.EditorUtility.SetDirty(styleConfig);
            UnityEditor.AssetDatabase.SaveAssets();

            Debug.Log($"成功遍历了 {graphics.Length} 个UI元素并将配置直接写入到持久化文件: {styleConfig.name}");
        }

        private string GetRelativePath(Transform target)
        {
            if (target == this.transform) return "";
            string path = target.name;
            Transform parent = target.parent;
            while (parent != null && parent != this.transform)
            {
                path = parent.name + "/" + path;
                parent = parent.parent;
            }
            return path;
        }
#endif
    }

    [System.Serializable]
    public class RoomStatusUIStyle
    {
        [Tooltip("房间状态匹配字符串，如 LIVE, CREATED, ENDED")]
        public string status; 
        
        [Tooltip("覆盖的所有UI元素配置项")]
        public List<UIElementOverride> elementOverrides = new List<UIElementOverride>();
    }

    [System.Serializable]
    public class UIElementOverride
    {
        [Tooltip("节点相对路径")]
        public string targetPath;
        [Tooltip("节点名称(仅用作面板展示辨别)")]
        public string componentName;

        public bool modifyActive;
        public bool isActive;

        public bool modifyColor;
        public Color color = Color.white;

        public bool modifyFontSize;
        public float fontSize = 14f;

        public bool modifySprite;
        public Sprite sprite;
    }
}
