using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Linq;

namespace AddrAuditor.Editor
{
    internal partial class AddrAnalyzeWindow
    {
        static readonly List<string> AG_CATEGORIES = new ()
        {
            "   Use Default",
            "   Asset Bundle Compression",
            "   Asset Bundle CRC",
            "   Bundle Naming Mode",
            "   Include XXX in Catalog",
            "   Internal Asset Naming Mode", // 親の設定を継承してDynamic推奨だがSceneの場合のみ注意喚起
            "   Bundle Mode",
            
            "   Inclue in Build", // 無効の時にビルドに含まれないのは正しいがの注意喚起
        };
        class AnalyzeViewGroupSetting : SubCategoryView
        {
            public override void OnSelectedChanged(IEnumerable<int> selectedItems)
            {
                var index = selectedItems.First();
            }
            
            public override void UpdateView()
            {
                this.treeList.itemsSource = AG_CATEGORIES;
                for (int i = 0; i < AA_CATEGORIES.Count; i++)
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
