using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Linq;

namespace AddrAuditor.Editor
{
    internal partial class AddrAnalyzeWindow : EditorWindow
    {
        /// <summary>
        /// TODO: 解析用
        /// </summary>
        public static void Analyzing()
        {
            var settingsPath = $"Assets/{nameof(AddrAutoGroupingSettings)}.asset";
            var groupingSettings = AssetDatabase.LoadAssetAtPath<AddrAutoGroupingSettings>(settingsPath);
        }

        static readonly List<string> MAIN_CATEGORIES = new ()
        {
            "   Addressables Asset Settings",
            "   Addressables Group Settings",
            "   Duplicate Assets",
            "   Built-in Assets",
            "   Unused Material Properties",
            "   Missing Asset References",
        };
        
        SubCategoryView[] subCategories;
        TwoPaneSplitView mainSplitView;
        VisualElement rightPane;
        
        /// <summary>
        /// Window初回構築
        /// </summary>
        void CreateGUI()
        {
            this.subCategories = new SubCategoryView[MAIN_CATEGORIES.Count];
            this.mainSplitView = new TwoPaneSplitView(0, 200, TwoPaneSplitViewOrientation.Horizontal);
         
            var root = this.rootVisualElement;   
            var header = new Box();
            var colorElement = 0.24f;
            header.style.backgroundColor = new Color(colorElement, colorElement, colorElement, 1f);
            header.style.height = 20f;
            header.style.borderBottomWidth = 1;
            header.style.borderBottomColor = Color.black;
            root.Add(header);
            
            // 左側のカテゴリリスト
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
            categories.selectedIndex = 0;
            categories.selectedIndicesChanged += this.OnCategoryChanged;
            this.mainSplitView.Add(categories);
            
            // 右側のView生成
            this.subCategories[0] = CreateSubCategoryView<AnalyzeViewAddrSetting>();
            this.subCategories[1] = CreateSubCategoryView<AnalyzeViewGroupSetting>();
            this.subCategories[2] = CreateSubCategoryView<AnalyzeViewAddrSetting>();
            this.subCategories[3] = CreateSubCategoryView<AnalyzeViewGroupSetting>();
            this.subCategories[4] = CreateSubCategoryView<AnalyzeViewAddrSetting>();

            // NOTE: TwoPaneSplitViewは初回だけRightPaneの挙動が違う
            //       おそらく動的にPaneを再構築するのを想定してない
            this.rightPane = new Box();
            this.mainSplitView.Add(this.rightPane);
            this.UpdateSubView(0);
            
            root.Add(this.mainSplitView);
        }

        /// <summary>
        /// RightPaneの再構築
        /// </summary>
        /// <param name="index"></param>
        void UpdateSubView(int index)
        {
            this.subCategories[index].UpdateView();
            this.rightPane.Clear();
            this.rightPane.Add(this.subCategories[index].root);
        }

        /// <summary>
        /// メインカテゴリが変更された際のLeftPaneの再構築
        /// </summary>
        /// <param name="selectedItems">選択された項目</param>
        void OnCategoryChanged(IEnumerable<int> selectedItems)
        {
            var index = selectedItems.First();
            this.UpdateSubView(index);
        }
    }
}
