// SettingPanel.cs
using UnityEngine;

public class DebugPanel : UIPanel
{
    [Header("Debug Specific References")]
    [SerializeField] private GameObject debugBackplate;
    
    public GameObject DebugBackplate => debugBackplate;

    public override void OnModeEnter()
    {
        base.OnModeEnter();
        Debug.Log("[DebugPanel] Debug mode activated");
    }

    public override void OnModeExit()
    {
        base.OnModeExit();
        Debug.Log("[DebugPanel] Debug mode deactivated");
    }
}