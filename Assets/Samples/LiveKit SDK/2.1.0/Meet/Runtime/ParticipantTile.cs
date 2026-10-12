using Google.MaterialDesign.Icons;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ParticipantTile : MonoBehaviour
{
    public RawImage Image;
    public TMP_Text Label;
    public MaterialIcon MicIcon;
    public RectTransform SpeakingBorder;

    private ResizeTextureController _controller;
    private Texture _placeholder;
    private bool _showingLive;

    private bool _speaking;

    private void Awake()
    {
        SetMicMuted(true);
    }

    public void SetPlaceholder(Texture tex)
    {
        _placeholder = tex;
        if (!_showingLive) Image.texture = tex;
    }

    public void BindLiveSource(Texture source, int rotationDegrees = 0, bool mirror = false)
    {
        _controller?.Dispose();
        
        int normalizedRotation = ((rotationDegrees % 360) + 360) % 360;
        bool swap = normalizedRotation == 90 || normalizedRotation == 270;
        float targetWidth = swap ? source.height : source.width;
        float targetHeight = swap ? source.width : source.height;

        _controller = new ResizeTextureController(
            source, targetWidth, targetHeight,
            ResizeTextureController.CropMode.FillCrop, // Note: Since target aspect matches source, no cropping will actually occur.
            rotationDegrees, mirror);
        _showingLive = true;
        Image.texture = _controller.GetTargetTexture();

        // 自动挂载并设置 AspectRatioFitter，保持原画比例，防止拉伸 (已根据要求关闭)
        /*
        AspectRatioFitter fitter = Image.gameObject.GetComponent<AspectRatioFitter>();
        if (fitter == null)
        {
            fitter = Image.gameObject.AddComponent<AspectRatioFitter>();
        }
        fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
        if (targetHeight > 0)
        {
            fitter.aspectRatio = targetWidth / targetHeight;
        }
        */
    }

    public void SetLiveRotation(int rotationDegrees, bool mirror)
    {
        _controller?.SetRotation(rotationDegrees, mirror);
    }

    public void ShowLive()
    {
        if (_controller == null) return;
        _showingLive = true;
        Image.texture = _controller.GetTargetTexture();
    }

    public void ShowPlaceholder()
    {
        _showingLive = false;
        Image.texture = _placeholder;
    }

    public void ClearLive()
    {
        _controller?.Dispose();
        _controller = null;
        _showingLive = false;
        Image.texture = _placeholder;
    }

    public void SetMicMuted(bool muted)
    {
        if (MicIcon == null) return;
        MicIcon.gameObject.SetActive(muted);
    }

    /// <summary>
    /// Highlights the tile with a colored border while the participant is speaking.
    /// </summary>
    public void SetSpeaking(bool speaking)
    {
        if (_speaking == speaking) return;
        _speaking = speaking;

        SpeakingBorder.gameObject.SetActive(speaking);
    }

    private void Update() => _controller?.Resize();

    private void OnDestroy()
    {
        _controller?.Dispose();
        _controller = null;
    }
}
