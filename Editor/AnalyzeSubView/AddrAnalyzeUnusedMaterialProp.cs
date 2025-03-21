using System.Collections.Generic;
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
            public string assetPath;
            public Material material;
            public SerializedProperty sp;
            public string propName;
            public int propIndex;
        }

        readonly List<UnusedProp> unusedProps = new();
        ListView listView;
        Label detailsLabel;

        /// <summary>
        /// Callback when any column is selected
        /// </summary>
        void OnSelectedChanged(IEnumerable<int> selectedItems)
        {
            if (selectedItems is not List<int> indexList || indexList.Count == 0)
                return;
            var index = indexList[0];
            if (string.IsNullOrEmpty(this.unusedProps[index].assetPath))
                return;
            // focusing in Project Window
            var obj = this.unusedProps[index].material;
            Selection.activeObject = obj;
            EditorGUIUtility.PingObject(obj);
        }

        /// <summary>
        /// called when require to analyze
        /// </summary>
        /// <param name="cache">build cache that created by AddrAnalyzeWindow</param>
        public override void Analyze(AnalyzeCache cache)
        {
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
                for (var i = texEnvSp.arraySize - 1; i >= 0; i--)
                {
                    var propName = texEnvSp.GetArrayElementAtIndex(i).FindPropertyRelative("first").stringValue;

                    if (!properties.Contains(propName))
                        AddUnusedList(this.unusedProps, path, m, texEnvSp, i, propName);
                }

                var floatsSp = sp.FindPropertyRelative("m_Floats");
                for (var i = floatsSp.arraySize - 1; i >= 0; i--)
                {
                    var propName = floatsSp.GetArrayElementAtIndex(i).FindPropertyRelative("first").stringValue;
                    if (!properties.Contains(propName))
                        AddUnusedList(this.unusedProps, path, m, floatsSp, i, propName);
                }

                var intSp = sp.FindPropertyRelative("m_Ints");
                for (var i = intSp.arraySize - 1; i >= 0; i--)
                {
                    var propName = intSp.GetArrayElementAtIndex(i).FindPropertyRelative("first").stringValue;
                    if (!properties.Contains(propName))
                        AddUnusedList(this.unusedProps, path, m, intSp, i, propName);
                }

                var colorsSp = sp.FindPropertyRelative("m_Colors");
                for (var i = colorsSp.arraySize - 1; i >= 0; i--)
                {
                    var propName = colorsSp.GetArrayElementAtIndex(i).FindPropertyRelative("first").stringValue;
                    if (!properties.Contains(propName))
                        AddUnusedList(this.unusedProps, path, m, colorsSp, i, propName);
                }
            }
        }

        /// <summary>
        /// called when created view (only once)
        /// </summary>
        protected override void OnCreateView()
        {
            var box = new VisualElement();
            var header = new Label("Details");
            header.style.unityFontStyleAndWeight = FontStyle.Bold;
            box.Add(header);
            this.detailsLabel = new Label("explain what is setting");
            this.detailsLabel.style.whiteSpace = WhiteSpace.Normal;
            this.detailsLabel.text = DETAILS_MESSAGE;
            box.Add(this.detailsLabel);
            foreach (var child in box.Children())
                child.style.left = 10f;
            this.rootElement.Add(box);
            
            this.listView = new ListView();
            this.listView.fixedItemHeight = 25f;
            this.listView.selectedIndicesChanged += this.OnSelectedChanged;
            this.listView.selectionType = SelectionType.Single;
            this.listView.makeItem = () =>
            {
                // 項目の基礎を構築（Label と Button を含む）
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
        /// Missingが見つかったのでリスト登録
        /// </summary>
        /// <param name="list">登録するリスト</param>
        /// <param name="path">該当のアセットパス</param>
        /// <param name="mat">該当のMaterial</param>
        /// <param name="sp">該当のSerializedProperty</param>
        /// <param name="index">該当のSerializedPropertyのインデックス</param>
        /// <param name="propName">該当のプロパティ名</param>
        static void AddUnusedList(List<UnusedProp> list, string path, Material mat, SerializedProperty sp, int index, string propName)
        {
            list.Add(new UnusedProp()
            {
                assetPath = path,
                material = mat,
                sp = sp,
                propIndex = index,
                propName = propName,
            });
        }
    }
}
