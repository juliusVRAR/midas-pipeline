// SettingPanel.cs
using UnityEngine;

public class SettingPanel : UIPanel
{
    [Header("Setting Specific References")]
    [SerializeField] private GameObject settingBackplate;
    
    public GameObject SettingBackplate => settingBackplate;

    public override void OnModeEnter()
    {
        base.OnModeEnter();
        Debug.Log("[SettingPanel] Setting mode activated");
    }

    public override void OnModeExit()
    {
        base.OnModeExit();
        Debug.Log("[SettingPanel] Setting mode deactivated");
    }
}