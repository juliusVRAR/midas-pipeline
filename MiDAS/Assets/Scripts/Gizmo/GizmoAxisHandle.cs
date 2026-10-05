// GizmoAxisHandle.cs
using TMPro;
using UnityEngine;

namespace Oculus.Interaction.Gizmo
{
    public enum GizmoAxis { X, Y, Z }
    public enum GizmoMode { Translate, Scale, Rotate, Free }
    
    [RequireComponent(typeof(Grabbable))]
    public class GizmoAxisHandle : MonoBehaviour, ITransformer
    {
        [Header("Axis")]
        [SerializeField] private GizmoAxis axis;
        
        [Header("Head Visuals")]
        [SerializeField] private GameObject translateHead;
        [SerializeField] private GameObject scaleHead;
        [SerializeField] private GameObject rotateHead;
        
        [Header("Interactable Reference")]
        [SerializeField] private RayInteractable rayInteractable;  // ← Add this
        
        [Header("Settings")]
        [SerializeField] private float handleOffset = 0.15f;
        [SerializeField] private float translateSensitivity = 1f;
        [SerializeField] private float scaleSensitivity = 1f;
        [SerializeField] private float rotateSensitivity = 100f;
        
        [Header("Visual Feedback")]
        [SerializeField] private MeshRenderer[] headRenderers;
        [SerializeField] private Color hoverColor = Color.yellow;
        [SerializeField] private Color grabColor = Color.white;
    
        
        private IGrabbable _grabbable;
        private Transform _target;
        private GizmoMode _currentMode;
        private Color _baseColor;
        
        // Transform state
        private Vector3 _startGrabPosition;
        private Vector3 _startTargetPosition;
        private Vector3 _startTargetScale;
        private Quaternion _startTargetRotation;
        private Vector3 _worldAxisDirection;
        
        public GizmoAxis Axis => axis;
        public bool IsGrabbed { get; private set; }
        
        public System.Action<GizmoAxisHandle> OnGrabbed;
        public System.Action<GizmoAxisHandle> OnReleased;
        
        private void Awake()
        {
            _grabbable = gameObject.GetComponent<IGrabbable>();
            _baseColor = axis.ToColor();
            SetHeadColor(_baseColor);
            
            // Auto-find RayInteractable if not assigned
            if (rayInteractable == null)
            {
                rayInteractable = GetComponent<RayInteractable>();
            }
        }
        
        private void OnEnable()
        {
            SubscribeToPointerEvents();
        }
        
        private void OnDisable()
        {
            UnsubscribeFromPointerEvents();
        }
        
        #region Pointer Event Subscription
        
        private void SubscribeToPointerEvents()
        {
            if (rayInteractable != null)
            {
                rayInteractable.WhenPointerEventRaised += HandlePointerEvent;
            }
        }
        
        private void UnsubscribeFromPointerEvents()
        {
            if (rayInteractable != null)
            {
                rayInteractable.WhenPointerEventRaised -= HandlePointerEvent;
            }
        }
        
        private void HandlePointerEvent(PointerEvent pointerEvent)
        {
            switch (pointerEvent.Type)
            {
                case PointerEventType.Hover:
                    OnHoverEnter();
                    break;
                case PointerEventType.Unhover:
                    OnHoverExit();
                    break;
                case PointerEventType.Select:
                    // Optional: handle select start
                    break;
                case PointerEventType.Unselect:
                    // Optional: handle select end
                    break;
            }
        }
        
        #endregion
        
        #region Initialization
        
        public void Initialize(Transform target)
        {
            _target = target;
            UpdateHandlePosition();
        }
        
        public void Initialize(IGrabbable grabbable)
        {
            // ITransformer interface requirement
        }
        
        #endregion
        
        #region Mode Management
        
        public void SetMode(GizmoMode mode)
        {
            _currentMode = mode;
            
            bool showHandles = mode != GizmoMode.Free;
            translateHead?.SetActive(showHandles && mode == GizmoMode.Translate);
            scaleHead?.SetActive(showHandles && mode == GizmoMode.Scale);
            rotateHead?.SetActive(showHandles && mode == GizmoMode.Rotate);
            
            gameObject.SetActive(showHandles);
            
            if (showHandles)
                UpdateHandlePosition();
        }
        
        #endregion
        
        #region Transform Operations
        
        public void BeginTransform()
        {
            if (_target == null || _grabbable.GrabPoints.Count == 0)
            {
                return;
            }
            
            IsGrabbed = true;
            
            _startGrabPosition = _grabbable.GrabPoints[0].position;
            _startTargetPosition = _target.position;
            _startTargetScale = _target.localScale;
            _startTargetRotation = _target.rotation;
            _worldAxisDirection = _target.TransformDirection(axis.ToLocalDirection());
            
            SetHeadColor(grabColor);
            OnGrabbed?.Invoke(this);
        }
        
        public void UpdateTransform()
        {
            if (_target == null || _grabbable.GrabPoints.Count == 0) return;
            
            float delta = CalculateDelta();
            
            switch (_currentMode)
            {
                case GizmoMode.Translate:
                    ApplyTranslate(delta);
                    break;
                case GizmoMode.Scale:
                    ApplyScale(delta);
                    break;
                case GizmoMode.Rotate:
                    ApplyRotate(delta);
                    break;
            }
            
            UpdateHandlePosition();
        }
        
        public void EndTransform()
        {
            IsGrabbed = false;
            SetHeadColor(_baseColor);
            OnReleased?.Invoke(this);
        }
        
        private float CalculateDelta()
        {
            Vector3 currentGrab = _grabbable.GrabPoints[0].position;
            Vector3 movement = currentGrab - _startGrabPosition;
            return Vector3.Dot(movement, _worldAxisDirection);
        }
        
        private void ApplyTranslate(float delta)
        {
            _target.position = _startTargetPosition + _worldAxisDirection * delta * translateSensitivity;
        }
        
        private void ApplyScale(float delta)
        {
            float scaleFactor = 1f + delta * scaleSensitivity;
            scaleFactor = Mathf.Max(0.01f, scaleFactor);
            _target.localScale = _startTargetScale * scaleFactor;
        }
        
        private void ApplyRotate(float delta)
        {
            float angle = delta * rotateSensitivity;
            Quaternion rotation = Quaternion.AngleAxis(angle, axis.ToRotateDirection());
            _target.rotation = _startTargetRotation * rotation;
        }
        
        #endregion
        
        #region Handle Position
        
        public void UpdateHandlePosition()
        {
            if (_target == null) return;
            
            // Align handle parent with target
            Transform handlesParent = transform.parent;
            handlesParent.SetPositionAndRotation(_target.position, _target.rotation);

            // Place handle outside the target bounds along the axis
            MeshFilter meshFilter = _target.GetComponentInChildren<MeshFilter>();
            Vector3 extents = Vector3.Scale(meshFilter.sharedMesh.bounds.extents, meshFilter.transform.localScale);
            Vector3 localExtents = Vector3.Scale(extents, _target.localScale);
            Vector3 localDir = axis.ToLocalDirection();
            float offset = handleOffset;
            
            transform.localPosition = Vector3.Scale(localExtents, localDir) + offset * localDir;
        }
        
        #endregion
        
        #region Visual Feedback
        
        private void SetHeadColor(Color color)
        {
            foreach (var renderer in headRenderers)
            {
                if (renderer != null)
                    renderer.material.color = color;
            }
        }
        
        public void OnHoverEnter()
        {
            if (!IsGrabbed)
            {
                SetHeadColor(hoverColor);
            }
        }
        
        public void OnHoverExit()
        {
            if (!IsGrabbed)
            {
                SetHeadColor(_baseColor);
            }
        }
        
        #endregion
    }
    
    public static class GizmoAxisExtensions
    {
        public static Vector3 ToLocalDirection(this GizmoAxis axis)
        {
            return axis switch
            {
                GizmoAxis.X => Vector3.right,
                GizmoAxis.Y => Vector3.up,
                GizmoAxis.Z => Vector3.forward,
                _ => Vector3.forward
            };
        }

        public static Vector3 ToRotateDirection(this GizmoAxis axis)
        {
            return axis switch
            {
                GizmoAxis.X => Vector3.up,
                GizmoAxis.Y => Vector3.forward,
                GizmoAxis.Z => Vector3.right,
                _ => Vector3.forward
            };
        }
        
        public static Color ToColor(this GizmoAxis axis)
        {
            return axis switch
            {
                GizmoAxis.X => new Color(1f, 0.3f, 0.3f),
                GizmoAxis.Y => new Color(0.3f, 1f, 0.3f),
                GizmoAxis.Z => new Color(0.3f, 0.5f, 1f),
                _ => Color.white
            };
        }
    }
}