using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;

namespace AddrAuditor.Editor
{
    class UnusedProp
    {
        public string assetPath;
        public Material material;
        public SerializedProperty sp;
        public string propName;
        public int propIndex;
    }

    /// <summary>
    /// 使用されていないMaterialPropertyの検出
    /// </summary>
    class AnalyzeViewUnusedMaterialProp : SubCategoryView
    {
        readonly List<UnusedProp> unusedProps = new();
        ListView listView;
        Label detailsLabel; // カテゴリの説明文

        /// <summary>
        /// 選択された時のコールバック
        /// </summary>
        void OnSelectedChanged(IEnumerable<int> selectedItems)
        {
            if (selectedItems is not List<int> indexList)
                return;
            if (indexList.Count == 0)
                return;

            var index = indexList[0];
            if (string.IsNullOrEmpty(this.unusedProps[index].assetPath))
                return;
            // Project Windowでフォーカスさせる
            var obj = this.unusedProps[index].material;
            Selection.activeObject = obj;
            EditorGUIUtility.PingObject(obj);
        }

        /// <summary>
        /// 解析処理
        /// </summary>
        public override void Analyze()
        {
            DigMissingComponents(this.unusedProps);
        }

        /// <summary>
        /// GUI構築
        /// </summary>
        protected override void OnCreateView()
        {
            var box = new VisualElement();
            var header = new Label("Details");
            header.style.unityFontStyleAndWeight = FontStyle.Bold;
            box.Add(header);
            this.detailsLabel = new Label("explain what is setting");
            this.detailsLabel.style.whiteSpace = WhiteSpace.Normal;
            this.detailsLabel.text = "Materialに含まれる未使用のPropertyを検出します。\n" +
                                     "MaterialのShaderを変更した際、変更前に使用されていたPropertyは自動で削除されません。\n" +
                                     "ランタイムでShaderを切り替えるようなケースがない限り削除した方がベターです。";
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
        /// 表示の更新
        /// カテゴリが選択された時に呼ばれる
        /// </summary>
        public override void UpdateView()
        {
            this.listView.itemsSource = this.unusedProps;
            this.listView.ClearSelection();
            this.listView.Rebuild();
        }

        static void DigMissingComponents(List<UnusedProp> results)
        {
            results.Clear();
            
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
                        AddUnusedList(results, path, m, texEnvSp, i, propName);
                }

                var floatsSp = sp.FindPropertyRelative("m_Floats");
                for (var i = floatsSp.arraySize - 1; i >= 0; i--)
                {
                    var propName = floatsSp.GetArrayElementAtIndex(i).FindPropertyRelative("first").stringValue;
                    if (!properties.Contains(propName))
                        AddUnusedList(results, path, m, floatsSp, i, propName);
                }

                var intSp = sp.FindPropertyRelative("m_Ints");
                for (var i = intSp.arraySize - 1; i >= 0; i--)
                {
                    var propName = intSp.GetArrayElementAtIndex(i).FindPropertyRelative("first").stringValue;
                    if (!properties.Contains(propName))
                        AddUnusedList(results, path, m, intSp, i, propName);
                }

                var colorsSp = sp.FindPropertyRelative("m_Colors");
                for (var i = colorsSp.arraySize - 1; i >= 0; i--)
                {
                    var propName = colorsSp.GetArrayElementAtIndex(i).FindPropertyRelative("first").stringValue;
                    if (!properties.Contains(propName))
                        AddUnusedList(results, path, m, colorsSp, i, propName);
                }
            }
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
