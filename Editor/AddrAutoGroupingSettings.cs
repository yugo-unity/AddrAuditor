using UnityEngine;


namespace UTJ
{
    /// <summary>
    /// AddrAutoGroupingの設定ファイル
    /// </summary>
    public class AddrAutoGroupingSettings : ScriptableObject
    {
        public bool hashName = true;
        public bool shaderGroup = false;
        public bool allowDuplicatedMaterial = true;
        //public int singleThreshold = 0;
        public string residentGroupGUID;
    }
}