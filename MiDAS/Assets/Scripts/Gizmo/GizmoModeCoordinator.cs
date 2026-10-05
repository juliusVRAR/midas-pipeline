using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace Oculus.Interaction.Gizmo
{
    public class GizmoModeCoordinator : MonoBehaviour
    {
        public static GizmoModeCoordinator Instance { get; private set; }
        
        // Queue for managers that try to register before Instance exists
        private static readonly HashSet<GizmoTransformManager> _pendingRegistrations = new HashSet<GizmoTransformManager>();
        
        [SerializeField] private GizmoMode initialMode = GizmoMode.Translate;
        
        public UnityEvent<GizmoMode> OnGlobalModeChanged;
        
        private readonly HashSet<GizmoTransformManager> _managers = new HashSet<GizmoTransformManager>();
        private GizmoMode _currentMode;
        
        public GizmoMode CurrentMode => _currentMode;
        
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            _currentMode = initialMode;
            
            // Process any pending registrations
            ProcessPendingRegistrations();
        }
        
        private void ProcessPendingRegistrations()
        {
            foreach (var manager in _pendingRegistrations)
            {
                if (manager != null)
                    Register(manager);
            }
            _pendingRegistrations.Clear();
        }
        
        public static void RequestRegistration(GizmoTransformManager manager)
        {
            if (Instance != null)
            {
                Instance.Register(manager);
            }
            else
            {
                // Queue for later when Instance becomes available
                _pendingRegistrations.Add(manager);
            }
        }
        
        public static void RequestUnregistration(GizmoTransformManager manager)
        {
            if (Instance != null)
            {
                Instance.Unregister(manager);
            }
            else
            {
                _pendingRegistrations.Remove(manager);
            }
        }
        
        public void Register(GizmoTransformManager manager)
        {
            if (_managers.Add(manager))
            {
                manager.SetMode(_currentMode);
                Debug.Log($"[GizmoCoordinator] Registered: {manager.name}. Total: {_managers.Count}");
            }
        }
        
        public void Unregister(GizmoTransformManager manager)
        {
            _managers.Remove(manager);
        }
        
        public void SetGlobalMode(GizmoMode mode)
        {
            if (_currentMode == mode) return;
            
            _currentMode = mode;
            
            foreach (var manager in _managers)
                manager?.SetMode(mode);
            
            OnGlobalModeChanged?.Invoke(mode);
        }
        
        // Convenience methods for UI
        public void SetTranslate() => SetGlobalMode(GizmoMode.Translate);
        public void SetScale() => SetGlobalMode(GizmoMode.Scale);
        public void SetRotate() => SetGlobalMode(GizmoMode.Rotate);
        public void SetFree() => SetGlobalMode(GizmoMode.Free);
        public void CycleMode() => SetGlobalMode((GizmoMode)(((int)_currentMode + 1) % 4));
    }
}