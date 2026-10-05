// UIPanel.cs
using UnityEngine;
using UnityEngine.Events;

public class UIPanel : MonoBehaviour, IUIPanel
{
    [Header("Panel Configuration")]
    [SerializeField] private UIMode mode;
    [SerializeField] private GameObject[] managedObjects;
    
    [Header("Events")]
    public UnityEvent OnPanelShown;
    public UnityEvent OnPanelHidden;
    
    public UIMode Mode => mode;
    public bool IsActive { get; private set; }

    protected virtual void Start()
    {
        // Auto-register with manager
        UIManager.Instance?.RegisterPanel(this);
    }

    protected virtual void OnDestroy()
    {
        UIManager.Instance?.UnregisterPanel(this);
    }

    public virtual void Show()
    {
        IsActive = true;
        SetManagedObjectsActive(true);
        OnModeEnter();
        OnPanelShown?.Invoke();
    }

    public virtual void Hide()
    {
        OnModeExit();
        SetManagedObjectsActive(false);
        IsActive = false;
        OnPanelHidden?.Invoke();
    }

    public virtual void OnModeEnter() { }
    public virtual void OnModeExit() { }

    protected void SetManagedObjectsActive(bool active)
    {
        if (managedObjects == null) return;
        
        foreach (var obj in managedObjects)
        {
            if (obj != null)
                obj.SetActive(active);
        }
    }
}