// GizmoTransformManager.cs
using System;
using UnityEngine;

namespace Oculus.Interaction.Gizmo
{
    /// <summary>
    /// Manages gizmo handles for a single target object
    /// </summary>
    public class GizmoTransformManager : MonoBehaviour
    {
        [Header("Target")]
        [SerializeField] private Transform target;
        [SerializeField] private Grabbable targetGrabbable;
        
        [Header("Axis Handles")]
        [SerializeField] private GizmoAxisHandle xHandle;
        [SerializeField] private GizmoAxisHandle yHandle;
        [SerializeField] private GizmoAxisHandle zHandle;
        
        [Header("Free Transform")]
        [SerializeField] private FreeTransformHandler freeHandler;
        
        [Header("Settings")]
        [SerializeField] private bool autoRegister = true;
        
        private GizmoMode _currentMode = GizmoMode.Translate;
        private Vector3 _initialScale;
        
        public GizmoMode CurrentMode => _currentMode;
        public Transform Target => target;
        
        public event Action<GizmoMode> OnModeChanged;
        public event Action OnTransformStarted;

        private void Awake()
        {
            _initialScale = target.localScale;
        }
        
        private void Start()
        {
            InitializeHandles();
            SetMode(_currentMode);
        }
        
        private void OnEnable()
        {
            if (autoRegister)
                GizmoModeCoordinator.RequestRegistration(this);
            
            SubscribeToHandles();
        }
        
        private void OnDisable()
        {
            if (autoRegister)
                GizmoModeCoordinator.RequestUnregistration(this);
            
            UnsubscribeFromHandles();
        }
        
        private void InitializeHandles()
        {
            if (target == null)
                target = transform.parent;
            
            xHandle?.Initialize(target);
            yHandle?.Initialize(target);
            zHandle?.Initialize(target);
            
            if (freeHandler != null && targetGrabbable != null)
                freeHandler.Initialize(targetGrabbable, this);
        }
        
        private void SubscribeToHandles()
        {
            if (xHandle != null) xHandle.OnGrabbed += OnHandleGrabbed;
            if (yHandle != null) yHandle.OnGrabbed += OnHandleGrabbed;
            if (zHandle != null) zHandle.OnGrabbed += OnHandleGrabbed;
        }
        
        private void UnsubscribeFromHandles()
        {
            if (xHandle != null) xHandle.OnGrabbed -= OnHandleGrabbed;
            if (yHandle != null) yHandle.OnGrabbed -= OnHandleGrabbed;
            if (zHandle != null) zHandle.OnGrabbed -= OnHandleGrabbed;
        }
        
        public void SetMode(GizmoMode mode)
        {
            _currentMode = mode;
            
            xHandle?.SetMode(mode);
            yHandle?.SetMode(mode);
            zHandle?.SetMode(mode);
            freeHandler?.SetActive(mode == GizmoMode.Free);
            
            OnModeChanged?.Invoke(mode);
        }
        
        private void OnHandleGrabbed(GizmoAxisHandle handle)
        {
            // Update all handles after any transform
            OnTransformStarted?.Invoke();
        }
        
        public void OnFreeTransformStarted()
        {
            OnTransformStarted?.Invoke();
        }
        
        private void UpdateAllHandlePositions()
        {
            xHandle?.UpdateHandlePosition();
            yHandle?.UpdateHandlePosition();
            zHandle?.UpdateHandlePosition();
        }

        public void ResetTransform()
        {
            if (target != null)
            {
                target.SetLocalPositionAndRotation(Vector3.zero, Quaternion.Euler(Vector3.zero));
                target.localScale = _initialScale;
                UpdateAllHandlePositions();
            }
        }
        
        public void SetAxisVisible(GizmoAxis axis, bool visible)
        {
            GizmoAxisHandle handle = axis switch
            {
                GizmoAxis.X => xHandle,
                GizmoAxis.Y => yHandle,
                GizmoAxis.Z => zHandle,
                _ => null
            };
            
            handle?.gameObject.SetActive(visible);
        }
    }
}