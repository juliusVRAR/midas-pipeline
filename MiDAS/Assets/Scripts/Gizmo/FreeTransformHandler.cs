// FreeTransformHandler.cs
using UnityEngine;

namespace Oculus.Interaction.Gizmo
{
    public class FreeTransformHandler : MonoBehaviour, ITransformer
    {
        [SerializeField] private GrabFreeTransformer innerTransformer;
        [SerializeField] private RayInteractable rayInteractable;
        
        private IGrabbable _grabbable;
        private GizmoTransformManager _manager;
        
        public bool IsActive { get; private set; }
        
        public void Initialize(IGrabbable grabbable, GizmoTransformManager manager)
        {
            _manager = manager;
            _grabbable = grabbable;
            innerTransformer?.Initialize(grabbable);
        }
        
        public void Initialize(IGrabbable grabbable)
        {
            innerTransformer?.Initialize(grabbable);
        }
        
        public void SetActive(bool active)
        {
            IsActive = active;
            enabled = active;
            rayInteractable.enabled = active;
        }
        
        public void BeginTransform()
        {
            if (!IsActive) return;
            _manager?.OnFreeTransformStarted();
            innerTransformer?.BeginTransform();
        }
        
        public void UpdateTransform()
        {
            if (!IsActive) return;
            innerTransformer?.UpdateTransform();
        }
        
        public void EndTransform()
        {
            innerTransformer?.EndTransform();
        }
    }
}