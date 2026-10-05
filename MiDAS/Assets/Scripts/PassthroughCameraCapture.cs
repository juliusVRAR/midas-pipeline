using System;
using System.Collections;
using System.Collections.Generic;
using Meta.XR;
using UnityEngine;
using UnityEngine.UI;
using static PoseController;
using static Sam3RequestBuilder;

public class PassthroughCameraCapture : MonoBehaviour
{

    [SerializeField] private PassthroughCameraAccess cameraAccess;
    [SerializeField] private Sam3RequestBuilder requestBuilder;
    [Range(1, 100)] public int jpgQuality = 80;
    private Vector2Int _cameraResolution;
    private Texture2D _reusableTexture;

    private IEnumerator Start()
    {

        while (!cameraAccess.IsPlaying)
        {
            yield return null;
        }
        _cameraResolution = cameraAccess.CurrentResolution;
    }

    [Serializable]
    public struct ImageCaptureProperties
    {
        public Texture2D imageTexture;
        public byte[] imageBytes;
        public Pose cameraPose;
        public Intrinsics intrinsics;
        public Vector2 sam2Point;
    }

    [Serializable]
    public struct ObjectTransformData
    {
        public int objectId;
        public string objectName;
        public float[] position;      // [x, y, z] world position
        public float[] rotation;      // [x, y, z, w] quaternion
        public float[] scale;         // [x, y, z] local scale
        public float[][] bbox_world;  // 8 vertices in world space
    }

    [Serializable]
    public struct CameraPosePacket
    {
        public byte[] imageBytes;
        public float[] cameraPose;           // [px, py, pz, qx, qy, qz, qw]
        public List<ObjectTransformData> objects;
    }

   

    public bool CaptureImage(out ImageCaptureProperties captureProperties)
    {
        captureProperties = default;

        if (cameraAccess == null) return false;
        
        // Get texture and point
        Texture2D imageTexture = new(_cameraResolution.x, _cameraResolution.y, TextureFormat.RGB24, false);
    

        // Get camera values
        Texture cameraTexture = cameraAccess.GetTexture();
        if (!TryGetPixelCoordinate(out var cameraPoint)) return false;
        Pose cameraPose = cameraAccess.GetCameraPose();

        // Get camera image
        imageTexture.SetPixels(((Texture2D)cameraTexture).GetPixels());
        imageTexture.Apply();
        byte[] imageBytes = imageTexture.EncodeToJPG(jpgQuality);

        captureProperties = new ImageCaptureProperties
        {
            imageTexture = imageTexture,
            imageBytes = imageBytes,
            cameraPose = cameraPose,
            intrinsics = GetIntrinsics(),
            sam2Point = cameraPoint,
        };

        return true;
    }

    public CameraPosePacket CaptureJpegWithPose()
    {
        byte[] imageBytes = CaptureJpeg();
        Pose cameraPose = cameraAccess.GetCameraPose();

        return new CameraPosePacket
        {
            imageBytes = imageBytes,
            cameraPose = new float[] {
                cameraPose.position.x,
                cameraPose.position.y,
                cameraPose.position.z,
                cameraPose.rotation.x,
                cameraPose.rotation.y,
                cameraPose.rotation.z,
                cameraPose.rotation.w
            }
        };
    }

    private bool TryGetPixelCoordinate(out Vector2Int pixel)
    {
        Vector3 worldPoint = requestBuilder.GetWorldPoint();
        pixel = default;
        if (!cameraAccess || !cameraAccess.IsPlaying || _cameraResolution == Vector2Int.zero)
        {
            return false;
        }

        var viewport = cameraAccess.WorldToViewportPoint(worldPoint);
        if (viewport.x < 0f || viewport.x > 1f || viewport.y < 0f || viewport.y > 1f)
        {
            return false;
        }
        
        // Convert to opencv 
        pixel = new Vector2Int(
            Mathf.Clamp(Mathf.RoundToInt(viewport.x * (_cameraResolution.x - 1)), 0, _cameraResolution.x - 1),
            Mathf.Clamp(Mathf.RoundToInt((1 - viewport.y) * (_cameraResolution.y - 1)), 0, _cameraResolution.y - 1));
        return true;
    }
    


    public byte[] CaptureJpeg()
    {
        if (cameraAccess == null) return null;

        Vector2Int size = cameraAccess.CurrentResolution;
        
        if (_reusableTexture == null || 
            _reusableTexture.width != size.x || 
            _reusableTexture.height != size.y)
        {
            if (_reusableTexture != null)
                Destroy(_reusableTexture);
            _reusableTexture = new Texture2D(size.x, size.y, TextureFormat.RGB24, false);
        }
        Texture cameraTexture = cameraAccess.GetTexture();
        _reusableTexture.SetPixels(((Texture2D)cameraTexture).GetPixels());
        _reusableTexture.Apply();

        return _reusableTexture.EncodeToJPG(jpgQuality);
    }

    public byte[] CapturePNG()
    {
        if (cameraAccess == null) return null;

        Vector2Int size = cameraAccess.CurrentResolution;
        
        if (_reusableTexture == null || 
            _reusableTexture.width != size.x || 
            _reusableTexture.height != size.y)
        {
            if (_reusableTexture != null)
                Destroy(_reusableTexture);
            _reusableTexture = new Texture2D(size.x, size.y, TextureFormat.RGB24, false);
        }
        Texture cameraTexture = cameraAccess.GetTexture();
        _reusableTexture.SetPixels(((Texture2D)cameraTexture).GetPixels());
        _reusableTexture.Apply();

        return _reusableTexture.EncodeToPNG();
    }

    public PosePacket CaptureJpegAndPose(Vector3[][] vertices)
    {
        byte[] imageBytes = CaptureJpeg();
        List<List<Vector2>> verticesInViewport = new();
        foreach(Vector3[] pose in vertices)
        {
            List<Vector2> objVertices = new();
            foreach(Vector3 v in pose)
            {
                objVertices.Add(cameraAccess.WorldToViewportPoint(v));
            }
            verticesInViewport.Add(objVertices);
        }
        

        return new PosePacket
        {
            imageBytes=imageBytes,
            vertices=verticesInViewport
        };
    }
    

    public Intrinsics GetIntrinsics() {
        var intrinsics = cameraAccess.Intrinsics;
        return new Intrinsics 
        {
            width = _cameraResolution.x,
            height = _cameraResolution.y,
            sensor_width = intrinsics.SensorResolution.x,
            sensor_height = intrinsics.SensorResolution.y,
            fx = intrinsics.FocalLength.x,
            fy = intrinsics.FocalLength.y,
            cx = intrinsics.PrincipalPoint.x,
            cy = intrinsics.PrincipalPoint.y,
        };
    }

    void OnDestroy()
    {
        if (_reusableTexture != null)
            Destroy(_reusableTexture);
    }


    public Vector2Int Size() => cameraAccess.CurrentResolution;
}
