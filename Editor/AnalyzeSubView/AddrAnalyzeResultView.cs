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
    }

    internal class RefEntry
    {
        public string groupPath;
        public string assetPath;
    }

    /// <summary>
    /// view to display result of analytics
    /// </summary>
    internal abstract class ResultView
    {
        public VisualElement rootElement { get; private set; }
        public virtual bool requireAnalyzeCache => false;

        public void CreateView(TwoPaneSplitViewOrientation orientation)
        {
            var dimension = orientation == TwoPaneSplitViewOrientation.Horizontal ? 500 : 70;
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
        protected static void FindReferencedEntries(List<RefEntry> ret, AnalyzeCache analyzeCache, RefAssetData refAsset)
        {
            var refAssetPath = refAsset.path;
            var isSpriteInAtlas = refAsset.usedSubAssetTypes.Contains(typeof(Sprite)) && refAsset.usedSubAssetTypes.Count == 1;
            // Spriteとしてのみ参照されているテクスチャでSpriteAtlasに含まれている場合はSpriteAtlasとして判定する
            if (isSpriteInAtlas)
            {
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(refAsset.path);
                foreach (var atlas in analyzeCache.spriteAtlases)
                {
                    if (atlas.instance.CanBindTo(sprite))
                    {
                        refAssetPath = AssetDatabase.GetAssetPath(atlas.instance);
                        break;
                    }
                }
            }

            ret.Clear();
            var entryCount = analyzeCache.explicitEntries.Count;
            for (var i = 0; i < entryCount; ++i)
            {
                var entry = analyzeCache.explicitEntries[i];
                
                EditorUtility.DisplayCancelableProgressBar("Searching Referring Entries...", refAsset.path, (float)i/entryCount);
                //var path = AssetDatabase.GUIDToAssetPath(entry.guid);
                var dependencyPaths = AssetDatabase.GetDependencies(entry.AssetPath, true);
                foreach (var depPath in dependencyPaths)
                {
                    if (depPath != refAssetPath)
                        continue;
                    ret.Add(new RefEntry()
                    {
                        groupPath = entry.parentGroup.name,
                        assetPath = entry.AssetPath,
                    });
                    break;
                }
            }
            EditorUtility.ClearProgressBar();

            if (ret.Count == 0)
            {
                if (isSpriteInAtlas)
                {
                    // SpriteAtlasが重複しているがSpriteAtlasがEntryにないケースは暗黙アセットであるSpriteAtlasを警告する
                    // 暗黙アセットであるSpriteAtlasを参照しているEntryを検出すると、
                    // プロジェクトによっては多数リストアップされ、本質的に何が重複アセットなのかわからなくなる懸念がある
                    ret.Add(new RefEntry()
                    {
                        groupPath = null,
                        assetPath = refAssetPath,
                    });
                }
                else
                {
                    Debug.LogError($"Unknown error, not found referenced AddressableEntry {refAsset.path}");
                }
            }
            
            ret.Sort(CompareName);
        }
        
        // sort by alphanumeric
        static readonly System.Text.RegularExpressions.Regex NUM_REGEX = new (@"[^0-9]");
        static int CompareName(string a, string b)
        {
            var ret = string.CompareOrdinal(a, b);
            // 桁数の違う数字を揃える
            var regA = NUM_REGEX.Replace(a, string.Empty);
            var regB = NUM_REGEX.Replace(b, string.Empty);
            if ((regA.Length > 0 && regB.Length > 0) && regA.Length != regB.Length)
            {
                if (ret > 0 && regA.Length < regB.Length)
                    return -1;
                else if (ret < 0 && regA.Length > regB.Length)
                    return 1;
            }

            return ret;
        }
        static int CompareName(RefAssetData a, RefAssetData b) => CompareName(a.path, b.path);
        static int CompareName(RefEntry a, RefEntry b) => CompareName(a.assetPath, b.assetPath);
    }
}