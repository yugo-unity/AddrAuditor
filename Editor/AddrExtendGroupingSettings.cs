using UnityEngine;


namespace UTJ
{
    public class AddrExtendGroupingSettings : ScriptableObject
    {
        public bool hashName = true;
        public bool shaderGroup = false;
        public bool allowDuplicatedMaterial = true;
        //public int singleThreshold = 0;
        public string residentGroupGUID;
    }
}