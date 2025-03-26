using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor;

namespace AddrAuditor.Editor
{
    /// <summary>
    /// view for duplicated asset in Addressable
    /// </summary>
    class AnalyzeViewDuplicatedAssets : ResultView
    {
        static readonly string DETAILS_MESSAGE = "重複してAssetBundleに含まれているアセットを検出します。\n" +
                                                 "適切にグループを設定するかAutoGrouping機能を利用して解決してください。\n" +
                                                 "Materialなど非常に小さいアセットは、ロード時間を考慮し重複の許容を検討できます。";

        public override bool requireAnalyzeCache => true;
        readonly List<RefAssetData> duplications = new ();
        List<RefEntry> refEntries = new ();

        AnalyzeCache analyzeCache;
        ListView listView, refentryListView;
        VisualElement referencedRoot;
        
        /// <summary>
        /// Callback when any column is selected
        /// </summary>
        void OnSelectedChanged(IEnumerable<int> selectedItems)
        {
            if (selectedItems is not List<int> indexList || indexList.Count == 0)
                return;
            var index = indexList[0];
            var dup = this.duplications[index];
            if (string.IsNullOrEmpty(dup.path))
                return;
            
            // focusing in Project Window
            var obj = AssetDatabase.LoadMainAssetAtPath(dup.path);
            Selection.activeObject = obj;
            EditorGUIUtility.PingObject(obj);

            this.UpdateReferencedView();
        }
        
        void OnSelectedRefentryListChanged(IEnumerable<int> selectedItems)
        {
            if (!selectedItems.Any())
                return;
            var index = selectedItems.First();
            var dup = this.refEntries[index];
            if (string.IsNullOrEmpty(dup.assetPath))
                return;
            
            // focusing in Project Window
            var obj = AssetDatabase.LoadMainAssetAtPath(this.refEntries[index].assetPath);
            Selection.activeObject = obj;
            EditorGUIUtility.PingObject(obj);
        }

        /// <summary>
        /// called when require to analyze
        /// </summary>
        /// <param name="cache">build cache that created by AddrAnalyzeWindow</param>
        public override void Analyze(AnalyzeCache cache)
        {
            this.analyzeCache = cache;
            FindDuplicatedAssets(this.duplications, cache);
        }

        /// <summary>
        /// called when created view (only once)
        /// </summary>
        protected override void OnCreateView()
        {
            // main assets list
            this.listView = new ListView();
            {
                this.listView.fixedItemHeight = 30f;
                this.listView.selectedIndicesChanged += this.OnSelectedChanged;
                this.listView.itemsChosen += chosenItems =>
                {
                    if (!chosenItems.Any())
                        return;
                    if (chosenItems.First() is RefAssetData refAsset)
                    {
                        // focusing in Project Window
                        var obj = AssetDatabase.LoadMainAssetAtPath(refAsset.path);
                        Selection.activeObject = obj;
                        EditorGUIUtility.PingObject(obj);
                    }
                };
                this.listView.selectionType = SelectionType.Single;
                this.listView.makeItem = () =>
                {
                    var label = new Label();
                    label.name = "itemLabel";
                    label.style.unityTextAlign = TextAnchor.MiddleLeft;
                    return label;
                };
                this.listView.bindItem = (element, index) =>
                {
                    var t = this.duplications[index];
                    var label = element.Q<Label>("itemLabel");
                    label.text = $"   {t.path}";
                };
            }
            this.rootElement.Add(this.listView);

            // right side view
            var optionalView = new TwoPaneSplitView(0, 100, TwoPaneSplitViewOrientation.Vertical);
            {
                var box = new Box();
                {
                    var header = new Label("Details");
                    header.style.unityFontStyleAndWeight = FontStyle.Bold;
                    header.style.left = 10f;
                    box.Add(header);
                    
                    var label = new Label("explain what is setting");
                    label.style.whiteSpace = WhiteSpace.Normal;
                    label.style.left = 10f;
                    label.style.height = 200f;
                    label.text = DETAILS_MESSAGE;
                    box.Add(label);
                }
                optionalView.Add(box);

                var referencedBox = new Box();
                {
                    this.refentryListView = new ListView();
                    {
                        this.refentryListView.fixedItemHeight = 25f;
                        this.refentryListView.selectedIndicesChanged += this.OnSelectedRefentryListChanged;
                        this.refentryListView.selectionType = SelectionType.Single;
                        this.refentryListView.makeItem = () =>
                        {
                            var label = new Label();
                            label.style.unityTextAlign = TextAnchor.MiddleLeft;
                            return label;
                        };
                        this.refentryListView.bindItem = (element, index) =>
                        {
                            var t = this.refEntries[index];
                            if (element is Label label)
                                label.text = $"   {t.groupPath ?? "No entry(Implicit asset)"} > {t.assetPath}";
                        };
                    }
                    this.refentryListView.style.left = 10f;
            
                    var header = new Label("Referencing Entries")
                    {
                        style =
                        {
                            unityFontStyleAndWeight = FontStyle.Bold,
                            left = 10f,
                            alignSelf = Align.FlexStart
                        }
                    };
                    referencedBox.Add(header);
                    this.referencedRoot = new VisualElement();
                    this.referencedRoot.style.flexGrow = 1;
                    this.referencedRoot.style.flexDirection = FlexDirection.Column;
                    this.referencedRoot.style.alignItems = Align.Stretch;
                    referencedBox.Add(this.referencedRoot);
                    
                    this.UpdateReferencedView();
                }
                optionalView.Add(referencedBox);
            }
            this.rootElement.Add(optionalView);
        }

        /// <summary>
        /// called when selecting any category
        /// </summary>
        public override void UpdateView()
        {
            this.listView.ClearSelection();
            this.listView.itemsSource = this.duplications;
            this.listView.Rebuild();
        }

        /// <summary>
        /// update view for referenced entries
        /// </summary>
        void UpdateReferencedView()
        {
            this.referencedRoot.Clear();
            
            var index = this.listView.selectedIndex;
            if (index >= 0 && this.analyzeCache.refEntryDic.TryGetValue(this.duplications[index].guid, out var referencedEntries))
            {
                this.UpdateReferencedList(referencedEntries);
            }
            else
            {
                var topSpacer = new VisualElement();
                topSpacer.style.flexGrow = 1;
                this.referencedRoot.Add(topSpacer);

                var button = new Button
                {
                    text = "Find Referenced Assets",
                    style =
                    {
                        alignSelf = Align.Center,
                        width = 200f,
                        height = 60f
                    }
                };
                if (index < 0)
                {
                    button.style.opacity = 0.5f;
                }
                else
                {
                    button.clicked += () =>
                    {
                        var dup = this.duplications[index];
                        var entriesCache = FindReferencedEntries(this.analyzeCache, dup);
                        this.analyzeCache.refEntryDic.Add(dup.guid, entriesCache);
                        this.UpdateReferencedList(entriesCache);
                    };
                }
                this.referencedRoot.Add(button);

                var bottomSpacer = new VisualElement();
                bottomSpacer.style.flexGrow = 1;
                this.referencedRoot.Add(bottomSpacer);
            }
        }

        /// <summary>
        /// update referenced entries list
        /// </summary>
        /// <param name="entries">referenced entries</param>
        void UpdateReferencedList(List<RefEntry> entries)
        {
            this.refEntries = entries;
            this.referencedRoot.Clear();
            this.refentryListView.ClearSelection();
            this.refentryListView.itemsSource = entries;
            this.refentryListView.Rebuild();
            this.referencedRoot.style.alignSelf = Align.Auto;
            this.referencedRoot.Add(this.refentryListView);
        }
    }
}
