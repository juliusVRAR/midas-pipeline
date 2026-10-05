using UnityEngine;
using UnityEngine.UI;
using static Sam3RequestBuilder;

public class Sam3ImageViewer : MonoBehaviour
{
    [SerializeField] private RawImage image;
    [SerializeField] private RawImage mask;
    [SerializeField] private RectTransform promptContainer;
    public RequestMeta Metadata
    {get; set;}
    public byte[] ImageBytes
    {get; set;}

    void Start()
    {
        gameObject.SetActive(false);
    }

    public void SetImage(Texture2D imageTexture)
    {
        image.texture = imageTexture;
    }

    public RawImage GetRawImage()
    {
        return image;
    }

    public RectTransform GetPromptContainer()
    {
        return promptContainer;
    }

    public void SetMask(Texture2D maskTexture)
    {
        mask.texture = maskTexture;
    }

    public byte[] MaskBytes
    {
        get
        {
            Texture2D maskTex = mask.texture as Texture2D;
            if (maskTex == null)
            {
                Debug.LogWarning("[Sam3ImageViewer] Mask texture is null or not a Texture2D.");
                return null;
            }
            return maskTex.EncodeToPNG();
        }
    }
}
