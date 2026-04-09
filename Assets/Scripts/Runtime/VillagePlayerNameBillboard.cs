using TMPro;
using UnityEngine;
using Mimic.Gameplay;

[AddComponentMenu("Mimic/Player Nickname Billboard")]
[DisallowMultipleComponent]
public sealed class VillagePlayerNameBillboard : MonoBehaviour
{
    [Header("Display")]
    [SerializeField] private Vector3 nameOffset = new Vector3(0f, 0.9f, 0f);
    [SerializeField] private float labelScale = 0.05f;
    [SerializeField] private float labelFontSize = 30f;
    [SerializeField] private Color labelColor = new Color(0.8f, 0.8f, 0.8f, 1f);
    [SerializeField] private TMP_FontAsset fallbackFontAsset;

    private TextMeshPro _label;
    private Camera _cachedCamera;

    private void Awake()
    {
        EnsureLabel();
    }

    private void OnEnable()
    {
        EnsureLabel();
        RefreshLabelText();
    }

    private void LateUpdate()
    {
        EnsureLabel();
        if (_label == null)
        {
            return;
        }

        FaceCamera();
        RefreshLabelText();
    }

    private void OnDisable()
    {
        if (_label != null)
        {
            _label.enabled = false;
        }
    }

    private void OnValidate()
    {
        ApplyVisualSettings();
    }

    private void EnsureLabel()
    {
        if (_label != null)
        {
            return;
        }

        var existingLabel = transform.Find("PlayerNicknameBillboard");
        if (existingLabel != null)
        {
            _label = existingLabel.GetComponent<TextMeshPro>();
        }

        if (_label == null)
        {
            var textObject = new GameObject("PlayerNicknameBillboard")
            {
                hideFlags = HideFlags.DontSave,
            };

            textObject.transform.SetParent(transform, false);
            textObject.transform.localPosition = nameOffset;
            textObject.transform.localRotation = Quaternion.identity;
            textObject.transform.localScale = Vector3.one;

            _label = textObject.AddComponent<TextMeshPro>();
        }

        ApplyVisualSettings();
        RefreshLabelText();
    }

    private void ApplyVisualSettings()
    {
        if (_label == null)
        {
            return;
        }

        _label.alignment = TextAlignmentOptions.Center;
        _label.color = labelColor;
        _label.enableAutoSizing = false;
        _label.fontSize = labelFontSize;
        _label.raycastTarget = false;
        _label.enableWordWrapping = false;
        _label.overflowMode = TextOverflowModes.Overflow;
        var sourceFont = fallbackFontAsset;
        if (sourceFont == null)
        {
            sourceFont = TMP_Settings.defaultFontAsset;
        }

        if (sourceFont != null)
        {
            _label.font = sourceFont;
        }
        _label.transform.localScale = Vector3.one * labelScale;
        _label.transform.localPosition = nameOffset;
    }

    private void RefreshLabelText()
    {
        if (_label == null)
        {
            return;
        }

        var player = GetComponentInParent<VillagePlayerSync>();
        _label.text = player != null ? player.DisplayName : gameObject.name;
    }

    private void FaceCamera()
    {
        if (_label == null)
        {
            return;
        }

        if (_cachedCamera == null)
        {
            _cachedCamera = Camera.main;
        }

        if (_cachedCamera == null)
        {
            return;
        }

        var toCamera = _label.transform.position - _cachedCamera.transform.position;
        if (toCamera.sqrMagnitude <= Mathf.Epsilon)
        {
            return;
        }

        _label.transform.rotation = Quaternion.LookRotation(toCamera.normalized, Vector3.up);
    }
}
