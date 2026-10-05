using System;
using UnityEngine;

[Serializable]
public class Sam3Response
{
    [Serializable]
    public class SegmentResponse
    {
        public bool ok;
        public string mask_png;
    }
    
}

