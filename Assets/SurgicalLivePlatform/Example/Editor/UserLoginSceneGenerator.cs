using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using SurgicalLivePlatform.Example.Runtime.UI;

namespace SurgicalLivePlatform.Example.Editor
{
    public static class UserLoginSceneGenerator
    {
        [MenuItem("Tools/Generate User Login Scene")]
        public static void GenerateScene()
        {
            string folderPath = "Assets/SurgicalLivePlatform/Example/Scene";
            if (!AssetDatabase.IsValidFolder("Assets/SurgicalLivePlatform/Example"))
            {
                AssetDatabase.CreateFolder("Assets/SurgicalLivePlatform", "Example");
            }
            if (!AssetDatabase.IsValidFolder(folderPath))
            {
                AssetDatabase.CreateFolder("Assets/SurgicalLivePlatform/Example", "Scene");
            }

            string scenePath = $"{folderPath}/UserLogin.unity";
            
            // Create a new empty scene
            Scene newScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            
            // Camera
            GameObject cameraObj = new GameObject("Main Camera");
            Camera camera = cameraObj.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.1f, 0.1f, 0.1f);
            cameraObj.tag = "MainCamera";
            cameraObj.transform.position = new Vector3(0, 0, -10);

            // Canvas (World Space for 3D UI)
            GameObject canvasObj = new GameObject("LoginCanvas");
            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvasObj.AddComponent<CanvasScaler>();
            canvasObj.AddComponent<GraphicRaycaster>();
            
            RectTransform canvasRect = canvasObj.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(800, 600);
            canvasRect.localScale = new Vector3(0.002f, 0.002f, 0.002f);
            canvasRect.position = new Vector3(0, 0, 0);

            // Event System
            GameObject eventSystemObj = new GameObject("EventSystem");
            eventSystemObj.AddComponent<UnityEngine.EventSystems.EventSystem>();
            eventSystemObj.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();

            // Background Panel
            GameObject panelObj = new GameObject("BackgroundPanel");
            panelObj.transform.SetParent(canvasObj.transform, false);
            Image panelImage = panelObj.AddComponent<Image>();
            panelImage.color = new Color(0.2f, 0.2f, 0.2f, 0.8f);
            RectTransform panelRect = panelObj.GetComponent<RectTransform>();
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.one;
            panelRect.sizeDelta = Vector2.zero;

            // Title
            GameObject titleObj = new GameObject("TitleText");
            titleObj.transform.SetParent(panelObj.transform, false);
            TextMeshProUGUI titleText = titleObj.AddComponent<TextMeshProUGUI>();
            titleText.text = "Surgical Live - 用户登录";
            titleText.fontSize = 48;
            titleText.alignment = TextAlignmentOptions.Center;
            RectTransform titleRect = titleObj.GetComponent<RectTransform>();
            titleRect.sizeDelta = new Vector2(600, 80);
            titleRect.anchoredPosition = new Vector2(0, 200);

            // Account Input
            GameObject accountInputObj = CreateInputField("AccountInput", panelObj.transform, "请输入账号...");
            accountInputObj.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, 50);

            // Password Input
            GameObject passwordInputObj = CreateInputField("PasswordInput", panelObj.transform, "请输入密码...");
            passwordInputObj.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, -50);
            TMP_InputField passwordInputField = passwordInputObj.GetComponent<TMP_InputField>();

            // Show Password Toggle
            GameObject toggleObj = CreateToggle("ShowPasswordToggle", panelObj.transform, "显示密码");
            toggleObj.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, -120);
            Toggle toggle = toggleObj.GetComponent<Toggle>();

            // Login Button
            GameObject loginBtnObj = CreateButton("LoginButton", panelObj.transform, "登录");
            loginBtnObj.GetComponent<RectTransform>().anchoredPosition = new Vector2(-150, -220);
            Button loginBtn = loginBtnObj.GetComponent<Button>();

            // Exit Button
            GameObject exitBtnObj = CreateButton("ExitButton", panelObj.transform, "退出");
            exitBtnObj.GetComponent<RectTransform>().anchoredPosition = new Vector2(150, -220);
            Button exitBtn = exitBtnObj.GetComponent<Button>();

            // Add UI Controller
            UserLoginUIController uiController = canvasObj.AddComponent<UserLoginUIController>();
            uiController.accountInput = accountInputObj.GetComponent<TMP_InputField>();
            uiController.passwordInput = passwordInputField;
            uiController.showPasswordToggle = toggle;
            uiController.loginButton = loginBtn;
            uiController.exitButton = exitBtn;

            // Save Scene
            EditorSceneManager.SaveScene(newScene, scenePath);
            Debug.Log($"[UserLoginSceneGenerator] 成功生成场景: {scenePath}");
        }

        private static GameObject CreateInputField(string name, Transform parent, string placeholderText)
        {
            GameObject inputObj = DefaultControls.CreateInputField(new DefaultControls.Resources());
            inputObj.name = name;
            inputObj.transform.SetParent(parent, false);
            RectTransform rect = inputObj.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(400, 60);

            // Replace standard InputField with TMP_InputField
            GameObject.DestroyImmediate(inputObj.GetComponent<InputField>());
            Transform oldText = inputObj.transform.Find("Text");
            if (oldText != null) GameObject.DestroyImmediate(oldText.gameObject);
            Transform oldPlaceholder = inputObj.transform.Find("Placeholder");
            if (oldPlaceholder != null) GameObject.DestroyImmediate(oldPlaceholder.gameObject);
            
            Transform oldTextArea = inputObj.transform.Find("Text Area");
            if (oldTextArea != null) GameObject.DestroyImmediate(oldTextArea.gameObject);

            TMP_InputField tmpInput = inputObj.AddComponent<TMP_InputField>();
            
            // Text Area
            GameObject textArea = new GameObject("Text Area");
            textArea.transform.SetParent(inputObj.transform, false);
            RectTransform textAreaRect = textArea.AddComponent<RectTransform>();
            textAreaRect.anchorMin = Vector2.zero;
            textAreaRect.anchorMax = Vector2.one;
            textAreaRect.offsetMin = new Vector2(10, 6);
            textAreaRect.offsetMax = new Vector2(-10, -7);
            textArea.AddComponent<RectMask2D>();

            // Placeholder
            GameObject placeholder = new GameObject("Placeholder");
            placeholder.transform.SetParent(textArea.transform, false);
            TextMeshProUGUI pText = placeholder.AddComponent<TextMeshProUGUI>();
            pText.text = placeholderText;
            pText.color = new Color(0.1f, 0.1f, 0.1f, 0.5f);
            pText.fontSize = 24;
            pText.alignment = TextAlignmentOptions.Left;
            pText.enableWordWrapping = false;
            RectTransform pRect = placeholder.GetComponent<RectTransform>();
            pRect.anchorMin = Vector2.zero;
            pRect.anchorMax = Vector2.one;
            pRect.sizeDelta = Vector2.zero;

            // Text
            GameObject text = new GameObject("Text");
            text.transform.SetParent(textArea.transform, false);
            TextMeshProUGUI tText = text.AddComponent<TextMeshProUGUI>();
            tText.text = "";
            tText.color = new Color(0.1f, 0.1f, 0.1f, 1f);
            tText.fontSize = 24;
            tText.alignment = TextAlignmentOptions.Left;
            tText.enableWordWrapping = false;
            RectTransform tRect = text.GetComponent<RectTransform>();
            tRect.anchorMin = Vector2.zero;
            tRect.anchorMax = Vector2.one;
            tRect.sizeDelta = Vector2.zero;

            tmpInput.textViewport = textAreaRect;
            tmpInput.textComponent = tText;
            tmpInput.placeholder = pText;

            return inputObj;
        }

        private static GameObject CreateToggle(string name, Transform parent, string labelText)
        {
            GameObject toggleObj = DefaultControls.CreateToggle(new DefaultControls.Resources());
            toggleObj.name = name;
            toggleObj.transform.SetParent(parent, false);
            
            Text textComp = toggleObj.GetComponentInChildren<Text>();
            if (textComp != null)
            {
                textComp.text = labelText;
                textComp.color = Color.white;
            }

            return toggleObj;
        }

        private static GameObject CreateButton(string name, Transform parent, string labelText)
        {
            GameObject btnObj = DefaultControls.CreateButton(new DefaultControls.Resources());
            btnObj.name = name;
            btnObj.transform.SetParent(parent, false);
            RectTransform rect = btnObj.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(200, 60);

            Text textComp = btnObj.GetComponentInChildren<Text>();
            if (textComp != null)
            {
                textComp.text = labelText;
                textComp.fontSize = 24;
            }

            return btnObj;
        }
    }
}
