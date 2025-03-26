using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEditor;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;

namespace AddrAuditor.Editor
{
    class AnalyzeViewAddrSetting : ResultView
    {
        enum ANALYZED_ITEM
        {
            LOG_RUNTIME_EXCEPTION,
            JSON_CATALOG,
            INTERNAL_ASSET_NAMING,
            INTERNAL_BUNDLE_ID,
            ASSET_LOAD_MODE,
            UNIQUE_BUNDLE_IDS,
            CONTIGUOUS_BUNDLES,
            NON_RECURSIVE_DEPENDENCY,
            STRIP_UNITY_VERSION,
            DISABLE_VISIBLE_SUB_ASSET,
            
            MAX
        }
        
        static readonly string[] ITEM_NAME = new string[(int)ANALYZED_ITEM.MAX]
        {
            "Log Runtime Exception",
            "Enable Json Catalog",
            "Internal Asset Naming Mode",
            "Internal Bundle Id Mode",
            "Asset Load Mode",
            "Unique Bundle IDs",
            "Contiguous Bundles",
            "Non-Recursive Dependency Calculation",
            "Strip Unity Version from AssetBundles",
            "Disable Visible Sub Asset Representations",
        };

        static readonly string[] ITEM_DETAILS = new string[(int)ANALYZED_ITEM.MAX]
        {
            // LOG_RUNTIME_EXCEPTION
            "読み込みに失敗した際の例外処理はローカルアセットのみを考慮する場合は不要ですので無効にできます。",
            // JSON_CATALOG
            "Catalogファイルをjsonで出力します。通常無効にしてbinaryで出力する方がファイルサイズが小さく、ロード時間が短いです。\n" +
            "本設定は後方互換と開発用の為に存在します。",
            // INTERNAL_ASSET_NAMING
            "bundle内のアセットをロードする際のID設定です。DynamicはGUIDを2byteに圧縮しカタログサイズを削減できるので推奨されます。\n" +
            "ただし同一グループ内にアセット数が非常に多く衝突が起きる場合はGUIDを利用してください。\n" +
            "またSceneが含まれるGroupに対してDynamicを適用するとSceneManagerにおいてScene名で検出が出来なくなることに注意してください。",
            // INTERNAL_BUNDLE_ID
            "Groupのエントリに変更があった際、AssetBundleのIDが変更されることによって依存元のAssetBundleに差分が発生するのを回避するための設定です。\n" +
            "通常Group Guidが推奨されます。ビルドマシン等を利用する際はGroup Guid Project Id Hashを使用することで" +
            "同一のプロジェクトで競合することを避けられます",
            // ASSET_LOAD_MODE
            "AssetBundleの中身をリクエストに必要なもののみロードするか一括でロードするかの設定です。\n" +
            "通常Requested Asset And Dependenciesのままで問題ありませんが、" +
            "PlatformによってはAll Packed Assets And Dependenciesとして一括ロードした方が" +
            "総合的にロード時間の短縮となるケースがあります。グループ構成によって設定を使い分けるとベターです。",
            // UNIQUE_BUNDLE_IDS
            "Content Updateの為の設定ですので無効にしてください。\n" +
            "有効にするとAssetBundleのIDがユニークになりビルドがdeterministicではなくなります。",
            // CONTIGUOUS_BUNDLES
            "AssetBundle内のアセットをソートします。複雑な階層を持つPrefab等でのロード時間の改善が見込めます。\n" +
            "有効にしてください。このオプションは下位互換のために無効とすることができます。",
            // NON_RECURSIVE_DEPENDENCY
            "アセットの依存関係を非再帰的に検出しビルド時間とランタイムメモリの削減を行います。\n" +
            "有効にしてください。このオプションは下位互換のために無効とすることができます。",
            // STRIP_UNITY_VERSION
            "AssetBundleのヘッダチャンクとSerialziedData内にあるUnity versionの値をゼロクリアします。\n" +
            "有効にしてください。ローカル専用としてAssetBundleを扱う場合不要なデータであり、余計な差分を発生する要因となります。\n" +
            "なおUnityバージョンをまたいでのAssetBundle利用は動作を保証されていないことに注意してください。",
            // DISABLE_VISIBLE_SUB_ASSET
            "SubAssetの情報を削除しSubAssetが多いアセットに対してビルド時間の削減が見込めます。\n" +
            "有効にした場合、SubAssetを指定してのロードができなくなる制限が発生します。\n" +
            "主としてSpriteAtlas内のSprite指定、fbx内のMeshやAnimation指定のロードができなくなります。\n" +
            "プロジェクトの状況をみて有効にできそうであればするとベターです。",
        };

        struct RecommendItem
        {
            public string category;
            public string recommend;

            public RecommendItem(ANALYZED_ITEM item)
            {
                this.category = ITEM_NAME[(int)item];
                this.recommend = ITEM_DETAILS[(int)item];
            }
        }

        ListView listView;
        Label recommendationLabel;
        readonly List<RecommendItem> recommendItems = new();
        AddressableAssetSettings settings;

        void OnSelectedChanged(IEnumerable<int> selectedItems)
        {
            if (selectedItems is not List<int> indexList || indexList.Count == 0)
                return;
            var index = indexList[0];
            this.recommendationLabel.text = this.recommendItems[index].recommend;
        }

        /// <summary>
        /// called when require to analyze
        /// </summary>
        /// <param name="cache">build cache that created by AddrAnalyzeWindow</param>
        public override void Analyze(AnalyzeCache cache)
        {
            this.listView.ClearSelection();
            this.recommendItems.Clear();
            this.recommendationLabel.text = "";
            
            this.settings = cache.addrSetting;
            var serializedObject = new SerializedObject(this.settings);
            var stripUnityVersionProp = serializedObject.FindProperty("m_StripUnityVersionFromBundleBuild");
            if (this.settings.buildSettings.LogResourceManagerExceptions)
                this.recommendItems.Add(new RecommendItem(ANALYZED_ITEM.LOG_RUNTIME_EXCEPTION));
            if (this.settings.EnableJsonCatalog)
                this.recommendItems.Add(new RecommendItem(ANALYZED_ITEM.JSON_CATALOG));
            if (this.settings.InternalIdNamingMode != BundledAssetGroupSchema.AssetNamingMode.Dynamic)
                this.recommendItems.Add(new RecommendItem(ANALYZED_ITEM.INTERNAL_ASSET_NAMING));
            if (this.settings.InternalBundleIdMode != BundledAssetGroupSchema.BundleInternalIdMode.GroupGuid)
                this.recommendItems.Add(new RecommendItem(ANALYZED_ITEM.INTERNAL_BUNDLE_ID));
            if (this.settings.AssetLoadMode != AssetLoadMode.AllPackedAssetsAndDependencies)
                this.recommendItems.Add(new RecommendItem(ANALYZED_ITEM.ASSET_LOAD_MODE));
            if (this.settings.UniqueBundleIds)
                this.recommendItems.Add(new RecommendItem(ANALYZED_ITEM.UNIQUE_BUNDLE_IDS));
            if (!this.settings.ContiguousBundles)
                this.recommendItems.Add(new RecommendItem(ANALYZED_ITEM.CONTIGUOUS_BUNDLES));
            if (!this.settings.NonRecursiveBuilding)
                this.recommendItems.Add(new RecommendItem(ANALYZED_ITEM.NON_RECURSIVE_DEPENDENCY));
            if (!stripUnityVersionProp.boolValue)
                this.recommendItems.Add(new RecommendItem(ANALYZED_ITEM.STRIP_UNITY_VERSION));
            if (!this.settings.DisableVisibleSubAssetRepresentations)
                this.recommendItems.Add(new RecommendItem(ANALYZED_ITEM.DISABLE_VISIBLE_SUB_ASSET));
            
            this.listView.itemsSource = this.recommendItems;
        }

        /// <summary>
        /// called when created view (only once)
        /// </summary>
        protected override void OnCreateView()
        {
            this.listView = new ListView();
            this.listView.selectedIndicesChanged += this.OnSelectedChanged;
            this.listView.itemsChosen += chosenItems =>
            {
                if (!chosenItems.Any())
                    return;
                // focusing in Project Window
                Selection.activeObject = this.settings;
                EditorGUIUtility.PingObject(this.settings);
            };
            this.listView.selectionType = SelectionType.Single;
            this.listView.makeItem = () =>
            {
                var label = new Label();
                label.style.unityTextAlign = TextAnchor.MiddleLeft;
                return label;
            };
            this.listView.bindItem = (element, index) =>
            {
                if (element is Label label)
                    label.text = $"   {this.recommendItems[index].category}";
            };
            this.rootElement.Add(this.listView);
            
            var box = new Box();
            {
                var header = new Label("Details");
                header.style.unityFontStyleAndWeight = FontStyle.Bold;
                header.style.left = 10f;
                box.Add(header);
                this.recommendationLabel = new Label();
                this.recommendationLabel.style.whiteSpace = WhiteSpace.Normal;
                box.Add(this.recommendationLabel);
            }
            this.rootElement.Add(box);
        }

        /// <summary>
        /// called when selecting any category
        /// </summary>
        public override void UpdateView()
        {
            this.listView.itemsSource = this.recommendItems;
            this.listView.Rebuild();
        }
    }
}
