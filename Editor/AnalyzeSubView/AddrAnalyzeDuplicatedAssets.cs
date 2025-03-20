using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build.DataBuilders;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine.UIElements;

namespace AddrAuditor.Editor
{
    /// <summary>
    /// 重複しているアセットの検出
    /// </summary>
    class AnalyzeViewDuplicatedAssets : SubCategoryView
    {
        readonly List<AddrAutoGrouping.ImplicitParam> duplicatedList = new ();
        readonly List<AddressableAssetEntry> allEntries = new ();
        readonly List<RefEntry> referencingList = new ();
        AddressableAssetsBuildContext aaContext;
        
        ListView listView;
        VisualElement optionalView;
        Label detailsLabel; // カテゴリの説明文
        ListView referenceView;
        
        /// <summary>
        /// Callback when any column is selected
        /// </summary>
        void OnSelectedChanged(IEnumerable<int> selectedItems)
        {
            if (!selectedItems.Any())
                return;
            var index = selectedItems.First();
            var dup = this.duplicatedList[index];
            if (string.IsNullOrEmpty(dup.path))
                return;
            
            // focusing in Project Window
            var obj = AssetDatabase.LoadMainAssetAtPath(this.duplicatedList[index].path);
            Selection.activeObject = obj;
            EditorGUIUtility.PingObject(obj);
            
            FindReferringEntries(this.referencingList, this.allEntries, dup);
            this.referenceView.ClearSelection();
            this.referenceView.itemsSource = this.referencingList;
            this.referenceView.Rebuild();
        }
        
        void OnSelectedReferenceChanged(IEnumerable<int> selectedItems)
        {
            if (!selectedItems.Any())
                return;
            var index = selectedItems.First();
            var dup = this.referencingList[index];
            if (string.IsNullOrEmpty(dup.path))
                return;
            
            // focusing in Project Window
            var obj = AssetDatabase.LoadMainAssetAtPath(this.referencingList[index].path);
            Selection.activeObject = obj;
            EditorGUIUtility.PingObject(obj);
        }

        /// <summary>
        /// 解析処理
        /// </summary>
        public override void Analyze()
        {
            // Auto Groupingとほぼ同じステップ
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            var (implicitParams, _, cachedContext) = AddrAutoGrouping.CollectImplicitParams(settings);
            if (implicitParams == null)
                return;
            this.aaContext = cachedContext;
            
            this.duplicatedList.Clear();
            foreach (var param in implicitParams)
            {
                if (param.bundles.Count == 1) // not duplicated
                    continue;
                this.duplicatedList.Add(param);
            }
            this.allEntries.Clear();
            var groups = settings.groups;
            foreach (var t in groups)
                this.allEntries.AddRange(t.entries);
            this.duplicatedList.Sort(CompareName);
        }

        /// <summary>
        /// GUI構築
        /// </summary>
        protected override void OnCreateView()
        {
            this.listView = new ListView();
            {
                this.listView.fixedItemHeight = 30f;
                this.listView.selectedIndicesChanged += this.OnSelectedChanged;
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
                    var t = this.duplicatedList[index];
                    var label = element.Q<Label>("itemLabel");
                    label.text = $"   {t.path}";
                };
            }
            this.rootElement.Add(this.listView);

            this.optionalView = new TwoPaneSplitView(0, 200, TwoPaneSplitViewOrientation.Vertical);
            {
                var box = new VisualElement();
                var header = new Label("Details");
                header.style.unityFontStyleAndWeight = FontStyle.Bold;
                box.Add(header);
                this.detailsLabel = new Label("explain what is setting");
                this.detailsLabel.name = "itemExplanation";
                this.detailsLabel.style.whiteSpace = WhiteSpace.Normal;
                this.detailsLabel.text = "重複してAssetBundleに含まれているアセットを検出します。\n" +
                                         "適切にグループを設定するかAutoGrouping機能を利用して解決してください。\n" +
                                         "Materialなど非常に小さいアセットは、ロード時間を考慮し重複の許容を検討できます。";
                box.Add(this.detailsLabel);
                foreach (var child in box.Children())
                    child.style.left = 10f;
                this.optionalView.Add(box);

                box = new VisualElement();
                header = new Label("Referring Entries");
                header.style.unityFontStyleAndWeight = FontStyle.Bold;
                box.Add(header);
                // TODO
                this.referenceView = new ListView();
                {
                    this.referenceView.fixedItemHeight = 25f;
                    this.referenceView.selectedIndicesChanged += this.OnSelectedReferenceChanged;
                    this.referenceView.selectionType = SelectionType.Single;
                    this.referenceView.makeItem = () =>
                    {
                        var label = new Label();
                        label.style.unityTextAlign = TextAnchor.MiddleLeft;
                        return label;
                    };
                    this.referenceView.bindItem = (element, index) =>
                    {
                        var t = this.referencingList[index];
                        if (element is Label label)
                            label.text = $"   {t.group.name} > {t.path}";
                    };
                }
                box.Add(this.referenceView);
                foreach (var child in box.Children())
                    child.style.left = 10f;
                this.optionalView.Add(box);
            }
            this.rootElement.Add(this.optionalView);
        }

        /// <summary>
        /// 表示の更新
        /// カテゴリが選択された時に呼ばれる
        /// </summary>
        public override void UpdateView()
        {
            this.listView.ClearSelection();
            this.listView.itemsSource = this.duplicatedList;
            this.listView.Rebuild();
        }
        
        static readonly System.Text.RegularExpressions.Regex NUM_REGEX = new System.Text.RegularExpressions.Regex(@"[^0-9]");
        /// <summary>
        /// alphanumericソート
        /// </summary>
        static int CompareName(AddrAutoGrouping.ImplicitParam aParam, AddrAutoGrouping.ImplicitParam bParam)
        {
            var a = aParam.path;
            var b = bParam.path;
            var ret = string.CompareOrdinal(a, b);
            // 桁数の違う数字を揃える
            var regA = NUM_REGEX.Replace(a, string.Empty);
            var regB = NUM_REGEX.Replace(b, string.Empty);
            if ((regA.Length > 0 && regB.Length > 0) && regA.Length != regB.Length)
            {
                if (ret > 0 && regA.Length < regB.Length)
                    return -1;
                else if (ret < 0 && regA.Length > regB.Length)
                    return 1;
            }

            return ret;
        }

        struct RefEntry
        {
            public string guid;
            public string path;
            public AddressableAssetGroup group;
        }

        static void FindReferringEntries(List<RefEntry> refEntries, List<AddressableAssetEntry> allEntries, AddrAutoGrouping.ImplicitParam param)
        {
            refEntries.Clear();
            var entryCount = allEntries.Count;
            for (var i = 0; i < entryCount; ++i)
            {
                var entry = allEntries[i];
                EditorUtility.DisplayCancelableProgressBar("Searching Referring Entries...", entry.AssetPath, (float)i/entryCount);
                var path = AssetDatabase.GUIDToAssetPath(entry.guid);
                var dependencyPaths = AssetDatabase.GetDependencies(path, true);
                foreach (var depPath in dependencyPaths)
                {
                    if (depPath != param.path)
                        continue;
                    var refEntry = new RefEntry()
                    {
                        guid = entry.guid,
                        path = path,
                        group = entry.parentGroup,
                    };
                    refEntries.Add(refEntry);
                    break;
                }
            }
            EditorUtility.ClearProgressBar();
        }
    }
}
