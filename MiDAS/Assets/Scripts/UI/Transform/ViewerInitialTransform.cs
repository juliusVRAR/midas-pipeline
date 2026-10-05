using UnityEngine;

public class ViewerInitialTransform : MonoBehaviour
{
    [SerializeField] private Transform head;    
    public float initialForwardOffset = 1.5f;
    public float initialHorizontalOffset = 1f;
    
    void OnEnable()
    {
        
    }
}
