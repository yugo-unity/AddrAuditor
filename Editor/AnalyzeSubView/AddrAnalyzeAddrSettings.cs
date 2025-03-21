using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor;

namespace AddrAuditor.Editor
{
    class AnalyzeViewAddrSetting : SubCategoryView
    {
        static readonly List<string> AA_CATEGORIES = new()
        {
            "   Log Runtime Exception",
            "   Enabled Json Catalog",
            "   Internal Asset Naming Mode",
            "   Internal Bundle Id Mode",
            "   Asset Load Mode",
            "   Unique Bundle IDs",
            "   Contiguous Bundles",
            "   Non-Recursive Dependency Calculation",
            "   Strip Unity Version from AssetBundles",
            "   Disable Visible Sub Asset Representations",
        };

        // TODO: purge to json file or csv or anything
        static readonly string[] AA_DETAILS_JA = new[]
        {
            "読み込みに失敗した際の例外処理はローカルアセットのみを考慮する場合不要です。",
            "カタログファイルを以前のjson式に戻します。下位互換やデバッグ用途で使用します。",
            "Bundle内のアセットをロードする際のID設定です。",
            "Groupのエントリに変更があった際のAssetBundleのIDが変更による依存元のAssetBundleに差分を回避するための設定です。",
            "AssetBundleの中身をリクエストに必要なもののみロードするか一括でロードするかの設定です。",
            "Content Updateの為の設定です。",
            "AssetBundle内のアセットをソートします。複雑な階層を持つPrefab等でのロード時間の改善が見込めます。",
            "アセットの依存関係を非再帰的に検出しビルド時間とランタイムメモリの削減を行います。",
            "AssetBundleのヘッダチャンクとSerialziedData内にあるUnity versionの値をゼロクリアします。",
            "SubAssetの情報を削除しSubAssetが多いアセットに対してビルド時間の削減が見込めます。",
        };

        // TODO: purge to json file or csv or anything
        static readonly string[] AA_RECOMMENDS_JA = new[]
        {
            "ローカルのみの場合は通常不要です。",
            "通常は無効としてバイナリ式の方がベターです。",
            "DynamicはGUIDを2byteに圧縮したもので、カタログサイズを削減できるので推奨されます。" +
            "ただし同一グループ内にアセット数が非常に多く問題が起きる場合はGUIDを利用してください。" +
            "またSceneが含まれるGroupに対してDynamicを適用すると" +
            "SceneManagerにおいてScene名で検出が出来なくなることに注意してください。",
            "Group Guidが推奨されます。ビルドマシン等を利用する際はGroup Guid Project Id Hashを使用することで" +
            "同一のプロジェクトで競合することを避けられます",
            "通常Requested Asset And Dependenciesのままで問題ありませんが、" +
            "PlatformによってはAll Packed Assets And Dependenciesとして一括ロードした方が" +
            "総合的にロード時間の短縮となるケースがあります。グループ構成によって設定を使い分けると良いでしょう。",
            "無効にしてください。有効にするとAssetBundleのIDがユニークになりビルドが決定的ではなくなります。",
            "有効にしてください。このオプションは下位互換のために無効とすることができます。",
            "有効にしてください。このオプションは下位互換のために無効とすることができます。" +
            "本設定が有効の場合、MonoScriptの循環参照が解決できないのでMonoScript Bundleとの併用が推奨されます。",
            "有効にしてください。ローカル専用としてAssetBundleを扱う場合不要なデータであり、余計な差分を発生する要因となります。" +
            "なおUnityバージョンをまたいでのAssetBundle利用は動作を保証されていないことに注意してください。",
            "有効にした場合、SubAssetを指定してのロードができなくなる制限が発生します。" +
            "主としてSpriteAtlas内のSprite指定、fbx内のMeshやAnimation指定のロードができなくなります。" +
            "プロジェクトの状況をみて有効にできそうであればするとベターでしょう。",
        };
        
        ListView listView;
        VisualElement optionalView;
        Label detailsLabel; // カテゴリの説明文
        Label recommendationLabel; // サブカテゴリに対する推奨説明（個別に変わらない限りDetailで行う） 

        void OnSelectedChanged(IEnumerable<int> selectedItems)
        {
            var index = selectedItems.First();
            this.detailsLabel.text = AA_DETAILS_JA[index];
            this.recommendationLabel.text = AA_RECOMMENDS_JA[index];
        }

        /// <summary>
        /// 解析処理
        /// </summary>
        public override void Analyze(AnalyzeCache cache)
        {
            var settingsPath = $"Assets/{nameof(AddrAutoGroupingSettings)}.asset";
            var groupingSettings = AssetDatabase.LoadAssetAtPath<AddrAutoGroupingSettings>(settingsPath);
        }

        /// <summary>
        /// GUI構築
        /// </summary>
        protected override void OnCreateView()
        {
            this.listView = new ListView();
            this.listView.selectedIndicesChanged += this.OnSelectedChanged;
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
                {
                    if (this.listView.itemsSource is List<string> list)
                        label.text = list[index];
                }
            };
            this.rootElement.Add(this.listView);
            
            var descriptionView = new TwoPaneSplitView(0, 200, TwoPaneSplitViewOrientation.Vertical);
            {
                var box = new VisualElement();
                var header = new Label("Details");
                header.style.unityFontStyleAndWeight = FontStyle.Bold;
                box.Add(header);
                this.detailsLabel = new Label("explain what is setting");
                this.detailsLabel.name = "itemExplanation";
                this.detailsLabel.style.whiteSpace = WhiteSpace.Normal;
                box.Add(this.detailsLabel);
                foreach (var child in box.Children())
                    child.style.left = 10f;
                descriptionView.Add(box);

                box = new VisualElement();
                header = new Label("Recommendation");
                header.style.unityFontStyleAndWeight = FontStyle.Bold;
                box.Add(header);
                this.recommendationLabel = new Label("About recommended setting");
                this.recommendationLabel.style.whiteSpace = WhiteSpace.Normal;
                box.Add(this.recommendationLabel);
                foreach (var child in box.Children())
                    child.style.left = 10f;
                descriptionView.Add(box);
            }
            this.optionalView = descriptionView;
            this.rootElement.Add(this.optionalView);
        }

        public override void UpdateView()
        {
            this.listView.itemsSource = AA_CATEGORIES;
            // for (var i = 0; i < AA_CATEGORIES.Count; i++)
            // {
            //     var tree = new TreeView();
            //     var assets = CreateTargetAssets(i.ToString());
            //     tree.SetRootItems(assets);
            // }
            this.listView.Rebuild();
        }
    }
}
