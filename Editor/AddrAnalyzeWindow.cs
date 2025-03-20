using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace AddrAuditor.Editor
{
    internal class AddrAnalyzeWindow : EditorWindow
    {
        enum ANALYZE
        {
            ASSET_SETTINGS,
            GROUP_SETTINGS,
            DUPLICATED_ASSETS,
            BUILT_IN_ASSETS,
            UNUSED_PROP,
            MISSING_REF,
        }

        static readonly List<string> MAIN_CATEGORIES = new ()
        {
            "   Addressables Asset Settings",
            "   Addressables Group Settings",
            "   Duplicated Assets",
            "   Built-in Assets",
            "   Unused Material Properties",
            "   Missing Asset References",
        };
        
        SubCategoryView[] subCategories;
        TwoPaneSplitView mainSplitView;
        VisualElement rightPane;
        int currentCategory;
        
        /// <summary>
        /// Window初回構築
        /// </summary>
        void CreateGUI()
        {
            this.subCategories = new SubCategoryView[MAIN_CATEGORIES.Count];
            this.mainSplitView = new TwoPaneSplitView(0, 200, TwoPaneSplitViewOrientation.Horizontal);
         
            var root = this.rootVisualElement;   
            var header = new Box();
            {
                var colorElement = 0.24f;
                header.style.backgroundColor = new Color(colorElement, colorElement, colorElement, 1f);
                header.style.height = 32f;
                header.style.borderBottomWidth = 1;
                header.style.borderBottomColor = Color.black;
            }
            var analyzeButton = new Button();
            {
                //analyzeButton.name = "itemButton";
                analyzeButton.text = "Analyze";
                analyzeButton.style.alignSelf = Align.FlexEnd;
                analyzeButton.style.width = 100f;
                analyzeButton.style.height = 25f;
                analyzeButton.clicked += () =>
                {
                    foreach (var view in this.subCategories)
                        view.Analyze();

                    this.subCategories[this.currentCategory].UpdateView();
                };
            }
            header.Add(analyzeButton);
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
            categories.selectedIndicesChanged += (selectedItems) =>
            {
                // メインカテゴリが変更された際のLeftPaneの再構築
                var index = selectedItems.First();
                this.UpdateSubView(index);
            };
            this.mainSplitView.Add(categories);
            
            // 右側のView生成
            this.subCategories[(int)ANALYZE.ASSET_SETTINGS] = CreateSubView<AnalyzeViewAddrSetting>(true);
            this.subCategories[(int)ANALYZE.GROUP_SETTINGS] = CreateSubView<AnalyzeViewGroupSetting>(true);
            this.subCategories[(int)ANALYZE.DUPLICATED_ASSETS] = CreateSubView<AnalyzeViewDuplicatedAssets>(true);
            this.subCategories[(int)ANALYZE.BUILT_IN_ASSETS] = CreateSubView<AnalyzeViewGroupSetting>(false); // TODO
            this.subCategories[(int)ANALYZE.UNUSED_PROP] = CreateSubView<AnalyzeViewUnusedMaterialProp>(false);
            this.subCategories[(int)ANALYZE.MISSING_REF] = CreateSubView<AnalyzeViewMissingReferences>(false);

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
            this.currentCategory = index;
            this.subCategories[index].UpdateView();
            this.rightPane.Clear();
            this.rightPane.Add(this.subCategories[index].rootElement);
        }
        
        /// <summary>
        /// RightPaneの共通生成処理
        /// </summary>
        /// <typeparam name="T">SubCategory固有のクラス</typeparam>
        /// <returns>SubCategory管理クラス</returns>
        static T CreateSubView<T>(bool splitThree) where T : SubCategoryView, new()
        {
            var view = new T();
            var orientation = splitThree ? TwoPaneSplitViewOrientation.Horizontal : TwoPaneSplitViewOrientation.Vertical;
            view.CreateView(orientation);
            return view;
        }
    }
}
