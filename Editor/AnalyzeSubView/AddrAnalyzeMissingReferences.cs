using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace AddrAuditor.Editor
{
    /// <summary>
    /// view for missing components/references
    /// </summary>
    class AnalyzeViewMissingReferences : ResultView
    {
        static readonly string DETAILS_MESSAGE = "Missingになっているアセット参照をもつComponentを検出します。該当オブジェクトを確認し、適切に処理してください。\n" +
                                                 "なお、この解析はAddressableに関わらずプロジェクト全体に行われます。";
        
        class MissingAsset
        {
            public string assetPath;
            public string gameObjectPath;

            public MissingAsset(string assetPath, string gameObjectPath)
            {
                assetPath = assetPath;
                gameObjectPath = gameObjectPath;
            }
        }

        readonly List<MissingAsset> results = new();
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
            var ret = this.results[index];
            if (string.IsNullOrEmpty(ret.assetPath))
                return;
            Object obj = null;
            var activeScene = SceneManager.GetActiveScene(); 
            // Project Windowでフォーカスさせる
            if (activeScene.path != ret.assetPath)
                obj = AssetDatabase.LoadAssetAtPath(ret.assetPath, typeof(Object));
            else
                obj = GameObject.Find(ret.gameObjectPath);
            Selection.activeObject = obj;
            EditorGUIUtility.PingObject(obj);
        }

        /// <summary>
        /// called when require to analyze
        /// </summary>
        /// <param name="cache">build cache that created by AddrAnalyzeWindow</param>
        public override void Analyze(AnalyzeCache cache)
        {
            this.results.Clear();
            var paths = AssetDatabase.GetAllAssetPaths();
            //foreach (var path in paths)
            for (var i = 0; i < paths.Length; ++i)
            {
                var path = paths[i];
                EditorUtility.DisplayCancelableProgressBar("Searching Missing References...", path, (float)i/paths.Length);
                if (path.Contains("Packages"))
                    continue;
                var o = AssetDatabase.LoadMainAssetAtPath(path);
                switch (o)
                {
                    case GameObject go:
                        DigMissingComponents(results, path, go.name, go);
                        break;
                    case SceneAsset:
                    {
                        var needToUnload = false;
                        var scene = SceneManager.GetActiveScene();
                        if (scene.path != path)
                        {
                            needToUnload = true;
                            scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                        }
                        if (scene.IsValid())
                        {
                            var gameObjects = scene.GetRootGameObjects();
                            foreach (var go in gameObjects)
                                DigMissingComponents(results, path, go.name, go);   
                        }
                        if (needToUnload)
                            EditorSceneManager.CloseScene(scene, true);
                        break;
                    }
                }
            }
            EditorUtility.ClearProgressBar();
        }

        /// <summary>
        /// called when created view (only once)
        /// </summary>
        protected override void OnCreateView()
        {
            var box = new Box();
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
            this.rootElement.Add(box);
            
            box = new Box();
            {
                this.listView = new ListView();
                this.listView.fixedItemHeight = 25f;
                this.listView.selectedIndicesChanged += this.OnSelectedChanged;
                this.listView.selectionType = SelectionType.Single;
                this.listView.makeItem = () =>
                {
                    var label = new Label();
                    label.style.unityTextAlign = TextAnchor.MiddleLeft;
                    return label;
                };
                this.listView.bindItem = (element, index) =>
                {
                    if (this.listView.itemsSource is List<string> missingPath)
                    {
                        if (element is Label label)
                            label.text = missingPath[index] ?? "Null Object";
                        // lost selected color when setting backgroundColor（css is required if we want）
                        // if (index % 2 == 0)
                        //     element.style.backgroundColor = new StyleColor(new Color(0.24f, 0.24f, 0.24f));
                        // else
                        //     element.style.backgroundColor = new StyleColor(new Color(0.21f, 0.21f, 0.21f));
                    }
                };
                box.Add(this.listView);
            }
            this.rootElement.Add(box);
        }

        /// <summary>
        /// called when selecting any category
        /// </summary>
        public override void UpdateView()
        {
            var labels = new List<string>(results.Count);
            foreach (var t in results)
                labels.Add($"   {t.assetPath} : {t.gameObjectPath}");
            this.listView.ClearSelection();
            this.listView.itemsSource = labels;
            this.listView.Rebuild();
        }

        /// <summary>
        /// find Missing References recursively
        /// </summary>
        /// <param name="results">results</param>
        /// <param name="assetPath">file path for selected asset</param>
        /// <param name="objPath">gameobject path for selected object</param>
        /// <param name="gameObject">selected object</param>
        static void DigMissingComponents(List<MissingAsset> results, string assetPath, string objPath, GameObject gameObject)
        {
            // check components in current gameobject
            var components = gameObject.GetComponents<Component>();
            foreach (var component in components)
            {
                if (component != null)
                    continue;
                results.Add(new MissingAsset(assetPath, objPath));
            }

            // check children
            var transform = gameObject.transform;
            for (var c = 0; c < transform.childCount; c++)
            {
                var t = transform.GetChild(c);
                var pt = PrefabUtility.GetPrefabAssetType(t.gameObject);
                var nextObjPath = $"{objPath}/{t.name}";
                if (pt == PrefabAssetType.MissingAsset)
                    results.Add(new MissingAsset(assetPath, objPath));
                else
                    DigMissingComponents(results, assetPath, nextObjPath, t.gameObject);
            }
        }
    }
}
