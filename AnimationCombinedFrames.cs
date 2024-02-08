
using UnityEngine;

public class AnimationCombinedFrames : ScriptableObject
{
    [System.Serializable]
    public class FrameTimings
    {
        public string name;
        public int offset;
        public int frames;
        public float duration;
    }

    public FrameTimings[] data;
}
