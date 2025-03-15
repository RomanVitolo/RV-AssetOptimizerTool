using System.Collections.Generic;

namespace RV_AssetOptimizerSystem.Editor
{
    [System.Serializable]
    public class OptimizationProfiles 
    {
        public List<TextureOptimizationProfile> textureProfiles = new List<TextureOptimizationProfile>();
        public List<ModelOptimizationProfile> modelProfiles = new List<ModelOptimizationProfile>();
        public List<AudioOptimizationProfile> audioProfiles = new List<AudioOptimizationProfile>();
    }
}