using UnityEngine;

public class CaptureButtonHandler : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField] private PoseObjectCoordinator poseCoordinator;
    [SerializeField] private CaptureFeedback feedback;

    [Header("Input")]
    [SerializeField] private OVRInput.Button snapshotButton = OVRInput.Button.One;
    [SerializeField] private OVRInput.Button recordButton = OVRInput.Button.Two;
    [SerializeField] private OVRInput.Controller controller = OVRInput.Controller.RTouch;

    [Header("Connection Settings")]
    [SerializeField] private float connectionTimeout = 10f;

    private CaptureState currentState = CaptureState.Idle;

    public enum CaptureState
    {
        Idle,       // Not recording, ready for snapshot or to start recording
        Connecting, // Attempting to establish WebSocket connection
        Recording   // Actively streaming frames
    }

    public CaptureState CurrentState => currentState;

    void Update()
    {
        if(UIManager.Instance?.CurrentMode.modeName != "Pose")
            return;
            
        if(OVRInput.GetUp(OVRInput.Button.Three))
        {
            poseCoordinator.ToggleAllObjectVisuals();
        }

        HandleSnapshotButton();
        HandleRecordButton();
    }

    /// <summary>
    /// Button One: Send single pose snapshot (only works when Idle)
    /// </summary>
    private void HandleSnapshotButton()
    {
        if (OVRInput.GetDown(snapshotButton, controller))
        {
            switch (currentState)
            {
                case CaptureState.Idle:
                    TakeSnapshot();
                    break;

                case CaptureState.Connecting:
                case CaptureState.Recording:
                    // Ignore snapshot button while connecting or recording
                    Debug.Log("[Capture] Snapshot ignored - currently recording or connecting");
                    break;
            }
        }
    }

    /// <summary>
    /// Button Two: Toggle recording on/off
    /// </summary>
    private void HandleRecordButton()
    {
        if (OVRInput.GetDown(recordButton, controller))
        {
            switch (currentState)
            {
                case CaptureState.Idle:
                    StartRecording();
                    break;

                case CaptureState.Connecting:
                    // Ignore - already trying to connect
                    Debug.Log("[Capture] Already connecting...");
                    break;

                case CaptureState.Recording:
                    StopRecording();
                    break;
            }
        }
    }

    private void TakeSnapshot()
    {
        Debug.Log("[Capture] Taking snapshot");
        poseCoordinator.SendPoses();
        feedback.PlaySnapshotFeedback();
    }

    private async void StartRecording()
    {
        Debug.Log("[Capture] Starting recording...");
        currentState = CaptureState.Connecting;

        bool success = await poseCoordinator.StartStreamPoses(connectionTimeout);

        if (success)
        {
            currentState = CaptureState.Recording;
            feedback.PlayRecordStartFeedback();
            Debug.Log("[Capture] Recording started");
        }
        else
        {
            currentState = CaptureState.Idle;
            // feedback.PlayErrorFeedback();
            Debug.LogError("[Capture] Failed to start recording - connection failed");
        }
    }

    private async void StopRecording()
    {
        Debug.Log("[Capture] Stopping recording");
        currentState = CaptureState.Connecting;

        await poseCoordinator.StopStreamPosesAsync();

        feedback.PlayRecordStopFeedback();
        currentState = CaptureState.Idle;
    }
}