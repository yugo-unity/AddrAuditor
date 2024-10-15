using UnityEditor;
using UnityEngine.UIElements;
using System.Collections.Generic;

namespace AddrAuditor.Editor
{
    internal partial class AddrAnalyzeWindow
    {
        public static void Analyzing()
        {
            var settingsPath = $"Assets/{nameof(AddrAutoGroupingSettings)}.asset";
            var groupingSettings = AssetDatabase.LoadAssetAtPath<AddrAutoGroupingSettings>(settingsPath);
        }

        static List<TreeViewItemData<string>> CreateAddrAssetList(int index)
        {
            var subViewList = new List<TreeViewItemData<string>>(10);
            for (var j = 0; j < 10; j++)
                subViewList.Add(new TreeViewItemData<string>(j + 1, (j+1).ToString()));
            //var subView = new TreeViewItemData<string>(index, "Enabled Json Catalog", subViewList);
            return subViewList;
        }
        
        static readonly List<string> AA_CATEGORIES = new ()
        {
            "   Disable Log Runtime Exception", // 例外処理の無効化
            "   Enabled Json Catalog",
            "   Internal Asset Naming Mode",
            "   Internal Bundle Id Mode",
            "   Asset Load Mode",
            "   Asset Provider",
            "   AssetBundle Provider",
            "   Unique Bundle IDs",
            "   Contiguous Bundles",
            "   Non-Recursive Dependency Calculation",
            "   Strip Unity Version from AssetBundles",
            "   Disable Visible Sub Asset Representations",
        };

        class AnalyzeViewAddrSetting : SubCategoryView
        {
            public override void UpdateView()
            {
                this.treeList.itemsSource = AA_CATEGORIES;
                for (int i = 0; i < AA_CATEGORIES.Count; i++)
                {
                    var tree = new TreeView();
                    var assets = CreateAddrAssetList(i);
                    tree.SetRootItems(assets);
                    //this.treeList.Add(tree);
                }
                this.treeList.Rebuild();
            }
        }
        
        static readonly List<string> AG_CATEGORIES = new ()
        {
            "   Use Default",
            "   Asset Bundle Compression",
            "   Asset Bundle CRC",
            "   Bundle Naming Mode",
            "   Inclde XXX in Catalog",
            "   Internal Asset Naming Mode", // 親の設定を継承してDynamic推奨だがSceneの場合のみ注意喚起
            "   Bundle Mode",
            
            "   Inclue in Build", // 無効の時にビルドに含まれないのは正しいがの注意喚起
        };
        class AnalyzeViewGroupSetting : SubCategoryView
        {
            public override void UpdateView()
            {
                this.treeList.itemsSource = AG_CATEGORIES;
                for (int i = 0; i < AA_CATEGORIES.Count; i++)
                {
                    var tree = new TreeView();
                    var assets = CreateAddrAssetList(i);
                    tree.SetRootItems(assets);
                    //this.treeList.Add(tree);
                }
                this.treeList.Rebuild();
            }
        }
    }
}
