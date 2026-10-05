// SamPanel.cs
using UnityEngine;

public class SamPanel : UIPanel
{
    [Header("Sam Specific References")]
    [SerializeField] private GameObject samViewer;
    [SerializeField] private GameObject pointPromt3D;
    

    public override void OnModeEnter()
    {
        pointPromt3D.SetActive(true);
        Debug.Log("[SamPanel] Sam mode activated");
    }

    public override void OnModeExit()
    {
        pointPromt3D.SetActive(false);
        samViewer.SetActive(false);
        Debug.Log("[SamPanel] Sam mode deactivated");
    }
}