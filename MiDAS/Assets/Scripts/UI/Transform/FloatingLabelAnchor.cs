using TMPro;
using UnityEngine;

public class FloatingLabelAnchor : MonoBehaviour
{
    [Header("Anchor")]
    public Transform target;                 // gizmo root or cube
    private Transform head;                   // CenterEyeAnchor or Camera.main.transform

    [Header("Offsets (meters)")]
    public float height = 0.10f;             // above target
    public float towardUser = 0.08f;         // toward camera

    [Header("Orientation")]
    public bool faceUser = true;
    public bool lockRoll = true;
    public float maxPitchDeg = 45f;          // optional clamp

    [Header("Smoothing")]
    public float posLerp = 20f;              // higher = snappier
    public float rotLerp = 20f;

    private InputProxy inputProxy;

    void Start()
    {
        head = Camera.main.transform;
    }

    void LateUpdate()
    {
        if (!target || !head) return;

        Vector3 targetPos = target.position;

        // Direction from target to user, flattened a bit if you want less vertical motion:
        Vector3 toUser = head.position - targetPos;
        Vector3 toUserDir = toUser.normalized;

        // Place bar above target and biased toward user
        Vector3 desiredPos = targetPos + Vector3.up * height + toUserDir * towardUser;

        // Smooth position
        transform.position = Vector3.Lerp(transform.position, desiredPos, 1f - Mathf.Exp(-posLerp * Time.deltaTime));

        // Rotate to face user (no roll)
        if (faceUser)
        {
            Vector3 lookDir = (transform.position - head.position).normalized;

            Quaternion desiredRot;

            if (lockRoll)
            {
                desiredRot = Quaternion.LookRotation(lookDir, Vector3.up);

                // Optional: clamp pitch so it doesn't tilt too much
                Vector3 e = desiredRot.eulerAngles;
                e.x = ClampAngle(e.x, -maxPitchDeg, maxPitchDeg);
                desiredRot = Quaternion.Euler(e.x, e.y, 0f);
            }
            else
            {
                desiredRot = Quaternion.LookRotation(lookDir);
            }

            transform.rotation = Quaternion.Slerp(transform.rotation, desiredRot, 1f - Mathf.Exp(-rotLerp * Time.deltaTime));
        }
    }

    static float ClampAngle(float angleDeg, float minDeg, float maxDeg)
    {
        angleDeg = Mathf.Repeat(angleDeg + 180f, 360f) - 180f;
        return Mathf.Clamp(angleDeg, minDeg, maxDeg);
    }
}
