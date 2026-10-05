using UnityEngine;
using UnityEngine.UI;

public class PoseViewer : MonoBehaviour
{
    public RawImage image;
    void Start()
    {
        gameObject.SetActive(false);
    }

    public void SetImage(Texture2D imageTexture)
    {
        image.texture = imageTexture;
    }

    public void TogglePose()
    {
        gameObject.SetActive(!gameObject.activeSelf);
    }
}
