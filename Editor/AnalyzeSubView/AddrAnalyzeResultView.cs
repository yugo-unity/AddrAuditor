using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor;
using UnityEditor.AddressableAssets.Settings;

namespace AddrAuditor.Editor
{
    internal class AnalyzeCache
    {
        public AddressableAssetSettings addrSetting;
        public List<RefAssetData> refAssets;
        public List<AddressableAssetEntry> explicitEntries;
        public List<SpriteAtlasData> spriteAtlases;
        public readonly Dictionary<string, List<RefEntry>> refEntryDic = new ();
        public readonly Dictionary<string, HashSet<string>> dependencyDic = new (); // asset path -> recursive dependency paths
    }

    internal class RefEntry
    {
        public string groupPath;
        public string assetPath;

        public RefEntry(string groupPath, string assetPath)
        {
            this.groupPath = groupPath;
            this.assetPath = assetPath;
        }
    }

    /// <summary>
    /// view to display result of analytics
    /// </summary>
    internal abstract class ResultView
    {
        protected const float REF_VIEW_DETAIL_HEIGHT = 160f;
        protected const int PROGRESS_INTERVAL_MASK = 63; // update progress bar every 64 items
        public VisualElement rootElement { get; private set; }
        public virtual bool requireAnalyzeCache => false;

        public void CreateView(TwoPaneSplitViewOrientation orientation)
        {
            var dimension = orientation == TwoPaneSplitViewOrientation.Horizontal ? 580 : 120;
            var root = new TwoPaneSplitView(0, dimension, orientation);
            this.rootElement = root;
            this.OnCreateView();
            this.UpdateView();
        }
        
        /// <summary>
        /// called when require to analyze
        /// </summary>
        /// <param name="cache">build cache that created by AddrAnalyzeWindow</param>
        public abstract void Analyze(AnalyzeCache cache);

        /// <summary>
        /// called when created view (only once)
        /// </summary>
        protected abstract void OnCreateView();

        /// <summary>
        /// called when selecting any category
        /// </summary>
        public abstract void UpdateView();

        /// <summary>
        /// get the duplicated assets what is referenced 2+
        /// </summary>
        /// <param name="ret">result</param>
        /// <param name="analyzeCache">Addressable build cache</param>
        protected static void FindDuplicatedAssets(List<RefAssetData> ret, AnalyzeCache analyzeCache)
        {
            foreach (var param in analyzeCache.refAssets)
            {
                if (param.bundles.Count == 1) // not duplicated
                    continue;
                ret.Add(param);
            }
            ret.Sort(CompareName);
        }

        /// <summary>
        /// get the referencing entries what is referencing a specific explicit/implicit asset  
        /// </summary>
        /// <param name="ret">result</param>
        /// <param name="analyzeCache">Addressable build cache</param>
        /// <param name="refAsset">explicit/implicit asset</param>
        protected List<RefEntry> FindReferencedEntries(AnalyzeCache analyzeCache, RefAssetData refAsset)
        {
            var ret = new List<RefEntry>();
            var refAssetPath = refAsset.path;
            var isSpriteInAtlas = refAsset.usedSubAssetTypes.Contains(typeof(Sprite)) && refAsset.usedSubAssetTypes.Count == 1;
            // Textures that is included in SpriteAtlas only referenced as Sprites are treated as SpriteAtlas
            if (isSpriteInAtlas)
            {
                var atlas = AddrAutoGrouping.FindPackingAtlas(refAsset, analyzeCache.spriteAtlases);
                if (atlas != null)
                    refAssetPath = AssetDatabase.GetAssetPath(atlas.instance);
            }

            // the dependencies are cached until the analyze cache is cleared
            bool DependsOn(string path)
            {
                if (!analyzeCache.dependencyDic.TryGetValue(path, out var dependencyPaths))
                {
                    dependencyPaths = new HashSet<string>(AssetDatabase.GetDependencies(path, true));
                    analyzeCache.dependencyDic.Add(path, dependencyPaths);
                }
                return dependencyPaths.Contains(refAssetPath);
            }

            var entryCount = analyzeCache.explicitEntries.Count;
            for (var i = 0; i < entryCount; ++i)
            {
                var entry = analyzeCache.explicitEntries[i];

                if ((i & PROGRESS_INTERVAL_MASK) == 0)
                    EditorUtility.DisplayCancelableProgressBar("Searching Referring Entries...", refAsset.path, (float)i/entryCount);

                if (entry.IsFolder)
                {
                    foreach (var asset in entry.SubAssets)
                        if (DependsOn(asset.AssetPath))
                            ret.Add(new RefEntry(entry.parentGroup.name, asset.AssetPath));
                }
                else
                {
                    if (DependsOn(entry.AssetPath))
                        ret.Add(new RefEntry(entry.parentGroup.name, entry.AssetPath));
                }
            }
            EditorUtility.ClearProgressBar();

            if (ret.Count == 0)
            {
                // SpriteAtlasが重複しているがSpriteAtlasがEntryにないケースは暗黙アセットであるSpriteAtlasを警告する
                // 暗黙アセットであるSpriteAtlasを参照しているEntryを検出すると、
                // プロジェクトによっては多数リストアップされ、本質的に何が重複アセットなのかわからなくなる懸念がある
                if (isSpriteInAtlas)
                    ret.Add(new RefEntry(null, refAssetPath));
                else
                    Debug.LogError($"Unknown error, not found referenced AddressableEntry {refAsset.path}");
            }
            
            ret.Sort(CompareName);
            return ret;
        }
        
        // sort by alphanumeric
        static int CompareName(RefAssetData a, RefAssetData b) => AddrUtility.CompareAlphanumeric(a.path, b.path);
        static int CompareName(RefEntry a, RefEntry b) => AddrUtility.CompareAlphanumeric(a.assetPath, b.assetPath);
    }
}