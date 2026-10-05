using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

public class PoseObjectCoordinator : MonoBehaviour
{
    public static PoseObjectCoordinator Instance { get; private set; }

    private static readonly HashSet<PoseObject> _pendingRegistrations = new HashSet<PoseObject>();

    [Header("Dependencies")]
    [SerializeField] private PoseController networkController;
    [SerializeField] private PoseStreamer streamer;

    private readonly HashSet<PoseObject> _poseObjects = new HashSet<PoseObject>();

    public int TrackedCount => _poseObjects.Count;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        ProcessPendingRegistrations();
    }

    private void ProcessPendingRegistrations()
    {
        foreach (var obj in _pendingRegistrations)
        {
            if (obj != null)
                Register(obj);
        }
        _pendingRegistrations.Clear();
    }

    public static void RequestRegistration(PoseObject poseObject)
    {
        if (Instance != null)
            Instance.Register(poseObject);
        else
            _pendingRegistrations.Add(poseObject);
    }

    public static void RequestUnregistration(PoseObject poseObject)
    {
        if (Instance != null)
            Instance.Unregister(poseObject);
        else
            _pendingRegistrations.Remove(poseObject);
    }

    public void Register(PoseObject poseObject)
    {
        if (_poseObjects.Add(poseObject))
        {
            Debug.Log($"[PoseObjectCoordinator] Registered: {poseObject.ObjectName}. Total: {_poseObjects.Count}");
        }
    }

    public void Unregister(PoseObject poseObject)
    {
        if (_poseObjects.Remove(poseObject))
        {
            Debug.Log($"[PoseObjectCoordinator] Unregistered: {poseObject.ObjectName}. Total: {_poseObjects.Count}");
        }
    }

    public void ToggleAllObjectVisuals()
    {
        foreach (var obj in _poseObjects)
            obj?.ToggleObjectVisibility();
    }

    public void SetAllObjectVisuals(bool visible)
    {
        foreach (var obj in _poseObjects)
            obj?.SetObjectVisibility(visible);
    }

    public void SendPoses()
    {
        var poses = GetAllPoses();
        networkController.SendPose(poses);
    }

    public async Task<bool> StartStreamPoses(float timeout = 10f)
    {
        return await streamer.StartWebSocket(timeout);
    }
    public async Task StopStreamPosesAsync()
    {
        await streamer.StopStreamAndUploadModelsAsync();
    }

    public Vector3[][] GetAllPoses()
    {
        var poseList = new List<Vector3[]>();
        
        foreach (var obj in _poseObjects)
        {
            if (obj != null)
                poseList.Add(obj.GetBoundingBoxVertices());
        }

        return poseList.ToArray();
    }

    public List<(int id, string name, Transform transform)> GetAllObjectTransforms()
    {
        List<(int, string, Transform)> result = new();
        
        foreach (var obj in _poseObjects)
        {
            result.Add((
                obj.ObjectId,       
                obj.ObjectName,    
                obj.ObjectTransform
            ));
        }
        
        return result;
    }
    
    public PoseObject GetPoseObject(int objectId)
    {
        foreach (PoseObject obj in _poseObjects)
        {
            if (obj != null && obj.ObjectId == objectId)
                return obj;
        }

        return null;
    }
}