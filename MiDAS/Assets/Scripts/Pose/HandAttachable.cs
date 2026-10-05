using UnityEngine;
using Oculus.Interaction.Gizmo;

public class HandAttachable : MonoBehaviour
{
    private Transform _handAnchor;
    private Vector3 _positionOffset;
    private Vector3 _rotationOffset;
    private Vector3 _originalScale;
    private bool _isAttachedToHand = true;
    private GizmoTransformManager _gizmoManager;

    public bool IsAttachedToHand => _isAttachedToHand;
    
    private void Awake()
    {
        if (_originalScale != Vector3.zero)
        {
            _originalScale = transform.localScale;
        }
        _gizmoManager = GetComponent<GizmoTransformManager>();
    }

    private void Start()
    {
        if (_gizmoManager != null)
        {
            _gizmoManager.OnTransformStarted += DetachFromHand;
        }
    }
    
    /// <summary>
    /// Called by HandAttachmentManager to initialize position
    /// </summary>
    public void Initialize(Transform handAnchor, Vector3 positionOffset, Vector3 rotationOffset)
    {
        _handAnchor = handAnchor;
        _positionOffset = positionOffset;
        _rotationOffset = rotationOffset;
        
        AttachToHand();
    }
    
    /// <summary>
    /// Called by HandAttachmentManager to update position
    /// </summary>
    public void SetOffsets(Vector3 positionOffset, Vector3 rotationOffset)
    {
        _positionOffset = positionOffset;
        _rotationOffset = rotationOffset;
        
        if (_isAttachedToHand)
        {
        transform.localPosition = _positionOffset;
        transform.localRotation = Quaternion.Euler(_rotationOffset);
        }
    }
    
    public void AttachToHand()
    {
        if (_handAnchor == null)
        {
            Debug.LogWarning($"[HandAttachable] No hand anchor for {gameObject.name}");
            return;
        }
        
        transform.SetParent(_handAnchor);
        transform.SetLocalPositionAndRotation(_positionOffset, Quaternion.Euler(_rotationOffset));
        transform.localScale = _originalScale;
        _gizmoManager.ResetTransform();
        _isAttachedToHand = true;
    }
    
    public void DetachFromHand()
    {
        if (!_isAttachedToHand) return;
        
        Vector3 worldPos = transform.position;
        Quaternion worldRot = transform.rotation;
        
        transform.SetParent(null);
        transform.position = worldPos;
        transform.rotation = worldRot;
        
        _isAttachedToHand = false;
    }

    public void SetDefaultSize(float targetSizeMeters)
    {
        PoseObject poseObject = GetComponentInChildren<PoseObject>();
        if (poseObject == null)
        {
            Debug.LogWarning($"[HandAttachable] No PoseObject found on {gameObject.name}, skipping size normalization.");
            return;
        }

        Bounds bounds = poseObject.GetWorldMeshBounds();
        float longestAxis = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
        Debug.Log($"[HandAttachable] '{gameObject.name}' bounds size: {bounds.size} (longestAxis={longestAxis*100f:F1}cm)");

        if (longestAxis <= Mathf.Epsilon)
        {
            Debug.LogWarning($"[HandAttachable] Bounds size is zero on {gameObject.name}, skipping size normalization.");
            return;
        }

        float scaleFactor = targetSizeMeters / longestAxis;
        transform.localScale *= scaleFactor;

        // Persist so AttachToHand() restores this scale, not the prefab default
        _originalScale = transform.localScale;

        Debug.Log($"[HandAttachable] '{gameObject.name}' normalized: longestAxis={longestAxis*100f:F1}cm → scaleFactor={scaleFactor:F4}");
    }
    
    
    private void OnDestroy()
    {
        if (_gizmoManager != null)
        {
            _gizmoManager.OnTransformStarted -= DetachFromHand;
        }
    }
}