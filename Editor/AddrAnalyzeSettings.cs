using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Linq;

namespace AddrAuditor.Editor
{
    internal partial class AddrAnalyzeWindow
    {
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
            public override void OnSelectedChanged(IEnumerable<int> selectedItems)
            {
                var index = selectedItems.First();
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
