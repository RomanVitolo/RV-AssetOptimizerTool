using UnityEditor;

namespace RV_AssetOptimizerSystem.Editor
{
    [System.Serializable]
    public class TextureOptimizationProfile
    {
        public string profileName;
        public int textureMaxSize;
        public TextureImporterFormat textureFormat;
        public bool generateMipMaps;
    }
}