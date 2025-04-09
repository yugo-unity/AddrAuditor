using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor;
using UnityEditor.Build.Profile;

namespace AddrAuditor.Editor
{
    /// <summary>
    /// view for duplicated assets in built-in assets
    /// </summary>
    class AnalyzeViewBuiltInAssets : ResultView
    {
        static readonly string DETAILS_MESSAGE = "Built-inとAssetBundleで重複して含まれているアセットを検出します。\n" + 
                                                 "またBuilt-inでしか利用しないアセットをAddressableに登録していないか確認してください。\n" +
                                                 "\n" +
                                                 "Detecting assets that are included in duplicate in Built-in and AssetBundle.\n" +
                                                 "Also, it will be better to check whether any assets used in Built-in only have not been registered in Addressable.";
            
        struct DuplicateAsset
        {
            public RefAssetData refAssetData; // 重複しているアセット
            public string builtInAsset; // 参照しているBuiltInアセット 
        }
        
        public override bool requireAnalyzeCache => true;
        readonly List<DuplicateAsset> duplications = new ();
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
            if (string.IsNullOrEmpty(dup.refAssetData.path))
                return;
            
            // focusing in Project Window
            var obj = AssetDatabase.LoadMainAssetAtPath(dup.refAssetData.path);
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
            this.duplications.Clear();
            
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

            var optionalView = new TwoPaneSplitView(0, REF_VIEW_DETAIL_HEIGHT, TwoPaneSplitViewOrientation.Vertical);
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
            if (index >= 0 && this.analyzeCache.refEntryDic.TryGetValue(this.duplications[index].refAssetData.guid, out var referencedEntries))
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
                        var entriesCache = FindReferencedEntries(this.analyzeCache, dup.refAssetData);
                        this.analyzeCache.refEntryDic.Add(dup.refAssetData.guid, entriesCache);
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
