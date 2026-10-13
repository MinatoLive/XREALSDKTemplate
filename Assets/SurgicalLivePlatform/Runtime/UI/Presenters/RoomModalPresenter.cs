using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using SurgicalLivePlatform.Runtime.Auth;
using SurgicalLivePlatform.Runtime.DTOs;
using UnityEngine;

namespace SurgicalLivePlatform.Runtime.UI
{
    /// <summary>
    /// 房间创建/编辑模态弹窗业务中介 (Room Modal Presenter)
    /// 纯 C# 实现，负责表单校验、部门拉取与异步创建/更新提交。
    /// </summary>
    public class RoomModalPresenter : IDisposable
    {
        private readonly IRoomModalView _view;
        private readonly IAuthService _authService;
        private readonly UILayerManager _layerManager;

        public event Action OnRoomOperationSuccess;

        public RoomModalPresenter(
            IRoomModalView view,
            IAuthService authService = null,
            UILayerManager layerManager = null)
        {
            _view = view ?? throw new ArgumentNullException(nameof(view));
            _authService = authService ?? SurgicalLiveSDK.Auth;
            _layerManager = layerManager ?? UILayerManager.Instance;

            _view.OnSubmitRequested += HandleSubmitRequested;
            _view.OnCloseRequested += HandleCloseRequested;
        }

        public void Dispose()
        {
            if (_view != null)
            {
                _view.OnSubmitRequested -= HandleSubmitRequested;
                _view.OnCloseRequested -= HandleCloseRequested;
            }
        }

        public async Task LoadDepartmentsAsync()
        {
            try
            {
                var depts = await DepartmentConfigService.GetDepartmentNamesAsync();
                _view.SetDepartments(depts);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[RoomModalPresenter] 加载科室列表异常: {ex.Message}");
            }
        }

        private async void HandleSubmitRequested(RoomFormData formData)
        {
            await SubmitFormAsync(formData);
        }

        public async Task<bool> SubmitFormAsync(RoomFormData formData)
        {
            if (formData == null) return false;

            // 1. 表单校验
            string name = formData.name?.Trim();
            if (string.IsNullOrEmpty(name))
            {
                _view.ShowErrorMessage("房间名称不能为空");
                return false;
            }

            if (name.Length < 2 || name.Length > 50)
            {
                _view.ShowErrorMessage("房间名称长度需在 2 到 50 个字符之间");
                return false;
            }

            if (!Regex.IsMatch(name, @"^[\u4e00-\u9fa5_a-zA-Z0-9\s\-]+$"))
            {
                _view.ShowErrorMessage("房间名称包含非法特殊字符");
                return false;
            }

            _view.SetSubmitting(true);

            try
            {
                var auth = _authService ?? SurgicalLiveSDK.Auth;
                if (auth == null)
                {
                    _view.ShowErrorMessage("系统认证服务未就绪");
                    return false;
                }

                if (formData.mode == FormMode.Edit)
                {
                    var updateReq = new UpdateRoomRequest
                    {
                        name = name,
                        description = formData.description?.Trim(),
                        cover_image_url = formData.coverUrl?.Trim()
                    };
                    await auth.UpdateRoomAsync(formData.roomId, updateReq);
                    Toast.Show("房间修改成功", 2.0f);
                }
                else
                {
                    var createReq = new CreateRoomRequest
                    {
                        name = name,
                        description = formData.description?.Trim(),
                        cover_image_url = formData.coverUrl?.Trim(),
                        department_id = formData.department,
                        members = new List<string>()
                    };
                    await auth.CreateRoomAsync(createReq);
                    Toast.Show(formData.createMode == RoomCreateMode.Immediate ? "立即开播创建成功" : "预约会议创建成功", 2.0f);
                }

                CloseModal();
                OnRoomOperationSuccess?.Invoke();
                return true;
            }
            catch (Exception ex)
            {
                _view.ShowErrorMessage($"提交失败: {ex.Message}");
                return false;
            }
            finally
            {
                _view.SetSubmitting(false);
            }
        }

        private void HandleCloseRequested()
        {
            CloseModal();
        }

        private void CloseModal()
        {
            var lm = _layerManager ?? UILayerManager.Instance;
            if (lm != null)
            {
                lm.CloseModal(_view);
            }
            else
            {
                _view.Hide();
            }
        }
    }
}
