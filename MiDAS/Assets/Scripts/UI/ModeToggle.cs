using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Connects a Unity Toggle to a UIMode.
/// Add this alongside the Toggle component.
/// </summary>
[RequireComponent(typeof(Toggle))]
public class ModeToggle : MonoBehaviour
{
    [Header("Configuration")]
    [SerializeField] private UIMode targetMode;
    
    [Header("UI Elements")]
    [SerializeField] private Image iconImage;
    [SerializeField] private TextMeshProUGUI labelText;
    [SerializeField] private Image backgroundImage;
    
    [Header("Visual States")]
    [SerializeField] private Color selectedColor = new Color(0.2f, 0.6f, 1f, 1f);
    [SerializeField] private Color normalColor = new Color(0.2f, 0.2f, 0.2f, 0.8f);

    private Toggle _toggle;

    private void Awake()
    {
        _toggle = GetComponent<Toggle>();
    }
    
    private void Start()
    {
        // Setup visuals from ScriptableObject
        if (targetMode != null)
        {
            if (iconImage != null && targetMode.icon != null)
                iconImage.sprite = targetMode.icon;
            
            if (labelText != null)
                labelText.text = targetMode.modeName;
        }
        
        // Subscribe to mode changes
        _toggle.onValueChanged.AddListener(OnToggleChanged);

        if (UIManager.Instance != null)
            UIManager.Instance.OnModeChanged.AddListener(OnExternalModeChanged);
        
        UpdateVisuals(false);
    }
    
    private void OnDestroy()
    {
        _toggle.onValueChanged.RemoveListener(OnToggleChanged);

        if (UIManager.Instance != null)
            UIManager.Instance.OnModeChanged.RemoveListener(OnExternalModeChanged);
    }
    
    private void OnToggleChanged(bool isOn)
    {
        if (isOn)
        {
            UIManager.Instance?.SetMode(targetMode);
        }
    }

    private void OnExternalModeChanged(UIMode newMode)
    {
        _toggle.SetIsOnWithoutNotify(newMode == targetMode);
        UpdateVisuals(newMode == targetMode);
    }
    
    private void UpdateVisuals(bool isSelected)
    {
        if (backgroundImage != null)
            backgroundImage.color = isSelected ? selectedColor : normalColor;
    }
}