using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor;
using UnityEditor.Build.Profile;

namespace AddrAuditor.Editor
{
    /// <summary>
    /// 重複しているアセットの検出
    /// </summary>
    class AnalyzeViewBuiltInAssets : SubCategoryView
    {
        static readonly string DETAILS_MESSAGE = "Built-inとAssetBundleで重複して含まれているアセットを検出します。\n" +
                                                 "Built-inには極力アセットを含めないよう適切に対応してください。\n" +
                                                 "またBuilt-inでしか利用しないアセットをAddressableに登録していないか確認してください。";
            
        struct DuplicateAsset
        {
            public RefAssetData refAssetData; // 重複しているアセット
            public string builtInAsset; // 参照しているBuiltInアセット 
        }
        
        public override bool requireAnalyzeCache => true;
        readonly List<DuplicateAsset> duplications = new ();
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
            if (!selectedItems.Any())
                return;
            var index = selectedItems.First();
            var dup = this.duplications[index];
            if (string.IsNullOrEmpty(dup.refAssetData.path))
                return;
            
            // // focusing in Project Window
            // var obj = AssetDatabase.LoadMainAssetAtPath(dup.refAssetData.path);
            // Selection.activeObject = obj;
            // EditorGUIUtility.PingObject(obj);
            
            FindReferencedEntries(this.refEntries, this.analyzeCache, dup.refAssetData);
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
            
            // // focusing in Project Window
            // var obj = AssetDatabase.LoadMainAssetAtPath(this.refEntries[index].assetPath);
            // Selection.activeObject = obj;
            // EditorGUIUtility.PingObject(obj);
        }

        /// <summary>
        /// 解析処理
        /// </summary>
        public override void Analyze(AnalyzeCache cache)
        {
            this.analyzeCache = cache;
            this.duplications.Clear();
            this.refEntries.Clear();
            
            // [[explicit assetsが対象] - allEntries]
            // 1. Built-in sceneに含まれているか
            var buildProfile = BuildProfile.GetActiveBuildProfile();
            var builtInScenes = buildProfile?.scenes ?? EditorBuildSettings.scenes;
            foreach (var scene in builtInScenes)
            {
                if (!scene.enabled)
                    continue;
                
                var dep = AssetDatabase.GetDependencies(scene.path, true);
                var dupImplicit = cache.refAssets.Where(
                    (param) =>
                    {
                        // TODO: test
                        if (param.path.Contains("wake_up"))
                        {
                            int hoge = 0;
                            ++hoge;
                        }
                        return dep.Contains(param.path);
                    }).ToArray();
                foreach (var dup in dupImplicit)
                {
                    this.duplications.Add(new DuplicateAsset()
                    {
                        refAssetData = dup,
                        builtInAsset = scene.path,
                    });
                }
            }
            
            // 2. Resourcesに含まれているか
            if (cache != null)
            {
                foreach (var param in cache.refAssets)
                {
                    if (!param.path.Contains("/Resources/"))
                        continue;
                    this.duplications.Add(new DuplicateAsset()
                    {
                        refAssetData = param,
                        builtInAsset = "Resources",
                    });
                }
            }
            
            // 3. GraphicsSettings/Always included shader & Preloaded Shaders に含まれているか
            Debug.LogWarning("3. Always included & Preloaded Shaders");
            var graphicsSettings = new SerializedObject(UnityEngine.Rendering.GraphicsSettings.GetGraphicsSettings());
            var alwaysIncludedShaders = graphicsSettings.FindProperty("m_AlwaysIncludedShaders");
            for (var i = 0; i < alwaysIncludedShaders.arraySize; i++)
            {
                // 各シェーダーを取得する
                var shader = alwaysIncludedShaders.GetArrayElementAtIndex(i).objectReferenceValue as Shader;
                if (shader != null)
                {
                    Debug.Log(shader.name);
                }
            }

            // 4. PlayerSettings/Preloaded Assetsに含まれているか
            Debug.LogWarning("4. Preloaded Assets");
            var preloadedAssets = PlayerSettings.GetPreloadedAssets();
            foreach (var asset in preloadedAssets)
            {
                Debug.Log(AssetDatabase.GetAssetPath(asset));
            }
            
            // 5. SRP Assets
            Debug.LogWarning("5. Default SRP Assets");
            var defaultRPAsset = UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline;
            var path = AssetDatabase.GetAssetPath(defaultRPAsset);
            var dependencyPaths = AssetDatabase.GetDependencies(path, true);
            foreach (var asset in dependencyPaths)
            {
                Debug.Log(asset);
            }
            
            // 6. QualitySettings SRP Assets
            Debug.LogWarning("6. QualitySettings SRP Assets");
            var buildTargetGroup = BuildPipeline.GetBuildTargetGroup(EditorUserBuildSettings.activeBuildTarget);
            for (var i = 0; i < QualitySettings.names.Length; i++)
            {
                if (!QualitySettings.IsPlatformIncluded(buildTargetGroup.ToString(), i))
                    continue;
                var rpAsset = QualitySettings.GetRenderPipelineAssetAt(i);
                Debug.Log(AssetDatabase.GetAssetPath(rpAsset));
            }
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
                this.listView.itemsChosen += chosenItems =>
                {
                    if (!chosenItems.Any())
                        return;
                    if (chosenItems.First() is DuplicateAsset dupAsset)
                    {
                        // focusing in Project Window
                        var obj = AssetDatabase.LoadMainAssetAtPath(dupAsset.refAssetData.path);
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
                    label.text = $"   {t.builtInAsset} > {t.refAssetData.path}";
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
                    // TODO
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
        /// 表示の更新
        /// カテゴリが選択された時に呼ばれる
        /// </summary>
        public override void UpdateView()
        {
            this.listView.ClearSelection();
            this.listView.itemsSource = this.duplications;
            this.listView.Rebuild();
        }
    }
}
