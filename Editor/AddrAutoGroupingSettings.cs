using UnityEngine;

namespace AddrAuditor.Editor
{
    /// <summary>
    /// saved settings for AddrAutoGrouping
    /// </summary>
    public class AddrAutoGroupingSettings : ScriptableObject
    {
        public bool shaderGroup = false;
        public bool allowDuplicatedMaterial = true;
        public bool fastAnalysis = false; // [experimental] simulate the layout instead of re-running the analysis build
        //public int singleThreshold = 0;
        public string residentGroupGUID;
        
        public bool hashName = false;
        public bool useLocalProvider = false;
        public bool useLocalBuild = false;
        public bool clearBuildCache = false;
    }
}