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
        readonly List<RefEntry> refEntries = new ();

        AnalyzeCache analyzeCache;
        ListView listView;
        VisualElement optionalView;
        Label detailsLabel;
        ListView referenceView;
        
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
            
            // // focusing in Project Window
            // var obj = AssetDatabase.LoadMainAssetAtPath(dup.path);
            // Selection.activeObject = obj;
            // EditorGUIUtility.PingObject(obj);
            
            FindReferencedEntries(this.refEntries, this.analyzeCache, dup);
            this.referenceView.ClearSelection();
            this.referenceView.itemsSource = this.refEntries;
            this.referenceView.Rebuild();
        }
        
        void OnSelectedReferenceChanged(IEnumerable<int> selectedItems)
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

            this.optionalView = new TwoPaneSplitView(0, 200, TwoPaneSplitViewOrientation.Vertical);
            {
                var box = new VisualElement();
                {
                    var header = new Label("Details");
                    header.style.unityFontStyleAndWeight = FontStyle.Bold;
                    box.Add(header);
                    this.detailsLabel = new Label("explain what is setting");
                    this.detailsLabel.name = "itemExplanation";
                    this.detailsLabel.style.whiteSpace = WhiteSpace.Normal;
                    this.detailsLabel.text = DETAILS_MESSAGE;
                    box.Add(this.detailsLabel);
                    foreach (var child in box.Children())
                        child.style.left = 10f;
                }
                this.optionalView.Add(box);

                box = new VisualElement();
                {
                    var header = new Label("Referencing Entries");
                    header.style.unityFontStyleAndWeight = FontStyle.Bold;
                    box.Add(header);
                    this.referenceView = new ListView();
                    {
                        this.referenceView.fixedItemHeight = 25f;
                        this.referenceView.selectedIndicesChanged += this.OnSelectedReferenceChanged;
                        this.referenceView.itemsChosen += chosenItems =>
                        {
                            if (!chosenItems.Any())
                                return;
                            if (chosenItems.First() is RefEntry refEntry)
                            {
                                // focusing in Project Window
                                var obj = AssetDatabase.LoadMainAssetAtPath(refEntry.assetPath);
                                Selection.activeObject = obj;
                                EditorGUIUtility.PingObject(obj);
                            }
                        };
                        this.referenceView.selectionType = SelectionType.Single;
                        this.referenceView.makeItem = () =>
                        {
                            var label = new Label();
                            label.style.unityTextAlign = TextAnchor.MiddleLeft;
                            return label;
                        };
                        this.referenceView.bindItem = (element, index) =>
                        {
                            var t = this.refEntries[index];
                            if (element is Label label)
                                label.text = $"   {t.groupPath ?? "No entry(Implicit asset)"} > {t.assetPath}";
                        };
                    }
                    box.Add(this.referenceView);
                    foreach (var child in box.Children())
                        child.style.left = 10f;
                }
                this.optionalView.Add(box);
            }
            this.rootElement.Add(this.optionalView);
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
    }
}
