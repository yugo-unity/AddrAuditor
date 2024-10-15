using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Linq;

namespace AddrAuditor.Editor
{
    internal partial class AddrAnalyzeWindow : EditorWindow
    {
        abstract class SubCategoryView
        {
            public TwoPaneSplitView root;
            public ListView treeList;
            public Label detailsLabel;
            public Label recommendationLabel;

            public abstract void UpdateView();
        }
        
        static readonly List<string> MAIN_CATEGORIES = new ()
        {
            "   Addressables Asset Settings",
            "   Addressables Group Settings",
            "   Duplicate Assets",
            "   Built-in Assets",
            "   Unused Material Properties"
        };
        
        SubCategoryView[] subCategories;
        VisualElement mainSplitView;
        
        public void CreateGUI()
        {
            var root = this.rootVisualElement;
            this.subCategories = new SubCategoryView[MAIN_CATEGORIES.Count];
            
            this.mainSplitView = new TwoPaneSplitView(0, 200, TwoPaneSplitViewOrientation.Horizontal);
            
            // 左側のカテゴリリスト
            var leftPane = new Box(); 
            var categories = new ListView(MAIN_CATEGORIES);
            categories.makeItem = () => 
            {
                var label = new Label();
                label.style.unityTextAlign = TextAnchor.MiddleLeft;
                return label;
            };
            categories.bindItem = (element, index) =>
            {
                if (element is Label label)
                    label.text = MAIN_CATEGORIES[index];
            };
            categories.Rebuild();
            categories.selectionChanged += this.OnLeftSelectionChanged;
            leftPane.Add(categories);
            this.mainSplitView.Add(leftPane);
            
            // 右側のView生成
            this.subCategories[0] = CreateSubCategoryView<AnalyzeViewAddrSetting>();
            this.subCategories[1] = CreateSubCategoryView<AnalyzeViewGroupSetting>();
            this.subCategories[2] = CreateSubCategoryView<AnalyzeViewAddrSetting>();
            this.subCategories[3] = CreateSubCategoryView<AnalyzeViewGroupSetting>();
            this.subCategories[4] = CreateSubCategoryView<AnalyzeViewAddrSetting>();
            this.mainSplitView.Add(this.subCategories[0].root);
            
            root.Add(this.mainSplitView);
        }

        void UpdateSubView(int index)
        {
            this.subCategories[index].UpdateView();
            this.mainSplitView.Add(this.subCategories[index].root);
        }

        void OnLeftSelectionChanged(IEnumerable<object> selectedItems)
        {
            if (selectedItems.FirstOrDefault() is string selectedItem)
            {
                // 右側のリスト更新
                var index = MAIN_CATEGORIES.IndexOf(selectedItem);
                this.mainSplitView.RemoveAt(1);
                this.UpdateSubView(index);
            }
        }

        static T CreateSubCategoryView<T>() where T : SubCategoryView, new()
        {
            var view = new T();
            var splitView = new TwoPaneSplitView(0, 300, TwoPaneSplitViewOrientation.Horizontal);
            
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
            splitView.Add(treeList);
            view.treeList = treeList;
            
            var de_re_view = new TwoPaneSplitView(0, 200, TwoPaneSplitViewOrientation.Vertical);

            var box = new Box();
            var header = new Label("Details");
            header.style.unityFontStyleAndWeight = FontStyle.Bold;
            box.Add(header);
            view.detailsLabel = new Label("explain what is setting"); 
            box.Add(view.detailsLabel);
            foreach (var child in box.Children())
                child.style.left = 10f;
            de_re_view.Add(box);
            
            box = new Box();
            header = new Label("Recommendation");
            header.style.unityFontStyleAndWeight = FontStyle.Bold;
            box.Add(header);
            view.recommendationLabel = new Label("About recommended setting");
            box.Add(view.recommendationLabel);
            foreach (var child in box.Children())
                child.style.left = 10f;
            de_re_view.Add(box);
            
            splitView.Add(de_re_view);

            view.root = splitView;
            view.UpdateView(); // 初期更新

            return view;
        }
    }
}
