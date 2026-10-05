using System.Collections;
using UnityEngine;

public class CaptureFeedback : MonoBehaviour
{
    [Header("Controller")]
    [SerializeField] private OVRInput.Controller controller = OVRInput.Controller.Touch;

    [Header("Recording Visual")]
    [SerializeField] private RecordingIndicator recordingIndicator;

    [Header("Snapshot Haptics")]
    [SerializeField, Range(0f, 1f)] private float snapshotFrequency = 1.0f;
    [SerializeField, Range(0f, 1f)] private float snapshotAmplitude = 0.45f;
    [SerializeField] private float snapshotDuration = 0.06f;

    [Header("Record Start Haptics")]
    [SerializeField, Range(0f, 1f)] private float recordStartFrequency = 1.0f;
    [SerializeField, Range(0f, 1f)] private float recordStartAmplitude = 0.8f;
    [SerializeField] private float recordStartPulseDuration = 0.08f;
    [SerializeField] private float recordStartGapDuration = 0.04f;
    [SerializeField] private float recordStartSecondPulseDuration = 0.12f;

    [Header("Record Stop Haptics")]
    [SerializeField, Range(0f, 1f)] private float recordStopFrequency = 0.6f;
    [SerializeField, Range(0f, 1f)] private float recordStopAmplitude = 0.35f;
    [SerializeField] private float recordStopDuration = 0.08f;

    [Header("Snapshot Flash")]
    [SerializeField] private float snapshotFlashDuration = 0.18f;

    private Coroutine hapticsRoutine;
    private Coroutine snapshotFlashRoutine;

    public void PlaySnapshotFeedback()
    {
        StartHapticsRoutine(SnapshotPulseRoutine());

        if (recordingIndicator != null)
        {
            if (snapshotFlashRoutine != null)
                StopCoroutine(snapshotFlashRoutine);

            snapshotFlashRoutine = StartCoroutine(SnapshotFlashRoutine());
        }
    }

    public void PlayRecordStartFeedback()
    {
        StartHapticsRoutine(RecordStartRoutine());

        if (recordingIndicator != null)
            recordingIndicator.SetRecordingVisible(true);
    }

    public void PlayRecordStopFeedback()
    {
        StartHapticsRoutine(RecordStopRoutine());

        if (recordingIndicator != null)
            recordingIndicator.SetRecordingVisible(false);
    }

    public void SetRecordingVisual(bool visible)
    {
        if (recordingIndicator != null)
            recordingIndicator.SetRecordingVisible(visible);
    }

    private void StartHapticsRoutine(IEnumerator routine)
    {
        if (hapticsRoutine != null)
            StopCoroutine(hapticsRoutine);

        StopHaptics();
        hapticsRoutine = StartCoroutine(routine);
    }

    private IEnumerator SnapshotPulseRoutine()
    {
        SetHaptics(snapshotFrequency, snapshotAmplitude);
        yield return new WaitForSecondsRealtime(snapshotDuration);
        StopHaptics();
        hapticsRoutine = null;
    }

    private IEnumerator RecordStartRoutine()
    {
        SetHaptics(recordStartFrequency, recordStartAmplitude);
        yield return new WaitForSecondsRealtime(recordStartPulseDuration);

        StopHaptics();
        yield return new WaitForSecondsRealtime(recordStartGapDuration);

        SetHaptics(recordStartFrequency, recordStartAmplitude);
        yield return new WaitForSecondsRealtime(recordStartSecondPulseDuration);

        StopHaptics();
        hapticsRoutine = null;
    }

    private IEnumerator RecordStopRoutine()
    {
        SetHaptics(recordStopFrequency, recordStopAmplitude);
        yield return new WaitForSecondsRealtime(recordStopDuration);
        StopHaptics();
        hapticsRoutine = null;
    }

    private IEnumerator SnapshotFlashRoutine()
    {
        recordingIndicator.ShowSnapshotFlash(true);
        yield return new WaitForSecondsRealtime(snapshotFlashDuration);
        recordingIndicator.ShowSnapshotFlash(false);
        snapshotFlashRoutine = null;
    }

    private void SetHaptics(float frequency, float amplitude)
    {
        OVRInput.SetControllerVibration(frequency, amplitude, controller);
    }

    private void StopHaptics()
    {
        OVRInput.SetControllerVibration(0f, 0f, controller);
    }

    private void OnDisable()
    {
        StopHaptics();

        if (recordingIndicator != null)
        {
            recordingIndicator.SetRecordingVisible(false);
            recordingIndicator.ShowSnapshotFlash(false);
        }
    }
}