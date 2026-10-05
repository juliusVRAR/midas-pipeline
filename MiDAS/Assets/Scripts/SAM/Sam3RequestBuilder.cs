using System;
using UnityEngine;
using UnityEngine.UI;

public class Sam3RequestBuilder : MonoBehaviour
{
    [Header("Optional metric anchor from Quest Depth API")]
    public bool useAnchor = false;
    public int anchorU = 0;
    public int anchorV = 0;
    public float anchorDepthMeters = 1.0f;

    [Header("Metadata")]
    public int sampleStride = 2;
    private Vector3 worldPoint = Vector3.zero;


    // ---------- Request schema (matches server) ----------
    [Serializable] public class Intrinsics
    {
        public int width, height;
        public float sensor_width, sensor_height;
        public float fx, fy, cx, cy;
    }

    [Serializable] public class Extrinsics
    {
        public float[][] T_world_cam;
    }

    [Serializable] public class BoxPrompt
    {
        public float[] boxXYXY;
    }

    [Serializable] public class PointPrompt
    {
        public int? image_id;
        public float[][] points;
        public int[] labels;
    }

    [Serializable] public class RequestMeta
    {
        public Intrinsics intrinsics;
        public Extrinsics extrinsics;
        public PointPrompt point_prompt;
        public string? text_prompt;
        public float[] world_point;

        public int? anchor_u;
        public int? anchor_v;
        public float? anchor_depth_m;

        public int sample_stride;
    }

    public static float[][] ToJagged4x4(Matrix4x4 m)
    {
        // Unity Matrix4x4 is row-major in indexing m[row,col]
        return new float[][]
        {
            new float[] { m.m00, m.m01, m.m02, m.m03 },
            new float[] { m.m10, m.m11, m.m12, m.m13 },
            new float[] { m.m20, m.m21, m.m22, m.m23 },
            new float[] { m.m30, m.m31, m.m32, m.m33 },
        };
    }

    public void SetWorldPoint(Vector3 worldPoint)
    {
        this.worldPoint = worldPoint;
    }

    public Vector3 GetWorldPoint()
    {
        return worldPoint;
    }

    public RequestMeta BuildMeta(PassthroughCameraCapture.ImageCaptureProperties captureProperties)
    {
        
        var meta = new RequestMeta
        {
            world_point = new float[] {worldPoint.x, worldPoint.y, worldPoint.z},
            intrinsics = captureProperties.intrinsics,
            extrinsics = new Extrinsics
            {
                T_world_cam = ToJagged4x4(Matrix4x4.TRS(captureProperties.cameraPose.position, captureProperties.cameraPose.rotation, Vector3.one)),
            },
            sample_stride = Mathf.Max(1, sampleStride),
        };

        var pts = new float[1][];
        pts[0] = new float[] { captureProperties.sam2Point.x, captureProperties.sam2Point.y };

        meta.point_prompt = new PointPrompt
        {
            points = pts,
            labels = new int[] {1}
        };
        meta.text_prompt = "";
        

        if (useAnchor)
        {
            meta.anchor_u = anchorU;
            meta.anchor_v = anchorV;
            meta.anchor_depth_m = anchorDepthMeters;
        }
        else
        {
            meta.anchor_u = null;
            meta.anchor_v = null;
            meta.anchor_depth_m = null;
        }

        return meta;
    }
}
