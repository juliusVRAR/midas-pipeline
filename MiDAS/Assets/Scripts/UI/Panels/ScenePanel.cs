// PosePanel.cs
using UnityEngine;

public class ScenePanel : UIPanel
{
    [Header("Scene Specific References")]
    [SerializeField] private GameObject sceneViewer;
    public override void OnModeEnter()
    {
        base.OnModeEnter();
        Debug.Log("[ScenePanel] Scene mode activated");
    }

    public override void OnModeExit()
    {
        base.OnModeExit();
        Debug.Log("[ScenePanel] Scene mode deactivated");
    }

}