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
            foreach (var path in paths)
            {
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
                    // backgroundColorを設定するとselected Colorが設定できない（cssを要求される）
                    // if (index % 2 == 0)
                    //     element.style.backgroundColor = new StyleColor(new Color(0.24f, 0.24f, 0.24f));
                    // else
                    //     element.style.backgroundColor = new StyleColor(new Color(0.21f, 0.21f, 0.21f));
                }
            };
            this.rootElement.Add(this.listView);
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
        /// Missing参照を掘り出す
        /// </summary>
        /// <param name="missingList">検出結果</param>
        /// <param name="assetPath">調べてるアセットのファイルパス</param>
        /// <param name="objPath">調べてるオブジェクトの階層パス</param>
        /// <param name="gameObject">調べるGameObject</param>
        static void DigMissingComponents(List<MissingAsset> missingList, string assetPath, string objPath, GameObject gameObject)
        {
            // 現在のGameObjectにアタッチされているコンポーネントの確認
            var components = gameObject.GetComponents<Component>();
            foreach (var component in components)
            {
                if (component != null)
                    continue;
                AddMissingList(missingList, assetPath, objPath);
            }

            // 子のGameObjectを再帰的に検索
            var transform = gameObject.transform;
            for (var c = 0; c < transform.childCount; c++)
            {
                var t = transform.GetChild(c);
                var pt = PrefabUtility.GetPrefabAssetType(t.gameObject);
                var nextObjPath = $"{objPath}/{t.name}";
                if (pt == PrefabAssetType.MissingAsset)
                    AddMissingList(missingList, assetPath, nextObjPath);
                else
                    DigMissingComponents(missingList, assetPath, nextObjPath, t.gameObject);
            }
        }

        /// <summary>
        /// Missingが見つかったのでリスト登録
        /// </summary>
        /// <param name="list">登録するリスト</param>
        /// <param name="path">該当のアセットパス</param>
        /// <param name="objPath">該当のGameObjectパス</param>
        /// <param name="obj">該当のGameObject</param>
        static void AddMissingList(List<MissingAsset> list, string path, string objPath)
        {
            list.Add(new MissingAsset()
            {
                assetPath = path,
                gameObjectPath = objPath,
            });
        }
    }
}
