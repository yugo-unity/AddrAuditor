using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace AddrAuditor.Editor
{
    class AnalyzeViewGroupSetting : ResultView
    {
        static readonly List<string> AG_CATEGORIES = new()
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
        
        ListView listView;
        VisualElement optionalView;
        Label detailsLabel; // カテゴリの説明文
        Label recommendationLabel; // サブカテゴリに対する推奨説明（個別に変わらない限りDetailで行う） 

        void OnSelectedChanged(IEnumerable<int> selectedItems)
        {
            var index = selectedItems.First();
        }

        /// <summary>
        /// 解析処理
        /// </summary>
        public override void Analyze(AnalyzeCache cache)
        {
            
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
            this.listView.itemsSource = AG_CATEGORIES;
            for (int i = 0; i < AG_CATEGORIES.Count; i++)
            {
                var tree = new TreeView();
                var assets = CreateTargetAssets(i.ToString());
                tree.SetRootItems(assets);
            }

            this.listView.Rebuild();
        }
        
        /// <summary>
        /// 対象のアセット一覧
        /// </summary>
        /// <param name="assetPath">対象のアセットパス</param>
        /// <returns>項目一覧</returns>
        static List<TreeViewItemData<string>> CreateTargetAssets(string assetPath)
        {
            var subViewList = new List<TreeViewItemData<string>>(10);
            for (var j = 0; j < 10; j++)
                subViewList.Add(new TreeViewItemData<string>(j + 1, (j+1).ToString()));
            return subViewList;
        }
    }
}
