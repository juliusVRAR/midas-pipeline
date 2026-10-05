using UnityEngine;

public class RecordingIndicator : MonoBehaviour
{
    [Header("Head Anchor")]
    [SerializeField] private Transform headTransform;

    [Header("Objects")]
    [SerializeField] private GameObject recordCircle;
    [SerializeField] private GameObject snapshotFlashCircle;

    [Header("Placement")]
    [SerializeField] private float distance = 0.8f;
    [Header("Behavior")]
    [SerializeField] private bool followHead = true;

    private bool recordingVisible;

    private void Awake()
    {
        SetRecordingVisible(false);
        ShowSnapshotFlash(false);
    }

    private void LateUpdate()
    {
        if (!followHead || headTransform == null)
            return;

        Vector3 forward = headTransform.forward;
        Vector3 right = headTransform.right;
        Vector3 up = headTransform.up;

        Vector3 targetPos =
            headTransform.position +
            forward * distance;

        transform.position = targetPos;

        Vector3 lookDir = transform.position - headTransform.position;
        if (lookDir.sqrMagnitude > 0.0001f)
        {
            transform.rotation = Quaternion.LookRotation(lookDir.normalized, Vector3.up);
        }
    }

    public void SetRecordingVisible(bool visible)
    {
        recordingVisible = visible;

        if (recordCircle != null)
            recordCircle.SetActive(visible);
    }

    public void ShowSnapshotFlash(bool visible)
    {
        if (snapshotFlashCircle != null)
            snapshotFlashCircle.SetActive(visible);
    }
}