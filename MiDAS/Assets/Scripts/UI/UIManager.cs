using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    [Header("Reference")]
    [SerializeField] private Transform leftHandAnchor;
    [SerializeField] private HandAttachmentManager attachmentManager;

    [Header("Positioning")]
    [SerializeField] private Vector3 offsetFromHand = new Vector3(0.1f, 0.1f, 0.1f);
    [SerializeField] private float tilt = 45f;
    
    [Header("Settings")]
    [SerializeField] private UIMode defaultMode;
    [SerializeField] private bool hideOnStart = false;
    
    [Header("Events")]
    public UnityEvent<UIMode> OnModeChanged;
    public UnityEvent<IUIPanel> OnPanelActivated;
    
    private Dictionary<UIMode, IUIPanel> _panels = new Dictionary<UIMode, IUIPanel>();
    private IUIPanel _activePanel;
    private bool _isVisible = true;
    
    public UIMode CurrentMode => _activePanel?.Mode;
    public bool IsVisible => _isVisible;
    
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (leftHandAnchor != null)
        {
            transform.SetParent(leftHandAnchor);
            transform.SetLocalPositionAndRotation(offsetFromHand, Quaternion.Euler(tilt, 0, 0));
        }
    }
    
    private void Start()
    {
        if (hideOnStart)
            HideAll();
        else if (defaultMode != null)
            SetMode(defaultMode);
    }
    
    public void RegisterPanel(IUIPanel panel)
    {
        if (panel?.Mode == null) return;
        
        _panels[panel.Mode] = panel;
        panel.Hide(); // Start hidden
        
        Debug.Log($"[UIManager] Registered panel: {panel.Mode.modeName}");
    }
    
    public void UnregisterPanel(IUIPanel panel)
    {
        if (panel?.Mode == null) return;
        _panels.Remove(panel.Mode);
    }
    
    public void SetMode(UIMode mode)
    {
        if (mode == null || !_panels.TryGetValue(mode, out var panel))
        {
            Debug.LogWarning($"[UIManager] Mode not found or no panel registered: {mode?.modeName}");
            return;
        }
        
        // Hide current panel
        _activePanel?.Hide();
        
        // Show new panel
        _activePanel = panel;
        _activePanel.Show();
        _isVisible = true;
        
        OnModeChanged?.Invoke(mode);
        OnPanelActivated?.Invoke(panel);
        
        Debug.Log($"[UIManager] Mode changed to: {mode.modeName}");
    }
    
    public void ToggleVisibility()
    {
        _isVisible = !_isVisible;
        gameObject.SetActive(_isVisible);
        attachmentManager.ToggleAttachedObjectsVisibility(_isVisible);
    }
    
    public void HideAll()
    {
        _activePanel?.Hide();
        _isVisible = false;
    }
    
    public T GetPanel<T>() where T : class, IUIPanel
    {
        foreach (var panel in _panels.Values)
        {
            if (panel is T typedPanel)
                return typedPanel;
        }
        return null;
    }
    
    /// <summary>
    /// Toggle viewer for current mode (delegates to active panel)
    /// </summary>
    public void ToggleCurrentViewer()
    {
        switch (_activePanel)
        {
            case PosePanel posePanel:
                posePanel.TogglePoseViewer();
                break;
            // Add cases for other panel types as needed
            default:
                Debug.LogWarning("[UIManager] No viewer toggle implemented for current panel");
                break;
        }
    }
}