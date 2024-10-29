using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;

namespace AddrAuditor.Editor
{
    internal partial class AddrAnalyzeWindow
    {
        /// <summary>
        /// Subカテゴリインスタンス
        /// </summary>
        abstract class SubCategoryView
        {
            public VisualElement root;
            public ListView treeList;
            public Label detailsLabel;
            public Label recommendationLabel;

            public abstract void UpdateView();
            public abstract void OnSelectedChanged(IEnumerable<int> selectedItems);
        }

        /// <summary>
        /// RightPaneの共通生成処理
        /// </summary>
        /// <typeparam name="T">SubCategory固有のクラス</typeparam>
        /// <returns>SubCategory管理クラス</returns>
        static T CreateSubCategoryView<T>() where T : SubCategoryView, new()
        {
            var view = new T();
            var subSplitView = new TwoPaneSplitView(0, 300, TwoPaneSplitViewOrientation.Horizontal);

            var treeList = new ListView();
            treeList.makeItem = () =>
            {
                var label = new Label();
                label.style.unityTextAlign = TextAnchor.MiddleLeft;
                return label;
            };
            treeList.bindItem = (element, index) =>
            {
                if (element is Label label)
                {
                    if (treeList.itemsSource is List<string> list)
                        label.text = list[index];
                }
            };
            treeList.selectedIndicesChanged += view.OnSelectedChanged;
            subSplitView.Add(treeList);
            view.treeList = treeList;

            var descriptionSplitView = new TwoPaneSplitView(0, 200, TwoPaneSplitViewOrientation.Vertical);
            var box = new VisualElement();
            var header = new Label("Details");
            header.style.unityFontStyleAndWeight = FontStyle.Bold;
            box.Add(header);
            view.detailsLabel = new Label("explain what is setting");
            view.detailsLabel.style.whiteSpace = WhiteSpace.Normal;
            box.Add(view.detailsLabel);
            foreach (var child in box.Children())
                child.style.left = 10f;
            descriptionSplitView.Add(box);

            box = new VisualElement();
            header = new Label("Recommendation");
            header.style.unityFontStyleAndWeight = FontStyle.Bold;
            box.Add(header);
            view.recommendationLabel = new Label("About recommended setting");
            view.recommendationLabel.style.whiteSpace = WhiteSpace.Normal;
            box.Add(view.recommendationLabel);
            foreach (var child in box.Children())
                child.style.left = 10f;
            descriptionSplitView.Add(box);
            
            subSplitView.Add(descriptionSplitView);

            view.root = subSplitView;
            view.UpdateView(); // 初期更新

            return view;
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
