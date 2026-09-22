using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using SurgicalLivePlatform.Runtime.UI;
using SurgicalLivePlatform.Example.Runtime.UI;

public static class MergeSceneBuilder
{
    [MenuItem("Tools/Merge Scenes")]
    public static void Merge()
    {
        string loginScenePath = "Assets/SurgicalLivePlatform/Example/Scene/UserLogin.unity";
        string mergedScenePath = "Assets/SurgicalLivePlatform/Example/Scene/AppMainScene.unity";

        var scene = EditorSceneManager.OpenScene(loginScenePath);

        Canvas canvas = Object.FindObjectOfType<Canvas>();
        if (canvas == null) {
            Debug.LogError("No canvas found in UserLogin scene!");
            return;
        }

        UserLoginUIController loginCtrl = Object.FindObjectOfType<UserLoginUIController>();
        GameObject loginPanelGo = null;

        if (loginCtrl != null && loginCtrl.gameObject != canvas.gameObject) {
            loginPanelGo = loginCtrl.gameObject;
            loginPanelGo.name = "LoginPanel";
        } else {
            loginPanelGo = new GameObject("LoginPanel");
            loginPanelGo.transform.SetParent(canvas.transform, false);
            var rect = loginPanelGo.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.sizeDelta = Vector2.zero;
            
            for (int i = canvas.transform.childCount - 1; i >= 0; i--) {
                Transform child = canvas.transform.GetChild(i);
                if (child != loginPanelGo.transform) {
                    child.SetParent(loginPanelGo.transform, false);
                }
            }
        }

        var lobbyPanelGo = new GameObject("LobbyPanel");
        lobbyPanelGo.transform.SetParent(canvas.transform, false);
        var lobbyRect = lobbyPanelGo.AddComponent<RectTransform>();
        lobbyRect.anchorMin = Vector2.zero;
        lobbyRect.anchorMax = Vector2.one;
        lobbyRect.sizeDelta = Vector2.zero;

        var lobbyView = lobbyPanelGo.AddComponent<RoomLobbyView>();

        string prefabFolder = "Assets/SurgicalLivePlatform/Runtime/UI/Prefabs";
        var cardPrefabGo = AssetDatabase.LoadAssetAtPath<GameObject>(prefabFolder + "/RoomCardItem.prefab");

        var headerGo = new GameObject("Header");
        headerGo.transform.SetParent(lobbyPanelGo.transform, false);
        var headerRect = headerGo.AddComponent<RectTransform>();
        headerRect.anchorMin = new Vector2(0, 1);
        headerRect.anchorMax = new Vector2(1, 1);
        headerRect.pivot = new Vector2(0.5f, 1);
        headerRect.sizeDelta = new Vector2(0, 80);
        var headerBg = headerGo.AddComponent<Image>();
        headerBg.color = new Color(0.2f, 0.2f, 0.2f);

        var titleGo = new GameObject("Title");
        titleGo.transform.SetParent(headerGo.transform, false);
        var titleText = titleGo.AddComponent<TextMeshProUGUI>();
        titleText.text = "手术大厅";
        titleText.fontSize = 36;
        titleText.alignment = TextAlignmentOptions.Left;
        var titleRect = titleGo.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0, 0);
        titleRect.anchorMax = new Vector2(1, 1);
        titleRect.offsetMin = new Vector2(40, 0);

        var createBtnGo = new GameObject("CreateRoomButton");
        createBtnGo.transform.SetParent(headerGo.transform, false);
        var createBtn = createBtnGo.AddComponent<Button>();
        var createBtnImg = createBtnGo.AddComponent<Image>();
        createBtnImg.color = Color.blue;
        var createBtnRect = createBtnGo.GetComponent<RectTransform>();
        createBtnRect.anchorMin = new Vector2(1, 0.5f);
        createBtnRect.anchorMax = new Vector2(1, 0.5f);
        createBtnRect.sizeDelta = new Vector2(160, 50);
        createBtnRect.anchoredPosition = new Vector2(-100, 0);

        var btnTextGo = new GameObject("Text");
        btnTextGo.transform.SetParent(createBtnGo.transform, false);
        var btnText = btnTextGo.AddComponent<TextMeshProUGUI>();
        btnText.text = "新建手术房间";
        btnText.fontSize = 20;
        btnText.alignment = TextAlignmentOptions.Center;
        btnText.GetComponent<RectTransform>().sizeDelta = new Vector2(160, 50);

        var svGo = new GameObject("ScrollView");
        svGo.transform.SetParent(lobbyPanelGo.transform, false);
        var svRect = svGo.AddComponent<RectTransform>();
        svRect.anchorMin = Vector2.zero;
        svRect.anchorMax = Vector2.one;
        svRect.offsetMax = new Vector2(0, -80);
        svRect.offsetMin = Vector2.zero;

        var viewportGo = new GameObject("Viewport");
        viewportGo.transform.SetParent(svGo.transform, false);
        var vpRect = viewportGo.AddComponent<RectTransform>();
        vpRect.anchorMin = Vector2.zero;
        vpRect.anchorMax = Vector2.one;
        vpRect.sizeDelta = Vector2.zero;
        viewportGo.AddComponent<Image>().color = new Color(0,0,0,0.5f);
        viewportGo.AddComponent<Mask>().showMaskGraphic = false;

        var contentGo = new GameObject("Content");
        contentGo.transform.SetParent(viewportGo.transform, false);
        var contentRect = contentGo.AddComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0, 1);
        contentRect.anchorMax = new Vector2(1, 1);
        contentRect.pivot = new Vector2(0.5f, 1);
        var vlg = contentGo.AddComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(20, 20, 20, 20);
        vlg.spacing = 20;
        vlg.childControlHeight = false;
        vlg.childControlWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.childForceExpandWidth = true;
        var csf = contentGo.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var scrollRect = svGo.AddComponent<ScrollRect>();
        scrollRect.content = contentRect;
        scrollRect.viewport = vpRect;
        scrollRect.horizontal = false;

        var loaderGo = new GameObject("LoadingIndicator");
        loaderGo.transform.SetParent(lobbyPanelGo.transform, false);
        var loaderText = loaderGo.AddComponent<TextMeshProUGUI>();
        loaderText.text = "Loading...";
        loaderText.alignment = TextAlignmentOptions.Center;
        var loaderRect = loaderGo.GetComponent<RectTransform>();
        loaderRect.anchorMin = new Vector2(0, 0);
        loaderRect.anchorMax = new Vector2(1, 0);
        loaderRect.sizeDelta = new Vector2(0, 50);
        loaderGo.SetActive(false);

        var modalGo = new GameObject("RoomModal");
        var modalRect = modalGo.AddComponent<RectTransform>();
        modalRect.anchorMin = Vector2.zero;
        modalRect.anchorMax = Vector2.one;
        modalRect.sizeDelta = Vector2.zero;
        var modalBg = modalGo.AddComponent<Image>();
        modalBg.color = new Color(0, 0, 0, 0.6f);

        var modalView = modalGo.AddComponent<RoomModalView>();

        var panelGo = new GameObject("Panel");
        panelGo.transform.SetParent(modalGo.transform, false);
        var panelRect = panelGo.AddComponent<RectTransform>();
        panelRect.sizeDelta = new Vector2(700, 800);
        panelGo.AddComponent<Image>().color = new Color(0.98f, 0.98f, 0.98f);
        
        var vlgModal = panelGo.AddComponent<VerticalLayoutGroup>();
        vlgModal.padding = new RectOffset(40, 40, 40, 40);
        vlgModal.spacing = 20;
        vlgModal.childControlHeight = false;
        vlgModal.childControlWidth = true;
        vlgModal.childForceExpandHeight = false;
        vlgModal.childForceExpandWidth = true;
        
        // Title
        var titleTextModal = CreateText(panelGo, "Title", "预约手术直播与协同会议", 24, true);
        var subTitleText = CreateText(panelGo, "SubTitle", "安排未来的达芬奇机器人手术转播、多学科会诊或示教培训", 14, false);
        subTitleText.color = Color.gray;

        // Field: Name
        CreateText(panelGo, "NameLabel", "会议/手术名称 *", 16, true);
        var nameInput = CreateInputField(panelGo, "NameInput", 50);

        // Field: Dept
        CreateText(panelGo, "DeptLabel", "所属临床专科 / 科室 *", 16, true);
        var deptDropdown = CreateDropdown(panelGo, "DepartmentDropdown", 50);
        
        // Horizontal group for Date and Duration
        var timeRowGo = new GameObject("TimeRow");
        timeRowGo.transform.SetParent(panelGo.transform, false);
        var timeRowRect = timeRowGo.AddComponent<RectTransform>();
        timeRowRect.sizeDelta = new Vector2(0, 80);
        var hlg = timeRowGo.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 20;
        hlg.childControlHeight = true;
        hlg.childControlWidth = true;
        hlg.childForceExpandWidth = true;

        var dateCol = new GameObject("DateCol");
        dateCol.transform.SetParent(timeRowGo.transform, false);
        var dateVlg = dateCol.AddComponent<VerticalLayoutGroup>();
        dateVlg.childControlHeight = false; dateVlg.childControlWidth = true;
        dateVlg.spacing = 10;
        CreateText(dateCol, "DateLabel", "预约开播时间 *", 16, true);
        var dateInput = CreateInputField(dateCol, "DateInput", 50);

        var durCol = new GameObject("DurCol");
        durCol.transform.SetParent(timeRowGo.transform, false);
        var durVlg = durCol.AddComponent<VerticalLayoutGroup>();
        durVlg.childControlHeight = false; durVlg.childControlWidth = true;
        durVlg.spacing = 10;
        CreateText(durCol, "DurLabel", "预计时长 (分钟)", 16, true);
        var durDropdown = CreateDropdown(durCol, "DurationDropdown", 50);

        // Field: Desc
        CreateText(panelGo, "DescLabel", "手术/会议说明", 16, true);
        var descInput = CreateInputField(panelGo, "DescInput", 120);

        // Field: Invitee
        CreateText(panelGo, "InviteeLabel", "邀请人 / 发起专家", 16, true);
        var inviteeInput = CreateInputField(panelGo, "InviteeInput", 50);

        // Buttons Row
        var btnRowGo = new GameObject("ButtonRow");
        btnRowGo.transform.SetParent(panelGo.transform, false);
        var btnRowRect = btnRowGo.AddComponent<RectTransform>();
        btnRowRect.sizeDelta = new Vector2(0, 60);
        var btnHlg = btnRowGo.AddComponent<HorizontalLayoutGroup>();
        btnHlg.childAlignment = TextAnchor.MiddleRight;
        btnHlg.spacing = 20;
        btnHlg.childControlHeight = false;
        btnHlg.childControlWidth = false;

        var closeBtn = CreateButton(btnRowGo, "CloseButton", "取消", new Vector2(100, 50), new Color(0.9f, 0.9f, 0.9f));
        var submitBtn = CreateButton(btnRowGo, "SubmitButton", "确认预约会议", new Vector2(180, 50), new Color(0.1f, 0.6f, 0.2f));
        submitBtn.GetComponentInChildren<TextMeshProUGUI>().color = Color.white;

        // Required hidden error texts for RoomModalView
        var nameErrGo = new GameObject("NameError");
        nameErrGo.transform.SetParent(panelGo.transform, false);
        var nameErrText = nameErrGo.AddComponent<TextMeshProUGUI>();
        nameErrText.color = Color.red;
        nameErrText.fontSize = 12;
        nameErrGo.SetActive(false); // Just keep references

        var nameCharText = new GameObject("NameChar").AddComponent<TextMeshProUGUI>();
        var descCharText = new GameObject("DescChar").AddComponent<TextMeshProUGUI>();

        var soModal = new SerializedObject(modalView);
        soModal.FindProperty("nameInput").objectReferenceValue = nameInput;
        soModal.FindProperty("nameErrorText").objectReferenceValue = nameErrText;
        soModal.FindProperty("nameCharCountText").objectReferenceValue = nameCharText;
        soModal.FindProperty("descInput").objectReferenceValue = descInput;
        soModal.FindProperty("descCharCountText").objectReferenceValue = descCharText;
        soModal.FindProperty("coverUrlInput").objectReferenceValue = dateInput; // Mock mapped
        soModal.FindProperty("hospitalDropdown").objectReferenceValue = deptDropdown; // Mock mapped
        soModal.FindProperty("departmentDropdown").objectReferenceValue = durDropdown; // Mock mapped
        soModal.FindProperty("submitButton").objectReferenceValue = submitBtn;
        soModal.FindProperty("closeButton").objectReferenceValue = closeBtn;
        soModal.ApplyModifiedProperties();

        modalGo.transform.SetParent(lobbyPanelGo.transform, false);
        modalGo.SetActive(false);

        var soLobby = new SerializedObject(lobbyView);
        soLobby.FindProperty("scrollRect").objectReferenceValue = scrollRect;
        soLobby.FindProperty("contentPanel").objectReferenceValue = contentRect;
        soLobby.FindProperty("roomCardPrefab").objectReferenceValue = cardPrefabGo;
        soLobby.FindProperty("createRoomButton").objectReferenceValue = createBtn;
        soLobby.FindProperty("loadingIndicator").objectReferenceValue = loaderGo;
        soLobby.ApplyModifiedProperties();

        var appFlowGo = new GameObject("AppFlowController");
        var appFlow = appFlowGo.AddComponent<AppFlowController>();

        var soApp = new SerializedObject(appFlow);
        soApp.FindProperty("loginPanel").objectReferenceValue = loginPanelGo;
        soApp.FindProperty("lobbyPanel").objectReferenceValue = lobbyPanelGo;
        soApp.FindProperty("lobbyView").objectReferenceValue = lobbyView;
        soApp.ApplyModifiedProperties();

        EditorSceneManager.SaveScene(scene, mergedScenePath);
        Debug.Log("Successfully merged into AppMainScene.unity");
    }

    [MenuItem("Tools/Rebuild Room Modal (AppMainScene)")]
    public static void RebuildRoomModalOnly()
    {
        string scenePath = "Assets/SurgicalLivePlatform/Example/Scene/AppMainScene.unity";
        var scene = EditorSceneManager.OpenScene(scenePath);
        
        var objs = Resources.FindObjectsOfTypeAll<GameObject>();
        GameObject roomModal = null;
        GameObject lobbyPanelGo = null;
        foreach(var o in objs) {
            if (o.name == "RoomModal" && o.scene == scene) roomModal = o;
            if (o.name == "LobbyPanel" && o.scene == scene) lobbyPanelGo = o;
        }
        
        if (roomModal == null || lobbyPanelGo == null) {
            Debug.LogError("RoomModal or LobbyPanel not found in AppMainScene!");
            return;
        }

        // 1. Rebuild Header in LobbyPanel for dual buttons
        var oldHeader = lobbyPanelGo.transform.Find("Header");
        if (oldHeader != null) Object.DestroyImmediate(oldHeader.gameObject);

        var headerGo = new GameObject("Header");
        headerGo.transform.SetParent(lobbyPanelGo.transform, false);
        headerGo.transform.SetAsFirstSibling();
        var headerRect = headerGo.AddComponent<RectTransform>();
        headerRect.anchorMin = new Vector2(0, 1);
        headerRect.anchorMax = new Vector2(1, 1);
        headerRect.pivot = new Vector2(0.5f, 1);
        headerRect.sizeDelta = new Vector2(0, 80);
        var headerBg = headerGo.AddComponent<Image>();
        headerBg.color = new Color(0.2f, 0.2f, 0.2f);

        var titleGo = new GameObject("Title");
        titleGo.transform.SetParent(headerGo.transform, false);
        var titleText = titleGo.AddComponent<TextMeshProUGUI>();
        titleText.text = "手术大厅";
        titleText.fontSize = 36;
        titleText.alignment = TextAlignmentOptions.Left;
        var titleRect = titleGo.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0, 0);
        titleRect.anchorMax = new Vector2(1, 1);
        titleRect.offsetMin = new Vector2(40, 0);

        var immediateBtn = CreateButton(headerGo, "ImmediateLiveBtn", "立即开播", new Vector2(140, 50), Color.blue);
        var immediateBtnRect = immediateBtn.GetComponent<RectTransform>();
        immediateBtnRect.anchorMin = new Vector2(1, 0.5f); immediateBtnRect.anchorMax = new Vector2(1, 0.5f);
        immediateBtnRect.anchoredPosition = new Vector2(-260, 0);

        var scheduledBtn = CreateButton(headerGo, "ScheduledLiveBtn", "预约直播", new Vector2(140, 50), new Color(0.1f, 0.6f, 0.2f));
        var scheduledBtnRect = scheduledBtn.GetComponent<RectTransform>();
        scheduledBtnRect.anchorMin = new Vector2(1, 0.5f); scheduledBtnRect.anchorMax = new Vector2(1, 0.5f);
        scheduledBtnRect.anchoredPosition = new Vector2(-100, 0);

        var lobbyView = lobbyPanelGo.GetComponent<RoomLobbyView>();
        var soLobby = new SerializedObject(lobbyView);
        soLobby.FindProperty("immediateLiveButton").objectReferenceValue = immediateBtn;
        soLobby.FindProperty("scheduledLiveButton").objectReferenceValue = scheduledBtn;
        soLobby.ApplyModifiedProperties();

        // 2. Rebuild Modal
        var modalView = roomModal.GetComponent<RoomModalView>();
        var oldPanel = roomModal.transform.Find("Panel");
        if (oldPanel != null) Object.DestroyImmediate(oldPanel.gameObject);

        var panelGo = new GameObject("Panel");
        panelGo.transform.SetParent(roomModal.transform, false);
        var panelRect = panelGo.AddComponent<RectTransform>();
        panelRect.sizeDelta = new Vector2(700, 800);
        panelGo.AddComponent<Image>().color = new Color(0.98f, 0.98f, 0.98f);
        
        var vlgModal = panelGo.AddComponent<VerticalLayoutGroup>();
        vlgModal.padding = new RectOffset(40, 40, 40, 40);
        vlgModal.spacing = 20;
        vlgModal.childControlHeight = false; vlgModal.childControlWidth = true;
        vlgModal.childForceExpandHeight = false; vlgModal.childForceExpandWidth = true;
        
        CreateText(panelGo, "Title", "直播与会议", 24, true);
        var subTitleText = CreateText(panelGo, "SubTitle", "填写以下信息即可开播或预约", 14, false);
        subTitleText.color = Color.gray;

        CreateText(panelGo, "NameLabel", "会议/手术名称 *", 16, true);
        var nameInput = CreateInputField(panelGo, "NameInput", 50);

        CreateText(panelGo, "DeptLabel", "所属临床专科 / 科室 *", 16, true);
        var deptDropdown = CreateDropdown(panelGo, "DepartmentDropdown", 50);
        
        var timeRowGo = new GameObject("TimeRow");
        timeRowGo.transform.SetParent(panelGo.transform, false);
        var timeRowRect = timeRowGo.AddComponent<RectTransform>();
        timeRowRect.sizeDelta = new Vector2(0, 80);
        var hlg = timeRowGo.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 20; hlg.childControlHeight = true; hlg.childControlWidth = true; hlg.childForceExpandWidth = true;

        var dateCol = new GameObject("DateCol");
        dateCol.transform.SetParent(timeRowGo.transform, false);
        var dateVlg = dateCol.AddComponent<VerticalLayoutGroup>();
        dateVlg.childControlHeight = false; dateVlg.childControlWidth = true; dateVlg.spacing = 10;
        CreateText(dateCol, "DateLabel", "预约开播时间 *", 16, true);
        var dateInput = CreateInputField(dateCol, "DateInput", 50);

        var durCol = new GameObject("DurCol");
        durCol.transform.SetParent(timeRowGo.transform, false);
        var durVlg = durCol.AddComponent<VerticalLayoutGroup>();
        durVlg.childControlHeight = false; durVlg.childControlWidth = true; durVlg.spacing = 10;
        CreateText(durCol, "DurLabel", "预计时长 (分钟)", 16, true);
        var durDropdown = CreateDropdown(durCol, "DurationDropdown", 50);

        CreateText(panelGo, "DescLabel", "手术/会议说明", 16, true);
        var descInput = CreateInputField(panelGo, "DescInput", 120);

        CreateText(panelGo, "InviteeLabel", "邀请人 / 发起专家", 16, true);
        var inviteeInput = CreateInputField(panelGo, "InviteeInput", 50);

        var btnRowGo = new GameObject("ButtonRow");
        btnRowGo.transform.SetParent(panelGo.transform, false);
        var btnRowRect = btnRowGo.AddComponent<RectTransform>();
        btnRowRect.sizeDelta = new Vector2(0, 60);
        var btnHlg = btnRowGo.AddComponent<HorizontalLayoutGroup>();
        btnHlg.childAlignment = TextAnchor.MiddleRight; btnHlg.spacing = 20;
        btnHlg.childControlHeight = false; btnHlg.childControlWidth = false;

        var closeBtn = CreateButton(btnRowGo, "CloseButton", "取消", new Vector2(100, 50), new Color(0.9f, 0.9f, 0.9f));
        var submitBtn = CreateButton(btnRowGo, "SubmitButton", "确认", new Vector2(180, 50), new Color(0.1f, 0.6f, 0.2f));

        var nameErrGo = new GameObject("NameError");
        nameErrGo.transform.SetParent(panelGo.transform, false);
        var nameErrText = nameErrGo.AddComponent<TextMeshProUGUI>();
        nameErrText.color = Color.red; nameErrText.fontSize = 12;
        nameErrGo.SetActive(false);

        var nameCharText = CreateText(panelGo, "NameChar", "", 12, false);
        var descCharText = CreateText(panelGo, "DescChar", "", 12, false);

        var soModal = new SerializedObject(modalView);
        soModal.FindProperty("nameInput").objectReferenceValue = nameInput;
        soModal.FindProperty("nameErrorText").objectReferenceValue = nameErrText;
        soModal.FindProperty("nameCharCountText").objectReferenceValue = nameCharText;
        soModal.FindProperty("descInput").objectReferenceValue = descInput;
        soModal.FindProperty("descCharCountText").objectReferenceValue = descCharText;
        soModal.FindProperty("coverUrlInput").objectReferenceValue = dateInput; // Mock
        soModal.FindProperty("departmentDropdown").objectReferenceValue = deptDropdown; 
        soModal.FindProperty("durationDropdown").objectReferenceValue = durDropdown; 
        soModal.FindProperty("timeRow").objectReferenceValue = timeRowGo; 
        soModal.FindProperty("submitButton").objectReferenceValue = submitBtn;
        soModal.FindProperty("closeButton").objectReferenceValue = closeBtn;
        soModal.ApplyModifiedProperties();
        
        SetLayerRecursively(headerGo, LayerMask.NameToLayer("UI"));
        SetLayerRecursively(panelGo, LayerMask.NameToLayer("UI"));

        EditorSceneManager.SaveScene(scene, scenePath);
        Debug.Log("Successfully rebuilt RoomModal and Lobby Header in AppMainScene.unity");
    }

    private static void SetLayerRecursively(GameObject obj, int layer)
    {
        obj.layer = layer;
        foreach (Transform child in obj.transform)
        {
            SetLayerRecursively(child.gameObject, layer);
        }
    }

    private static TextMeshProUGUI CreateText(GameObject parent, string name, string text, int size, bool isBold)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent.transform, false);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = size;
        tmp.color = new Color(0.2f, 0.2f, 0.2f);
        if (isBold) tmp.fontStyle = FontStyles.Bold;
        var rect = go.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(0, size + 10);
        return tmp;
    }

    private static TMP_InputField CreateInputField(GameObject parent, string name, float height)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent.transform, false);
        var rect = go.AddComponent<RectTransform>();
        rect.sizeDelta = new Vector2(0, height);
        var img = go.AddComponent<Image>();
        img.color = new Color(0.95f, 0.95f, 0.95f);
        var input = go.AddComponent<TMP_InputField>();
        
        var textArea = new GameObject("Text Area");
        textArea.transform.SetParent(go.transform, false);
        var textAreaRect = textArea.AddComponent<RectTransform>();
        textAreaRect.anchorMin = Vector2.zero;
        textAreaRect.anchorMax = Vector2.one;
        textAreaRect.offsetMin = new Vector2(10, 10);
        textAreaRect.offsetMax = new Vector2(-10, -10);
        textArea.AddComponent<RectMask2D>();
        
        var text = new GameObject("Text");
        text.transform.SetParent(textArea.transform, false);
        var tmp = text.AddComponent<TextMeshProUGUI>();
        tmp.color = Color.black;
        tmp.fontSize = 16;
        
        input.textViewport = textAreaRect;
        input.textComponent = tmp;
        return input;
    }

    private static TMP_Dropdown CreateDropdown(GameObject parent, string name, float height)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent.transform, false);
        var rect = go.AddComponent<RectTransform>();
        rect.sizeDelta = new Vector2(0, height);
        var img = go.AddComponent<Image>();
        img.color = new Color(0.95f, 0.95f, 0.95f);
        var dropdown = go.AddComponent<TMP_Dropdown>();

        var label = new GameObject("Label");
        label.transform.SetParent(go.transform, false);
        var labelRect = label.AddComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero; labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(10, 0);
        var tmp = label.AddComponent<TextMeshProUGUI>();
        tmp.color = Color.black;
        tmp.fontSize = 16;
        tmp.alignment = TextAlignmentOptions.Left;
        
        dropdown.captionText = tmp;
        return dropdown;
    }

    private static Button CreateButton(GameObject parent, string name, string text, Vector2 size, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent.transform, false);
        var rect = go.AddComponent<RectTransform>();
        rect.sizeDelta = size;
        var img = go.AddComponent<Image>();
        img.color = color;
        var btn = go.AddComponent<Button>();

        var label = new GameObject("Text");
        label.transform.SetParent(go.transform, false);
        var labelRect = label.AddComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero; labelRect.anchorMax = Vector2.one;
        var tmp = label.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.color = new Color(0.2f, 0.2f, 0.2f);
        tmp.fontSize = 18;
        tmp.alignment = TextAlignmentOptions.Center;
        if (color != Color.white && color != new Color(0.9f, 0.9f, 0.9f)) tmp.color = Color.white;

        return btn;
    }
}
