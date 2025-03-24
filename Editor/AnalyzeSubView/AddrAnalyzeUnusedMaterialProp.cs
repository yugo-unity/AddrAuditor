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
        
        class UnusedProp
        {
            public string guid;
            public string assetPath;
            public Material material;
            public SerializedProperty sp;
            public string propName;
            public int propIndex;
            public RefAssetData refAssetData;
            bool initRefAssetData;
            
            public void AddList(List<UnusedProp> list, SerializedProperty sp, int index, string propName, AnalyzeCache analyzeCache)
            {
                this.sp = sp;
                this.propIndex = index;
                this.propName = propName;
                if (this.initRefAssetData == false)
                {
                    this.refAssetData = analyzeCache.refAssets.Find(item => item.guid == this.guid);
                    this.initRefAssetData = true;
                }
                list.Add(this);
            }
        }

        public override bool requireAnalyzeCache => true;
        readonly List<UnusedProp> unusedProps = new();
        readonly List<RefEntry> refEntries = new ();
        
        AnalyzeCache analyzeCache;
        ListView listView, referenceView;
        VisualElement optionalView;
        Label detailsLabel;

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
            
            // // focusing in Project Window
            // Selection.activeObject = propData.material;
            // EditorGUIUtility.PingObject(propData);
            
            FindReferencedEntries(this.refEntries, this.analyzeCache, propData.refAssetData);
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
            this.unusedProps.Clear();
            //var guids = AssetDatabase.FindAssets("t:Material"); // include Packages
            var serachFolder = new string[] { "Assets", };
            var guids = AssetDatabase.FindAssets("t:Material", serachFolder);

            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
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
        }

        /// <summary>
        /// called when created view (only once)
        /// </summary>
        protected override void OnCreateView()
        {   
            this.listView = new ListView();
            this.listView.fixedItemHeight = 25f;
            this.listView.selectedIndicesChanged += this.OnSelectedChanged;
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
            this.rootElement.Add(this.listView);
            
            this.optionalView = new TwoPaneSplitView(0, 200, TwoPaneSplitViewOrientation.Vertical);
            {
                var box = new VisualElement();
                {
                    var header = new Label("Details");
                    header.style.unityFontStyleAndWeight = FontStyle.Bold;
                    box.Add(header);
                    this.detailsLabel = new Label("explain what is setting");
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
                            if (chosenItems.First() is UnusedProp prop)
                            {
                                // focusing in Project Window
                                Selection.activeObject = prop.material;
                                EditorGUIUtility.PingObject(prop.material);
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
            this.listView.itemsSource = this.unusedProps;
            this.listView.ClearSelection();
            this.listView.Rebuild();
        }
    }
}
