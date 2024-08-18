using UnityEngine;

namespace UTJ
{
    /// <summary>
    /// saved settings for AddrAutoGrouping
    /// </summary>
    public class AddrAutoGroupingSettings : ScriptableObject
    {
        public bool shaderGroup = false;
        public bool allowDuplicatedMaterial = true;
        //public int singleThreshold = 0;
        public string residentGroupGUID;
        
        public bool hashName = false;
        public bool useLocalProvider = false;
        public bool useLocalBuild = false;
        public bool clearBuildCache = false;
    }
}