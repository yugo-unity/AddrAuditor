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
            "読み込みに失敗した際の例外処理はローカルアセットのみを考慮する場合は不要ですので無効にできます。\n" +
            "\n" +
            "You can disable this option.\n" +
            "No need to consider handling exceptions in the event of a failed load for local assets.",
            // JSON_CATALOG
            "Catalogファイルをjsonで出力します。\n" +
            "通常は設定を無効とし、binaryで扱った方がファイルサイズが小さくロード時間が短いです。\n" +
            "本設定は後方互換と開発用の為に存在します。\n" +
            "\n" +
            "The Catalog file is output in JSON format. " +
            "Normally, disabling this and outputting in binary format will result in a smaller file size and shorter load times.\n" +
            "This setting exists for backward compatibility and development purposes.",
            // INTERNAL_ASSET_NAMING
            "bundle内のアセットをロードする際のID設定です。\n" +
            "DynamicはGUIDを2byteに圧縮しカタログサイズを削減できるので推奨されます。\n" +
            "ただし同一グループ内にアセット数が非常に多く衝突が起きる場合はGUIDを利用してください。\n" +
            "またSceneが含まれるGroupに対してDynamicを適用すると、\n" +
            "SceneManagerにおいてScene名で検出が出来なくなることに注意してください。\n" +
            "\n" +
            "This is the ID setting when loading assets in a bundle.\n" +
            "Dynamic is recommended, it compresses GUIDs into 2 bytes to reduce catalog size.\n" +
            "However, if large number of assets in the same group and conflict with ID, use GUIDs.\n" +
            "Also, be careful that if you apply Dynamic to a group that contains Scenes.\n" +
            "You will no longer be able to detect scenes by Scene name from SceneManager.",
            // INTERNAL_BUNDLE_ID
            "Groupのエントリに変更があった際、AssetBundleのIDが変更されることによって、" +
            "依存元のAssetBundleに差分が発生するのを回避するための設定です。\n" +
            "通常Group Guidが推奨されます。\n" +
            "ビルドマシン等を利用する際はGroup Guid Project Id Hashを使用することで" +
            "同一のプロジェクトで競合することを避けられます。\n" +
            "\n" +
            "This setting is used to avoid differences occurring in the AssetBundle of the dependent source\n" +
            "when there are changes to the Group entry, by changing the AssetBundle ID.\n" +
            "The Group Guid is usually recommended.\n" +
            "If using a build machine, using the Group Guid Project Id Hash avoids conflicts in the same project.",
            // ASSET_LOAD_MODE
            "AssetBundleの中身をリクエストに必要なもののみロードするか一括でロードするかの設定です。\n" +
            "通常Requested Asset And Dependenciesのままで問題ありませんが、" +
            "PlatformによってはAll Packed Assets And Dependenciesとして一括ロードした方が" +
            "総合的にロード時間の短縮となるケースがあります。\n" +
            "\n" +
            "This setting determines whether to load only the contents of the AssetBundle that are required for the request, or to load everything at once.\n" +
            "Normally, it is fine to leave this set to “Requested Asset And Dependencies”. " +
            "But depending on the platform, it may be better to load everything at once as “All Packed Assets And Dependencies” to reduce the load time.",
            // UNIQUE_BUNDLE_IDS
            "Content Updateの為の設定ですので無効にしてください。\n" +
            "有効にするとAssetBundleのIDがユニークになりビルドがdeterministicではなくなります。\n" +
            "\n" +
            "This is a setting for content updates. Please disable this option.\n" +
            "If this is enabled, the AssetBundle ID will become unique and the build will not be deterministic.",
            // CONTIGUOUS_BUNDLES
            "AssetBundle内のアセットをソートします。複雑な階層を持つPrefab等でのロード時間の改善が見込めます。\n" +
            "有効にしてください。このオプションは下位互換のために無効とすることができます。\n" +
            "\n" +
            "Sorts the assets in AssetBundles. This can be expected to improve load times for prefabs with complex hierarchies. " +
            "Please enable this option. This can be disabled for backward compatibility.",
            // NON_RECURSIVE_DEPENDENCY
            "アセットの依存関係を非再帰的に検出しビルド時間とランタイムメモリの削減を行います。\n" +
            "有効にしてください。このオプションは下位互換のために無効とすることができます。\n" +
            "\n" +
            "Detecting asset dependencies with non-recursive and reducing build time and runtime memory.\n" +
            "Please enable this option. This can be disabled for backward compatibility.",
            // STRIP_UNITY_VERSION
            "AssetBundleのヘッダチャンクとSerialized Data内にあるUnity versionの値をゼロクリアします。\n" +
            "有効にしてください。ローカル専用としてAssetBundleを扱う場合不要なデータです。\n" +
            "なおUnityバージョンをまたいでのAssetBundle利用は動作を保証されていないことに注意してください。\n" +
            "\n" +
            "Unity version at header chunk and Serialized Data in AssetBundle are zero-cleared.\n" +
            "Please enable this option. These are unnecessary when using AssetBundle for local only.\n" +
            "NOTE: that using AssetBundles across Unity versions is not supported. ",
            // DISABLE_VISIBLE_SUB_ASSET
            "SubAssetの情報を削除しSubAssetが多いアセットに対してビルド時間の削減が見込めます。\n" +
            "有効にした場合、SubAssetを指定してのロードができなくなる制限が発生します。\n" +
            "主としてSpriteAtlas内のSprite指定、fbx内のMeshやAnimation指定のロードができなくなります。\n" +
            "プロジェクトの状況をみて有効にできそうであればするとベターです。\n" +
            "\n" +
            "Deleting SubAsset information can reduce build times for assets who has many SubAssets.\n" +
            "If enabled, there is restrictions that will prevent loading by specifying SubAsset.\n" + 
            "For example, it prevents loading of Sprites in SpriteAtlas and Meshes and Animations in fbx.\n" + 
            "It is better to consider to be enabled if it will be useful or not for your project.",
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
            
            // focusing in Project Window
            Selection.activeObject = this.settings;
            EditorGUIUtility.PingObject(this.settings);
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
                this.recommendationLabel.style.left = 10f;
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
