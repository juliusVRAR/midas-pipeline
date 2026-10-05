// PosePanel.cs
using Oculus.Interaction.Gizmo;
using UnityEngine;
using UnityEngine.UI;

public class PosePanel : UIPanel
{
    [Header("Pose Specific References")]
    [SerializeField] private GameObject poseViewer;
    [SerializeField] private GizmoModeCoordinator modeCoordinator;
    [SerializeField] private Toggle freeModeToggle;
    
    public GameObject PoseViewer => poseViewer;

    public override void OnModeEnter()
    {
        base.OnModeEnter();
        Debug.Log("[PosePanel] Pose mode activated");
    }

    public override void OnModeExit()
    {
        modeCoordinator.SetGlobalMode(GizmoMode.Free); // Ensure we exit pose mode in the coordinator
        freeModeToggle.isOn = true; // Reset the toggle to reflect the mode change
        Debug.Log("[PosePanel] Pose mode deactivated");
    }

    /// <summary>
    /// Toggle the pose viewer visibility
    /// </summary>
    public void TogglePoseViewer()
    {
        if (poseViewer != null)
        {
            poseViewer.SetActive(!poseViewer.activeSelf);
        }
    }
}