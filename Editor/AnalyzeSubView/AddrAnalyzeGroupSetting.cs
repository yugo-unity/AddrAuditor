using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;

namespace AddrAuditor.Editor
{
    class AnalyzeViewGroupSetting : ResultView
    {
        enum ANALYZED_ITEM
        {
            USE_DEFAULT,
            BUNDLE_COMPRESSION,
            BUNDLE_CRC,
            BUNDLE_NAMING,
            INCLUDE_IN_CATALOG,
            INTERNAL_ASSET_NAMING,
            BUNDLE_MODE,
            INCLUDE_IN_BUILD,
            
            MAX
        }
        
        static readonly string[] ITEM_NAME = new string[(int)ANALYZED_ITEM.MAX]
        {
            "Use Default",
            "Asset Bundle Compression",
            "Asset Bundle CRC",
            "Bundle Naming Mode",
            "Include XXX in Catalog",
            "Internal Asset Naming Mode", // 親の設定を継承してDynamic推奨だがSceneの場合のみ注意喚起
            "Bundle Mode",

            "Inclue in Build", // 無効の時にビルドに含まれないのは正しいがの注意喚起
        };
        
        // TODO: purge to json file or csv or anything, and support English
        static readonly string[] ITEM_RECOMMEND = new string[(int)ANALYZED_ITEM.MAX]
        {
            // USE_DEFAULT
            "Disableにしてください。Use Defaultはリモートアセットを意識した設定となっておりコンソールプラットフォームでは不適切な値となります。",
            // BUNDLE_COMPRESSION
            "ローカルアセットのみを考慮する場合、通常Uncompressedが推奨されます。PCはLZ4でも構いません。\n" +
            "プラットフォームによってはROM圧縮機能の併用を考慮しますが、DLCに関してはLZ4圧縮を検討する必要があるかもしれません。",
            // BUNDLE_CRC
            "CRCチェックはAssetBundleダウンロード時の破損を判定することが主目的であり、ローカルアセットのみを考慮する場合は不要です。\n" +
            "有効であると判定のためにロード時間が著しく延びることに注意してください。",
            // BUNDLE_NAMING
            "FileName、またはFileName Hashとしてください。AssetBundleのファイル名設定となります。\n" +
            "AppendやOnly Hashの際のHash値はAssetBundleの内容物から計算されるために変更があった場合に別ファイルとして扱われます。",
            // INCLUDE_IN_CATALOG
            "Addresses、GUIDsはどちらかのみ有効とする方が望ましいです。\n" +
            "Catalogファイルに書き込まれますが、両対応するとCatalogファイルの肥大化につながります。" +
            "GUIDsの場合はAssetReferenceからのロードがスムーズです。",
            // INTERNAL_ASSET_NAMING
            "AddressablesAssetSettingsのCatalog設定のところが共通設定となり通常Dynamicが推奨されますが、\n" +
            "Sceneアセットが含まれるGroupでは注意が必要です。" +
            "Dynamicの場合はSceneManagerクラスにおいてScene名の文字列でSceneクラスを取得することができなくなります。",
            // BUNDLE_MODE
            "Pack SeparatelyによってAssetBundleの数が著しく増えると、ビルド時間やロード時間のオーバーヘッドとなります。\n" +
            "なるべくPacked TogetherになるようGroupingを検討してください。\n" +
            "ただしAssetBundleのUnloadはbundle単位で行われますのでエントリが著しく多いGroupでのPacked Togetherには注意してください。",
            // INCLUDE_IN_BUILD
            "開発中のデバッグ用途やDLCのグループをROMビルドの際から除外する際に利用可能です。\n" +
            "意図せず無効となっていないか確認してください。",
        };

        struct RecommendItem
        {
            public AddressableAssetGroup group;
            public string category;
            public string recommend;

            public RecommendItem(AddressableAssetGroup group, ANALYZED_ITEM item)
            {
                this.group = group;
                this.category = ITEM_NAME[(int)item];
                this.recommend = ITEM_RECOMMEND[(int)item];
            }
        }

        ListView listView;
        Label recommendationLabel;
        readonly List<RecommendItem> recommendItems = new();

        void OnSelectedChanged(IEnumerable<int> selectedItems)
        {
            if (selectedItems is not List<int> indexList || indexList.Count == 0)
                return;
            var index = indexList[0];
            var item = this.recommendItems[index];
            this.recommendationLabel.text = $"{item.recommend}";
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
            
            var settings = cache.addrSetting;
            foreach (var group in settings.groups)
            {
                var schema = group.GetSchema<BundledAssetGroupSchema>();
                
                if (schema.UseDefaultSchemaSettings)
                    this.recommendItems.Add(new RecommendItem(group, ANALYZED_ITEM.USE_DEFAULT));
                if (schema.Compression != BundledAssetGroupSchema.BundleCompressionMode.Uncompressed)
                    this.recommendItems.Add(new RecommendItem(group, ANALYZED_ITEM.BUNDLE_COMPRESSION));
                if (schema.UseAssetBundleCrc)
                    this.recommendItems.Add(new RecommendItem(group, ANALYZED_ITEM.BUNDLE_CRC));
                if (schema.BundleNaming is BundledAssetGroupSchema.BundleNamingStyle.AppendHash or BundledAssetGroupSchema.BundleNamingStyle.OnlyHash)
                    this.recommendItems.Add(new RecommendItem(group, ANALYZED_ITEM.BUNDLE_NAMING));
                if (schema.IncludeAddressInCatalog && schema.IncludeGUIDInCatalog)
                    this.recommendItems.Add(new RecommendItem(group, ANALYZED_ITEM.INCLUDE_IN_CATALOG));
                if (group.entries.Select(entry => entry.AssetPath.Contains(".unity")).FirstOrDefault())
                {
                    if (schema.InternalIdNamingMode == BundledAssetGroupSchema.AssetNamingMode.Dynamic)
                        this.recommendItems.Add(new RecommendItem(group, ANALYZED_ITEM.INTERNAL_ASSET_NAMING));
                }
                if (schema.BundleMode == BundledAssetGroupSchema.BundlePackingMode.PackSeparately)
                {
                    if (!group.name.Contains("+Shared_Single"))
                        this.recommendItems.Add(new RecommendItem(group, ANALYZED_ITEM.BUNDLE_MODE));
                }
                if (!schema.IncludeInBuild)
                    this.recommendItems.Add(new RecommendItem(group, ANALYZED_ITEM.INCLUDE_IN_BUILD));
            }

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
                if (chosenItems.First() is RecommendItem item)
                {
                    // focusing in Project Window
                    var obj = item.group;
                    Selection.activeObject = obj;
                    EditorGUIUtility.PingObject(obj);
                }
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
                var item = this.recommendItems[index];
                if (element is Label label)
                    label.text = $"   {item.group.name} > {item.category}";
            };
            this.rootElement.Add(this.listView);
            
            var box = new VisualElement();
            var header = new Label("Recommendation");
            header.style.unityFontStyleAndWeight = FontStyle.Bold;
            box.Add(header);
            this.recommendationLabel = new Label();
            this.recommendationLabel.style.whiteSpace = WhiteSpace.Normal;
            box.Add(this.recommendationLabel);
            foreach (var child in box.Children())
                child.style.left = 10f;
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
