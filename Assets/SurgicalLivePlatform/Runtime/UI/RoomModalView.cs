using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Text.RegularExpressions;
using SurgicalLivePlatform.Runtime;
using SurgicalLivePlatform.Runtime.DTOs;
using System.Collections.Generic;
using System.Linq;

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

    public class RoomModalView : MonoBehaviour
    {
        [Header("Form Inputs")]
        [SerializeField] private TMP_InputField nameInput;
        [SerializeField] private TMP_Text nameErrorText;
        [SerializeField] private TMP_Text nameCharCountText;

        [SerializeField] private TMP_InputField descInput;
        [SerializeField] private TMP_Text descCharCountText;

        [SerializeField] private TMP_InputField coverUrlInput; // DateInput in mock
        
        [Header("Selectors")]
        [SerializeField] private TMP_Dropdown departmentDropdown;
        [SerializeField] private TMP_Dropdown durationDropdown;
        
        [Header("Time Layout (Hide for Immediate)")]
        [SerializeField] private GameObject timeRow;

        [Header("Controls")]
        [SerializeField] private Button submitButton;
        [SerializeField] private Button closeButton;
        [SerializeField] private GameObject lobbyViewToHide;

        private FormMode currentMode = FormMode.Create;
        private RoomCreateMode createMode = RoomCreateMode.Scheduled;
        private string editingRoomId;

        private void Awake()
        {
            if(submitButton) submitButton.onClick.AddListener(OnSubmit);
            if(closeButton) closeButton.onClick.AddListener(CloseModal);
            if(nameInput) nameInput.onValueChanged.AddListener(OnNameChanged);
            if(descInput) descInput.onValueChanged.AddListener(OnDescChanged);
        }

        public async void Open(RoomCreateMode mode)
        {
            currentMode = FormMode.Create;
            createMode = mode;
            editingRoomId = string.Empty;
            ClearForm();
            
            if (timeRow != null)
            {
                timeRow.SetActive(mode == RoomCreateMode.Scheduled);
            }
            
            if (submitButton != null)
            {
                var tmp = submitButton.GetComponentInChildren<TextMeshProUGUI>();
                if (tmp != null) tmp.text = mode == RoomCreateMode.Immediate ? "立即开播" : "确认预约会议";
            }

            InitializeDurationDropdown();
            await InitializeDepartmentDropdown();
            
            if (lobbyViewToHide) lobbyViewToHide.SetActive(false);
            gameObject.SetActive(true);
        }

        public async void OpenForEdit(RoomItem room)
        {
            currentMode = FormMode.Edit;
            createMode = RoomCreateMode.Scheduled; // Editing defaults to full form
            editingRoomId = room.id;
            ClearForm();
            
            if (timeRow != null) timeRow.SetActive(true);
            
            InitializeDurationDropdown();
            await InitializeDepartmentDropdown();
            
            if (nameInput) nameInput.text = room.name;
            if (descInput) descInput.text = room.description;
            if (coverUrlInput) coverUrlInput.text = room.cover_image_url;
            
            if (lobbyViewToHide) lobbyViewToHide.SetActive(false);
            gameObject.SetActive(true);
        }

        private void InitializeDurationDropdown()
        {
            if (durationDropdown == null) return;
            durationDropdown.ClearOptions();
            durationDropdown.AddOptions(new List<string> { "30分钟", "60分钟", "90分钟", "120分钟" });
        }

        private async System.Threading.Tasks.Task InitializeDepartmentDropdown()
        {
            if (departmentDropdown == null) return;
            departmentDropdown.ClearOptions();
            var depts = await DepartmentConfigService.GetDepartmentNamesAsync();
            departmentDropdown.AddOptions(depts);
        }

        private void ClearForm()
        {
            if (nameInput) nameInput.text = "";
            if (descInput) descInput.text = "";
            if (coverUrlInput) coverUrlInput.text = "";
            if (nameErrorText) nameErrorText.text = "";
            if (nameCharCountText) nameCharCountText.text = "0/50";
            if (descCharCountText) descCharCountText.text = "0/500";
        }

        private void OnNameChanged(string text)
        {
            if(nameCharCountText) nameCharCountText.text = $"{text.Length}/50";
            ValidateName(text);
        }

        private void OnDescChanged(string text)
        {
            if(descCharCountText) descCharCountText.text = $"{text.Length}/500";
        }

        private bool ValidateName(string text)
        {
            if (string.IsNullOrEmpty(text) || text.Length < 2 || text.Length > 50)
            {
                if(nameErrorText) nameErrorText.text = "房间名长度需在2-50个字符之间";
                return false;
            }

            if (Regex.IsMatch(text, @"[!@#$%^&*()_+=\[{\]};:<>|./?,\\]"))
            {
                if(nameErrorText) nameErrorText.text = "房间名不能包含特殊字符";
                return false;
            }

            if(nameErrorText) nameErrorText.text = "";
            return true;
        }

        private async void OnSubmit()
        {
            string nameVal = nameInput ? nameInput.text : "Unnamed";
            if (!ValidateName(nameVal)) return;

            if(submitButton) submitButton.interactable = false;

            string selectedDept = departmentDropdown != null && departmentDropdown.options.Count > 0 ? departmentDropdown.options[departmentDropdown.value].text : "";
            string descVal = descInput ? descInput.text : "";
            string coverUrlVal = coverUrlInput ? coverUrlInput.text : "";

            try
            {
                if (currentMode == FormMode.Edit)
                {
                    var req = new UpdateRoomRequest { name = nameVal, description = descVal, cover_image_url = coverUrlVal };
                    await SurgicalLiveSDK.Auth.UpdateRoomAsync(editingRoomId, req);
                    Debug.Log($"[RoomModalView] UpdateRoomAsync successful. ID: {editingRoomId}");
                }
                else
                {
                    var req = new CreateRoomRequest
                    {
                        name = nameVal,
                        description = descVal,
                        cover_image_url = coverUrlVal,
                        department_id = selectedDept,
                        members = new List<string>() 
                    };
                    var roomResponse = await SurgicalLiveSDK.Auth.CreateRoomAsync(req);
                    Debug.Log($"[RoomModalView] CreateRoomAsync successful ({createMode}). New Room ID: {roomResponse.id}");
                }
                
                CloseModal();
            }
            catch (System.Exception e)
            {
                if(nameErrorText) nameErrorText.text = "服务器错误: " + e.Message;
                Debug.LogError($"[RoomModalView] Submission error: {e.Message}");
            }
            finally
            {
                if(submitButton) submitButton.interactable = true;
            }
        }

        private void CloseModal()
        {
            gameObject.SetActive(false);
            if (lobbyViewToHide) lobbyViewToHide.SetActive(true);
        }
    }
}

