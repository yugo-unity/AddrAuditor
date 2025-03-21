using System.Collections.Generic;
using UnityEngine.UIElements;
using UnityEditor;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

namespace AddrAuditor.Editor
{
    internal class AnalyzeCache
    {
        //public AddressableAssetSettings addrSetting;
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
    /// Subカテゴリインスタンス
    /// </summary>
    internal abstract class SubCategoryView
    {
        public VisualElement rootElement { get; private set; }
        public virtual bool requireAnalyzeCache => false;

        public void CreateView(TwoPaneSplitViewOrientation orientation)
        {
            var dimension = orientation == TwoPaneSplitViewOrientation.Horizontal ? 500 : 70;
            var root = new TwoPaneSplitView(0, dimension, orientation);
            this.rootElement = root;
            this.OnCreateView();
            this.UpdateView(); // 初回更新
        }

        /// <summary>
        /// 解析処理
        /// </summary>
        public abstract void Analyze(AnalyzeCache cache);

        /// <summary>
        /// 固有Viewの生成
        /// </summary>
        /// <returns>AnalyzeCacheの要求</returns>
        protected abstract void OnCreateView();

        /// <summary>
        /// 表示の更新
        /// カテゴリが選択された時に呼ばれる
        /// </summary>
        public abstract void UpdateView();

        /// <summary>
        /// 
        /// </summary>
        /// <param name="refEntries"></param>
        /// <param name="allEntries"></param>
        /// <param name="refAsset"></param>
        protected static void FindReferencedEntries(List<RefEntry> refEntries, AnalyzeCache analyzeCache, RefAssetData refAsset)
        {
            var refAssetPath = refAsset.path;
            var isSpriteInAtlas = refAsset.usedSubAssetTypes.Contains(typeof(Sprite)) && refAsset.usedSubAssetTypes.Count == 1;
            // Spriteとしてのみ参照されているテクスチャでSpriteAtlasに含まれている場合はSpriteAtlasとして判定する
            if (isSpriteInAtlas)
            {
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(refAsset.path);
                //var packed = false;
                foreach (var atlas in analyzeCache.spriteAtlases)
                {
                    // AFAIK no way to find SpriteAtlas contains a Sprite before instancing
                    if (atlas.instance.CanBindTo(sprite))
                    {
                        refAssetPath = AssetDatabase.GetAssetPath(atlas.instance);
                        break;
                    }
                }
            }

            refEntries.Clear();
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
                    refEntries.Add(new RefEntry()
                    {
                        groupPath = entry.parentGroup.name,
                        assetPath = entry.AssetPath,
                    });
                    break;
                }
            }
            EditorUtility.ClearProgressBar();

            if (refEntries.Count == 0)
            {
                if (isSpriteInAtlas)
                {
                    // SpriteAtlasが重複しているがSpriteAtlasがEntryにないケースは暗黙アセットであるSpriteAtlasを警告する
                    // 暗黙アセットであるSpriteAtlasを参照しているEntryを検出すると、
                    // プロジェクトによっては多数リストアップされ、本質的に何が重複アセットなのかわからなくなる懸念がある
                    refEntries.Add(new RefEntry()
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
        }
        
        static readonly System.Text.RegularExpressions.Regex NUM_REGEX = new (@"[^0-9]");
        /// <summary>
        /// alphanumericソート
        /// </summary>
        protected static int CompareName(RefAssetData aParam, RefAssetData bParam)
        {
            var a = aParam.path;
            var b = bParam.path;
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
    }
}