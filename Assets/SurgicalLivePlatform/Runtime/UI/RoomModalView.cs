using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using SurgicalLivePlatform.Runtime.DTOs;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SurgicalLivePlatform.Runtime.UI
{
    public enum RoomCreateMode
    {
        Immediate,
        Scheduled
    }

    public enum FormMode
    {
        Create,
        Edit
    }

    /// <summary>
    /// 手术房间创建/预约模态弹窗视图 (MVP Dumb View)
    /// 遵循 IUIView 生命周期协议与 BaseUIPanel 规范，实现 IRoomModalView。
    /// 彻底废除与下层页面的耦合 (无 lobbyViewToHide)，网络请求与异步提交流转全由 RoomModalPresenter 接管。
    /// </summary>
    public class RoomModalView : BaseUIPanel, IRoomModalView
    {
        [Header("Form Inputs")]
        [SerializeField] private TMP_InputField nameInput;
        [SerializeField] private TMP_Text nameErrorText;
        [SerializeField] private TMP_Text nameCharCountText;

        [SerializeField] private TMP_InputField descInput;
        [SerializeField] private TMP_Text descCharCountText;

        [SerializeField] private TMP_InputField coverUrlInput;

        [Header("Selectors")]
        [SerializeField] private TMP_Dropdown departmentDropdown;
        [SerializeField] private TMP_Dropdown durationDropdown;

        [Header("Time Layout (Hide for Immediate)")]
        [SerializeField] private GameObject timeRow;

        [Header("Controls")]
        [SerializeField] private Button submitButton;
        [SerializeField] private Button closeButton;

        public event Action<RoomFormData> OnSubmitRequested;
        public event Action OnCloseRequested;

        private RoomModalPresenter _presenter;
        private FormMode currentMode = FormMode.Create;
        private RoomCreateMode createMode = RoomCreateMode.Scheduled;
        private string editingRoomId;

        private void Awake()
        {
            InitializeView();
        }

        private void Start()
        {
            InitializeView();
        }

        #region Lifecycle Hooks (BaseUIPanel)

        protected override void OnInit()
        {
            if (submitButton) submitButton.onClick.AddListener(OnSubmit);
            if (closeButton) closeButton.onClick.AddListener(() => OnCloseRequested?.Invoke());
            if (nameInput) nameInput.onValueChanged.AddListener(OnNameChanged);
            if (descInput) descInput.onValueChanged.AddListener(OnDescChanged);

            InitializeDurationDropdown();

            // 自愈关联 Presenter
            if (_presenter == null)
            {
                _presenter = new RoomModalPresenter(this);
            }
        }

        protected override void OnShow()
        {
            if (timeRow != null)
            {
                timeRow.SetActive(createMode == RoomCreateMode.Scheduled);
            }

            if (submitButton != null)
            {
                var tmp = submitButton.GetComponentInChildren<TextMeshProUGUI>();
                if (tmp != null)
                {
                    tmp.text = createMode == RoomCreateMode.Immediate ? "立即开播" : "确认预约会议";
                }
            }

            _presenter?.LoadDepartmentsAsync();
        }

        protected override void OnHide()
        {
            ClearForm();
        }

        private void OnDestroy()
        {
            _presenter?.Dispose();
            _presenter = null;
        }

        public void AttachPresenter(RoomModalPresenter presenter)
        {
            _presenter?.Dispose();
            _presenter = presenter;
        }

        #endregion

        #region IRoomModalView Implementation

        public void SetDepartments(List<string> departments)
        {
            if (departmentDropdown == null) return;
            departmentDropdown.ClearOptions();
            if (departments != null)
            {
                departmentDropdown.AddOptions(departments);
            }
        }

        public void SetSubmitting(bool isSubmitting)
        {
            if (submitButton != null)
            {
                submitButton.interactable = !isSubmitting;
            }
        }

        public void ShowErrorMessage(string message)
        {
            if (nameErrorText != null)
            {
                nameErrorText.text = message;
            }
            Toast.Show(message, 2.5f);
        }

        public void ClearForm()
        {
            if (nameInput) nameInput.text = "";
            if (descInput) descInput.text = "";
            if (coverUrlInput) coverUrlInput.text = "";
            if (nameErrorText) nameErrorText.text = "";
            if (nameCharCountText) nameCharCountText.text = "0/50";
            if (descCharCountText) descCharCountText.text = "0/500";
        }

        #endregion

        #region Context Configuration

        /// <summary>
        /// 配置新建房间上下文模式
        /// </summary>
        public void Configure(RoomCreateMode mode)
        {
            currentMode = FormMode.Create;
            createMode = mode;
            editingRoomId = string.Empty;
            ClearForm();
        }

        /// <summary>
        /// 配置编辑已有房间上下文模式
        /// </summary>
        public void Configure(RoomItem room)
        {
            currentMode = FormMode.Edit;
            createMode = RoomCreateMode.Scheduled;
            editingRoomId = room != null ? room.id : string.Empty;
            ClearForm();

            if (room != null)
            {
                if (nameInput) nameInput.text = room.name;
                if (descInput) descInput.text = room.description;
                if (coverUrlInput) coverUrlInput.text = room.cover_image_url;
            }
        }

        #endregion

        private void InitializeDurationDropdown()
        {
            if (durationDropdown == null) return;
            durationDropdown.ClearOptions();
            durationDropdown.AddOptions(new List<string> { "30分钟", "60分钟", "90分钟", "120分钟" });
        }

        private void OnNameChanged(string text)
        {
            if (nameCharCountText) nameCharCountText.text = $"{text.Length}/50";
            ValidateName(text);
        }

        private void OnDescChanged(string text)
        {
            if (descCharCountText) descCharCountText.text = $"{text.Length}/500";
        }

        private bool ValidateName(string text)
        {
            if (string.IsNullOrEmpty(text) || text.Length < 2 || text.Length > 50)
            {
                if (nameErrorText) nameErrorText.text = "房间名长度需在2-50个字符之间";
                return false;
            }

            if (Regex.IsMatch(text, @"[!@#$%^&*()_+=\[{\]};:<>|./?,\\]"))
            {
                if (nameErrorText) nameErrorText.text = "房间名不能包含特殊字符";
                return false;
            }

            if (nameErrorText) nameErrorText.text = "";
            return true;
        }

        private int ParseDurationMinutes()
        {
            if (durationDropdown == null || durationDropdown.options == null || durationDropdown.options.Count == 0)
            {
                return 60;
            }

            string optText = durationDropdown.options[durationDropdown.value].text;
            if (optText.Contains("30")) return 30;
            if (optText.Contains("60")) return 60;
            if (optText.Contains("90")) return 90;
            if (optText.Contains("120")) return 120;
            return 60;
        }

        private void OnSubmit()
        {
            string nameVal = nameInput ? nameInput.text : "Unnamed";
            if (!ValidateName(nameVal)) return;

            string selectedDept = departmentDropdown != null && departmentDropdown.options.Count > 0 
                ? departmentDropdown.options[departmentDropdown.value].text 
                : "";
            string descVal = descInput ? descInput.text : "";
            string coverUrlVal = coverUrlInput ? coverUrlInput.text : "";

            var formData = new RoomFormData
            {
                mode = currentMode,
                createMode = createMode,
                roomId = editingRoomId,
                name = nameVal,
                description = descVal,
                coverUrl = coverUrlVal,
                department = selectedDept,
                durationMinutes = ParseDurationMinutes()
            };

            OnSubmitRequested?.Invoke(formData);
        }
    }
}
