using System;
using System.Collections.Generic;

namespace SurgicalLivePlatform.Runtime.DTOs
{
    #region 请求 DTO (Request DTOs)

    /// <summary>
    /// 注册硬件设备请求
    /// </summary>
    [Serializable]
    public class RegisterDeviceRequest
    {
        public string device_sn;
        public string device_type;
        public string device_name;
    }

    /// <summary>
    /// 更新设备硬件能力请求
    /// </summary>
    [Serializable]
    public class UpdateDeviceCapabilitiesRequest
    {
        public string video_codecs;
        public string max_resolution;
        public bool has_microphone;
    }

    #endregion

    #region 响应与模型 DTO (Response DTOs)

    [Serializable]
    public class DeviceInfo
    {
        public string id;
        public string device_sn;
        public string device_type;
        public string device_name;
        public string status;
    }

    [Serializable]
    public class DeviceList
    {
        public List<DeviceInfo> list;
        public int total;
    }

    [Serializable]
    public class RegisterDeviceResponse
    {
        public string device_secret;
    }

    #endregion

    #region JSON 包装类 (Wrappers)

    [Serializable]
    public class DeviceListWrapper
    {
        public int code;
        public string message;
        public DeviceList data;
    }

    [Serializable]
    public class DeviceInfoWrapper
    {
        public int code;
        public string message;
        public DeviceInfo data;
    }

    [Serializable]
    public class RegisterDeviceResponseWrapper
    {
        public int code;
        public string message;
        public RegisterDeviceResponse data;
    }

    #endregion
}
