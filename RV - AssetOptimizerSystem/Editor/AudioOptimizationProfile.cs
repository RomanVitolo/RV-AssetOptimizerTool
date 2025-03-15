using UnityEngine;

namespace RV_AssetOptimizerSystem.Editor
{
    [System.Serializable]
    public class AudioOptimizationProfile 
    {
        public string profileName;
        public AudioCompressionFormat audioFormat;
        public int audioQuality;
        public AudioClipLoadType audioLoadType;
    }
}