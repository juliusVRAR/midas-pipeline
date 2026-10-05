using System;
using UnityEngine;

public class PoseExtractor : MonoBehaviour
{
    public int objectId = 0;
    public string objectName = "Object name";
    private MeshRenderer cubeMeshRenderer;
    private MeshRenderer objectMeshRenderer;
    private MeshFilter cubeMesh;
    void Awake()
    {
        var meshRendererComponents = gameObject.GetComponentsInChildren<MeshRenderer>();
        cubeMeshRenderer = meshRendererComponents[0];
        objectMeshRenderer = meshRendererComponents[1];
        
        cubeMesh = gameObject.GetComponentInChildren<MeshFilter>();
    }

    public void Disable3DObject()
    {
        objectMeshRenderer.enabled = !objectMeshRenderer.enabled;
    }

    public Vector3[] GetCubeMeshVertices()
    {
        Mesh mesh = cubeMesh.sharedMesh;
        Bounds b = mesh.bounds; // LOCAL space bounds

        Vector3 c = b.center;
        Vector3 e = b.extents;

        // 8 corners in LOCAL space
        Vector3[] localCorners = new Vector3[8]
        {
            c + new Vector3(-e.x, -e.y, -e.z),
            c + new Vector3( e.x, -e.y, -e.z),
            c + new Vector3( e.x, -e.y,  e.z),
            c + new Vector3(-e.x, -e.y,  e.z),

            c + new Vector3(-e.x,  e.y, -e.z),
            c + new Vector3( e.x,  e.y, -e.z),
            c + new Vector3( e.x,  e.y,  e.z),
            c + new Vector3(-e.x,  e.y,  e.z)
        };

        // Convert to WORLD space
        Vector3[] worldCorners = new Vector3[8];
        for (int i = 0; i < 8; i++)
            worldCorners[i] = cubeMesh.gameObject.transform.TransformPoint(localCorners[i]);

        return worldCorners;
    }

    public void SetObjectName(String name)
    {
        objectName = name;
    }
}
