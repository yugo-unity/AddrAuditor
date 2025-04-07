using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor;

namespace AddrAuditor.Editor
{
    /// <summary>
    /// view for unused Material Properties
    /// </summary>
    class AnalyzeViewUnusedMaterialProp : ResultView
    {
        static readonly string DETAILS_MESSAGE = "Materialに含まれる未使用のPropertyを検出します。MaterialのShaderを変更した際、" +
                                                 "変更前に使用されていたPropertyは自動で削除されません。\n" +
                                                 "ランタイムでShaderを切り替えるようなケースがない限り削除した方がベターです。\n" +
                                                 "なお、この解析はAddressableに関わらずプロジェクト全体に行われます。";
        
        struct UnusedProp
        {
            public string guid;
            public string assetPath;
            public Material material;
            public SerializedProperty sp;
            public string propName;
            public int propIndex;
            public RefAssetData refAssetData;
            bool initRefAssetData;
            
            public void AddList(List<UnusedProp> list, SerializedProperty sp, int propIndex, string propName, AnalyzeCache analyzeCache)
            {
                this.sp = sp;
                this.propIndex = propIndex;
                this.propName = propName;
                if (this.initRefAssetData == false)
                {
                    var temp = this.guid;
                    this.refAssetData = analyzeCache.refAssets.Find(item => item.guid == temp);
                    this.initRefAssetData = true;
                }
                list.Add(this);
            }
        }

        public override bool requireAnalyzeCache => true;
        readonly List<UnusedProp> unusedProps = new ();
        List<RefEntry> refEntries = new ();
        
        AnalyzeCache analyzeCache;
        ListView listView, referenceListView;
        VisualElement referencedBox;

        /// <summary>
        /// Callback when any column is selected
        /// </summary>
        void OnSelectedChanged(IEnumerable<int> selectedItems)
        {
            if (selectedItems is not List<int> indexList || indexList.Count == 0)
                return;
            var index = indexList[0];
            var propData = this.unusedProps[index];
            if (string.IsNullOrEmpty(propData.assetPath))
                return;
            
            // focusing in Project Window
            Selection.activeObject = propData.material;
            EditorGUIUtility.PingObject(propData.material);

            this.UpdateReferencedView();
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
            this.unusedProps.Clear();
            //var guids = AssetDatabase.FindAssets("t:Material"); // include Packages
            var serachFolder = new string[] { "Assets", };
            var guids = AssetDatabase.FindAssets("t:Material", serachFolder);

            for (var index = 0; index < guids.Length; ++index)
            {
                var guid = guids[index];
                var path = AssetDatabase.GUIDToAssetPath(guid);
                EditorUtility.DisplayCancelableProgressBar("Searching Unused Material Properties...", path, (float)index/guids.Length);
                var m = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (m == null || m.shader == null)
                    continue;

                var properties = new HashSet<string>();
                var count = ShaderUtil.GetPropertyCount(m.shader);
                for (var i = 0; i < count; i++)
                {
                    var propName = ShaderUtil.GetPropertyName(m.shader, i);
                    properties.Add(propName);
                }

                var so = new SerializedObject(m);
                var sp = so.FindProperty("m_SavedProperties");

                var texEnvSp = sp.FindPropertyRelative("m_TexEnvs");
                var unusedProp = new UnusedProp()
                {
                    guid = guid,
                    assetPath = path,
                    material = m,
                    //sp = sp,
                    // propIndex = index,
                    // propName = propName,
                };
                for (var i = texEnvSp.arraySize - 1; i >= 0; i--)
                {
                    var propName = texEnvSp.GetArrayElementAtIndex(i).FindPropertyRelative("first").stringValue;
                    if (!properties.Contains(propName))
                        unusedProp.AddList(unusedProps, texEnvSp, i, propName, analyzeCache);
                }

                var floatsSp = sp.FindPropertyRelative("m_Floats");
                for (var i = floatsSp.arraySize - 1; i >= 0; i--)
                {
                    var propName = floatsSp.GetArrayElementAtIndex(i).FindPropertyRelative("first").stringValue;
                    if (!properties.Contains(propName))
                        unusedProp.AddList(unusedProps, floatsSp, i, propName, analyzeCache);
                }

                var intSp = sp.FindPropertyRelative("m_Ints");
                for (var i = intSp.arraySize - 1; i >= 0; i--)
                {
                    var propName = intSp.GetArrayElementAtIndex(i).FindPropertyRelative("first").stringValue;
                    if (!properties.Contains(propName))
                        unusedProp.AddList(unusedProps, intSp, i, propName, analyzeCache);
                }

                var colorsSp = sp.FindPropertyRelative("m_Colors");
                for (var i = colorsSp.arraySize - 1; i >= 0; i--)
                {
                    var propName = colorsSp.GetArrayElementAtIndex(i).FindPropertyRelative("first").stringValue;
                    if (!properties.Contains(propName))
                        unusedProp.AddList(unusedProps, colorsSp, i, propName, analyzeCache);
                }
            }
            EditorUtility.ClearProgressBar();
        }

        /// <summary>
        /// called when created view (only once)
        /// </summary>
        protected override void OnCreateView()
        {   
            // main assets list
            this.listView = new ListView();
            {
                this.listView.fixedItemHeight = 25f;
                this.listView.selectedIndicesChanged += this.OnSelectedChanged;
                this.listView.itemsChosen += chosenItems =>
                {
                    if (!chosenItems.Any())
                        return;
                    if (chosenItems.First() is UnusedProp propData)
                    {
                        // focusing in Project Window
                        Selection.activeObject = propData.material;
                        EditorGUIUtility.PingObject(propData.material);
                    }
                };
                this.listView.selectionType = SelectionType.Single;
                this.listView.makeItem = () =>
                {
                    var container = new VisualElement();
                    container.style.flexDirection = FlexDirection.Row;

                    var button = new Button();
                    button.name = "itemButton";
                    button.text = "Remove";
                    container.Add(button);

                    var label = new Label();
                    label.name = "itemLabel";
                    label.style.unityTextAlign = TextAnchor.MiddleLeft;
                    container.Add(label);

                    return container;
                };
                this.listView.bindItem = (element, index) =>
                {
                    if (this.listView.itemsSource[index] is not UnusedProp t)
                        return;
                    var label = element.Q<Label>("itemLabel");
                    label.text = $"   {t.assetPath} : {t.propName}";
                    var button = element.Q<Button>("itemButton");
                    button.clicked += () =>
                    {
                        t.sp.DeleteArrayElementAtIndex(t.propIndex);
                        t.sp.serializedObject.ApplyModifiedProperties();
                        this.UpdateView();
                    };
                };
            }
            this.rootElement.Add(this.listView);
            
            var optionalView = new TwoPaneSplitView(0, 100, TwoPaneSplitViewOrientation.Vertical);
            {
                var detailBox = new Box();
                {
                    var header = new Label("Details");
                    header.style.unityFontStyleAndWeight = FontStyle.Bold;
                    header.style.left = 10f;
                    detailBox.Add(header);
                    var label = new Label("explain what is setting");
                    label.style.whiteSpace = WhiteSpace.Normal;
                    label.style.left = 10f;
                    label.text = DETAILS_MESSAGE;
                    detailBox.Add(label);
                }
                optionalView.Add(detailBox);

                var referenceBox = new Box();
                {
                    this.referenceListView = new ListView();
                    {
                        this.referenceListView.fixedItemHeight = 25f;
                        this.referenceListView.selectedIndicesChanged += this.OnSelectedReferenceChanged;
                        this.referenceListView.selectionType = SelectionType.Single;
                        this.referenceListView.makeItem = () =>
                        {
                            var label = new Label();
                            label.style.unityTextAlign = TextAnchor.MiddleLeft;
                            return label;
                        };
                        this.referenceListView.bindItem = (element, index) =>
                        {
                            var t = this.refEntries[index];
                            if (element is Label label)
                                label.text = $"   {t.groupPath ?? "No entry(Implicit asset)"} > {t.assetPath}";
                        };
                    }
                    this.referenceListView.style.left = 10f;
                    
                    var header = new Label("Referencing Entries")
                    {
                        style =
                        {
                            unityFontStyleAndWeight = FontStyle.Bold,
                            left = 10f,
                            alignSelf = Align.FlexStart
                        }
                    };
                    referenceBox.Add(header);
                    this.referencedBox = new VisualElement();
                    this.referencedBox.style.flexGrow = 1;
                    this.referencedBox.style.flexDirection = FlexDirection.Column;
                    this.referencedBox.style.alignItems = Align.Stretch;
                    referenceBox.Add(this.referencedBox);
                    
                    this.UpdateReferencedView();
                }
                optionalView.Add(referenceBox);
            }
            this.rootElement.Add(optionalView);
        }

        /// <summary>
        /// called when selecting any category
        /// </summary>
        public override void UpdateView()
        {
            this.listView.itemsSource = this.unusedProps;
            this.listView.ClearSelection();
            this.listView.Rebuild();
        }

        /// <summary>
        /// update view for referenced entries
        /// </summary>
        void UpdateReferencedView()
        {
            this.referencedBox.Clear();
            
            var index = this.listView.selectedIndex;
            if (index >= 0 && this.analyzeCache.refEntryDic.TryGetValue(this.unusedProps[index].guid, out var referencedEntries))
            {
                this.UpdateReferencedList(referencedEntries);
            }
            else
            {
                var topSpacer = new VisualElement();
                topSpacer.style.flexGrow = 1;
                this.referencedBox.Add(topSpacer);

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
                        var dup = this.unusedProps[index].refAssetData;
                        var entriesCache = FindReferencedEntries(this.analyzeCache, dup);
                        this.analyzeCache.refEntryDic.Add(dup.guid, entriesCache);
                        this.UpdateReferencedList(entriesCache);
                    };
                }
                this.referencedBox.Add(button);

                var bottomSpacer = new VisualElement();
                bottomSpacer.style.flexGrow = 1;
                this.referencedBox.Add(bottomSpacer);
            }
        }

        /// <summary>
        /// update referenced entries list
        /// </summary>
        /// <param name="entries">referenced entries</param>
        void UpdateReferencedList(List<RefEntry> entries)
        {
            this.refEntries = entries;
            this.referencedBox.Clear();
            this.referenceListView.ClearSelection();
            this.referenceListView.itemsSource = entries;
            this.referenceListView.Rebuild();
            this.referencedBox.style.alignSelf = Align.Auto;
            this.referencedBox.Add(this.referenceListView);
        }
    }
}
