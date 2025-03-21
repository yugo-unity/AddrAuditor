using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;

namespace AddrAuditor.Editor
{
    /// <summary>
    /// Analyze references for Addressable
    /// </summary>
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
            
            MAX,
        }
        static readonly string[] MAIN_CATEGORIES = new string[(int)ANALYZE.MAX]
        {
            "   Addressable Asset Settings",
            "   Addressable Group Settings",
            "   Duplicated Assets in Addressable",
            "   Duplicated Assets in Built-in Assets",
            "   Unused Material Properties in Project",
            "   Missing Asset References in Project",
        };
        
        ResultView[] subCategories = new ResultView[MAIN_CATEGORIES.Length];
        TwoPaneSplitView mainSplitView;
        VisualElement rightPane;
        int currentCategory;
        AnalyzeCache analyzeCache;
        
        /// <summary>
        /// Build Editor-Window
        /// </summary>
        void CreateGUI()
        {
            AddrUtility.ReloadInternalAPI();
            
            var root = this.rootVisualElement;
            
            var header = new Box();
            {
                var colorElement = 0.24f;
                header.style.backgroundColor = new Color(colorElement, colorElement, colorElement, 1f);
                header.style.height = 32f;
                header.style.borderBottomWidth = 1;
                header.style.borderBottomColor = Color.black;
                header.style.flexDirection = FlexDirection.Row;

                var analyzeButton = new Button();
                {
                    analyzeButton.text = "Analyze";
                    analyzeButton.style.width = 100f;
                    analyzeButton.style.height = 25f;
                    analyzeButton.clicked += () =>
                    {
                        var subView = this.subCategories[this.currentCategory];
                        this.CreateAnalyzeCache(subView.requireAnalyzeCache);
                        subView.Analyze(this.analyzeCache);
                        subView.UpdateView();
                    };
                }
                header.Add(analyzeButton);
                var analyzeAllButton = new Button();
                {
                    analyzeAllButton.text = "Analyze All";
                    analyzeAllButton.style.width = 100f;
                    analyzeAllButton.style.height = 25f;
                    analyzeAllButton.clicked += () =>
                    {
                        this.CreateAnalyzeCache(true);
                        foreach (var view in this.subCategories)
                            view.Analyze(this.analyzeCache);
                        this.subCategories[this.currentCategory].UpdateView();
                    };
                }
                header.Add(analyzeAllButton);
            }
            root.Add(header);

            this.mainSplitView = new TwoPaneSplitView(0, 240, TwoPaneSplitViewOrientation.Horizontal);
            {
                var categories = new ListView(MAIN_CATEGORIES);
                {
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
                    categories.selectedIndex = 0;
                    categories.selectedIndicesChanged += (selectedItems) =>
                    {
                        var index = selectedItems.First();
                        this.UpdateSubView(index);
                    };
                }
                this.mainSplitView.Add(categories);

                this.rightPane = new Box();
                this.mainSplitView.Add(this.rightPane);
            }
            root.Add(this.mainSplitView);

            this.subCategories[(int)ANALYZE.ASSET_SETTINGS] = CreateSubView<AnalyzeViewAddrSetting>(true);
            this.subCategories[(int)ANALYZE.GROUP_SETTINGS] = CreateSubView<AnalyzeViewGroupSetting>(true);
            this.subCategories[(int)ANALYZE.DUPLICATED_ASSETS] = CreateSubView<AnalyzeViewDuplicatedAssets>(true);
            this.subCategories[(int)ANALYZE.BUILT_IN_ASSETS] = CreateSubView<AnalyzeViewBuiltInAssets>(true);
            this.subCategories[(int)ANALYZE.UNUSED_PROP] = CreateSubView<AnalyzeViewUnusedMaterialProp>(false);
            this.subCategories[(int)ANALYZE.MISSING_REF] = CreateSubView<AnalyzeViewMissingReferences>(false);

            this.UpdateSubView(0);
        }

        /// <summary>
        /// create cache to be used multiple categories
        /// </summary>
        void CreateAnalyzeCache(bool buildCache)
        {
            var setting = AddressableAssetSettingsDefaultObject.Settings;
            if (buildCache)
            {
                var (refAssets, spAtlases) = AddrAutoGrouping.CollectReferencedAssetInfo(setting, null, false);
                var entries = new List<AddressableAssetEntry>();
                foreach (var t in setting.groups)
                    entries.AddRange(t.entries);
                this.analyzeCache = new AnalyzeCache()
                {
                    addrSetting = setting,
                    refAssets = refAssets,
                    explicitEntries = entries,
                    spriteAtlases = spAtlases,
                };
            }
            else
            {
                this.analyzeCache = new AnalyzeCache()
                {
                    addrSetting = setting,
                };
            }
        }

        /// <summary>
        /// update selected info 
        /// </summary>
        /// <param name="categoryIndex">index for category what you want to display</param>
        void UpdateSubView(int categoryIndex)
        {
            this.currentCategory = categoryIndex;
            this.subCategories[categoryIndex].UpdateView();
            this.rightPane.Clear();
            this.rightPane.Add(this.subCategories[categoryIndex].rootElement);
        }
        
        /// <summary>
        /// create instances for any category's info
        /// </summary>
        /// <typeparam name="T">SubCategory class</typeparam>
        /// <returns>SubCategory instance</returns>
        static T CreateSubView<T>(bool splitThree) where T : ResultView, new()
        {
            var view = new T();
            var orientation = splitThree ? TwoPaneSplitViewOrientation.Horizontal : TwoPaneSplitViewOrientation.Vertical;
            view.CreateView(orientation);
            return view;
        }
    }
}
