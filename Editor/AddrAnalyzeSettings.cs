using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace AddrAuditor.Editor
{
    internal partial class AddrAnalyzeWindow
    {
        static readonly List<string> AA_CATEGORIES = new ()
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

        class AnalyzeViewAddrSetting : SubCategoryView
        {
            public override void OnSelectedChanged(IEnumerable<int> selectedItems)
            {
                var index = selectedItems.First();
                this.detailsLabel.text = AA_DETAILS_JA[index];
                this.recommendationLabel.text = AA_RECOMMENDS_JA[index];
            }

            public override void UpdateView()
            {
                this.treeList.itemsSource = AA_CATEGORIES;
                for (var i = 0; i < AA_CATEGORIES.Count; i++)
                {
                    var tree = new TreeView();
                    var assets = CreateTargetAssets(i.ToString());
                    tree.SetRootItems(assets);
                }
                this.treeList.Rebuild();
            }
        }
    }
}
