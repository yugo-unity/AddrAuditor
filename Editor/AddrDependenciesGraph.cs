/***********************************************************************************************************
 * AddrDependenciesGraph.cs
 * Copyright (c) Yugo Fujioka - Unity Technologies Japan K.K.
 * 
 * Licensed under the Unity Companion License for Unity-dependent projects--see Unity Companion License.
 * https://unity.com/legal/licenses/unity-companion-license
 * Unless expressly provided otherwise, the Software under this license is made available strictly
 * on an "AS IS" BASIS WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED.
 * Please review the license for details on these and other terms and conditions.
***********************************************************************************************************/

using System.Collections.Generic;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build.BuildPipelineTasks;
using UnityEditor.AddressableAssets.Build.DataBuilders;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEditor.Build.Content;
using UnityEditor.Build.Pipeline;
using UnityEditor.Experimental.GraphView;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace AddrAuditor.Editor
{
    /// <summary>
    /// Addressablesで自動解決した依存関係をノードグラフで確認する
    /// </summary>
    internal class AddrDependenciesGraph : EditorWindow
    {
        private GraphView graphView = null;
        private DependenciesRule bundleRule = new ();
        private AddrAutoGroupingSettings groupingSettings;
        private GraphSetting graphSetting;
        private Box mainBox;


        [System.Serializable]
        public class IgnorePrefix
        {
            public string text;
        }

        [SerializeField] private List<IgnorePrefix> ignorePrefixList = new List<IgnorePrefix>();

        void UpdateBundleDependencies(string bundleName, BundlesGraph.TYPE graphType = BundlesGraph.TYPE.BUNDLE_DEPENDENCE)
        {
            if (this.mainBox is not null)
                this.rootVisualElement.Remove(this.mainBox);
            if (this.graphView is not null)
                this.rootVisualElement.Remove(this.graphView);
            this.graphView = new BundlesGraph(graphType, this.graphSetting, this.bundleRule, bundleName);
            this.rootVisualElement.Add(this.graphView);
            this.rootVisualElement.Add(this.mainBox);
        }

        #region MAIN LAYOUT

        public void CreateGUI()
        {
            this.groupingSettings = AssetDatabase.LoadAssetAtPath<AddrAutoGroupingSettings>(AddrAutoGrouping.SETTINGS_PATH);
            var settings = AddressableAssetSettingsDefaultObject.Settings;

            this.mainBox = new Box();
            this.mainBox.style.width = 300f;
            this.rootVisualElement.Add(this.mainBox);

            AddrUtility.CreateSpace(this.mainBox);

            // Select Group
            var groupList = settings.groups.FindAll(group =>
            {
                var schema = group.GetSchema<BundledAssetGroupSchema>();
                if (schema != null && schema.IncludeInBuild)
                    return (schema.IncludeAddressInCatalog || schema.IncludeGUIDInCatalog ||
                            schema.IncludeLabelsInCatalog);
                return false;
            });
            var selectedGroupField = new PopupField<AddressableAssetGroup>("Selected Group", groupList, 0,
                value => value.name,
                value => value.name);
            selectedGroupField.name = "SelectedGroup";
            selectedGroupField.tooltip = "表示するグループ\n\n The group what you want to analyze.";
            this.mainBox.Add(selectedGroupField);

            // Select Entry
            var entryList = new List<AddressableAssetEntry>() { null };
            entryList.AddRange(selectedGroupField.value.entries);
            var selectedEntryField = new PopupField<AddressableAssetEntry>("Selected Entry", entryList, 0,
                value => value == null ? "all" : value.address,
                value => value == null ? "all" : value.address);
            selectedEntryField.name = "SelectedEntry";
            selectedEntryField.tooltip = "指定グループの中の特定のエントリに表示範囲を限定します\n\n The specific entry in the group what you want to analyze.";
            this.mainBox.Add(selectedEntryField);

            // Options
            var residentNodeToggle = AddrUtility.CreateToggle(this.mainBox,
                "Visible Resident Groups",
                "常駐アセットグループを表示します。\n\n If disabled, resident nodes are hidden.",
                true);
            var sharedNodeToggle = AddrUtility.CreateToggle(this.mainBox,
                "Visible Shared Groups",
                "自動生成されたSharedグループを表示します。\n\n If disabled, shared nodes that are created automatically are hidden.",
                true);
            var shaderNodeToggle = AddrUtility.CreateToggle(this.mainBox,
                "Visible Shader Group",
                "自動生成されたShaderグループを表示します。\n\n If disabled, shader node that are created automatically is hidden.",
                true);
            var depthRoot = new VisualElement();
            depthRoot.style.flexDirection = FlexDirection.Row;
            this.mainBox.Add(depthRoot);
            const int defaultDepth = 0;
            var depthSlider = AddrUtility.CreateSliderInt(depthRoot,
                "Visible Depth",
                "依存関係の表示制限です。0で無効となります。\n\n Limit the number of dependency recursions. 0 means no limit.",
                defaultDepth, 0, 5);
            depthSlider.style.width = 250f;
            var depthInteger = AddrUtility.CreateInteger(depthRoot,
                string.Empty, string.Empty,
                defaultDepth);
            depthInteger.style.width = 30f;
            depthInteger.RegisterValueChangedCallback((ev) =>
            {
                var val = Mathf.Clamp(ev.newValue, 0, 3);
                depthInteger.value = val;
                depthSlider.SetValueWithoutNotify(val);
            });
            depthSlider.RegisterValueChangedCallback((ev) => { depthInteger.SetValueWithoutNotify(ev.newValue); });
            this.CreateStringList(this.mainBox,
                "Ignore Keyword",
                "特定の文字列をグループ名に含む場合にノード表示を省略します。\n\n Hide the groups if their name contain string here.");

            // Groupが変更されたらEntryのリストを更新
            selectedGroupField.RegisterCallback<ChangeEvent<AddressableAssetGroup>>((ev) =>
            {
                entryList.Clear();
                entryList.Add(null);
                foreach (var entry in ev.newValue.entries)
                    entryList.Add(entry);
                selectedEntryField.index = 0;
            });

            // Space
            AddrUtility.CreateSpace(this.mainBox);

            // Clear Analysis Button
            {
                var button = AddrUtility.CreateButton(this.mainBox,
                    "Clear Addressables Analysis",
                    "設定やエントリが更新された際にキャッシュをクリアしてください。\n\n" +
                    "You should clear the cache if settings or entries are updated.");
                button.clicked += () =>
                {
                    this.bundleRule.Clear();
                    if (this.graphView != null)
                    {
                        this.rootVisualElement.Remove(this.graphView);
                        this.graphView = null;
                    }
                };
            }

            // Space
            AddrUtility.CreateSpace(this.mainBox);

            // Bundle-Dependencies Button
            {
                var button = AddrUtility.CreateButton(this.mainBox, 
                    "View Bundle-Dependencies",
                    "暗黙的にロードされるAssetBundleを確認できます\n\n" +
                    "You can check assetbundles that are loaded implicitly.");
                button.clicked += () =>
                {
                    this.bundleRule.Execute();

                    var selectedEntry = selectedEntryField.index > 0 ? selectedEntryField.value : null;
                    this.graphSetting = new GraphSetting()
                    {
                        rootGraph = this,
                        residentGroupGuid = this.groupingSettings.residentGroupGUID,
                        selectedGroup = selectedGroupField.value,
                        selectedEntry = selectedEntry,
                        enabledShaderNode = shaderNodeToggle.value,
                        enabledSharedNode = sharedNodeToggle.value,
                        enabledResidentNode = residentNodeToggle.value,
                        enabledDepth = depthSlider.value,
                        ignoreList = new List<IgnorePrefix>(this.ignorePrefixList),
                    };
                    this.UpdateBundleDependencies("", BundlesGraph.TYPE.BUNDLE_DEPENDENCE);
                };
            }

            // Space
            AddrUtility.CreateSpace(this.mainBox);

            // Asset-Dependencies Button
            {
                var button = AddrUtility.CreateButton(this.mainBox, 
                    "View Asset-Dependencies",
                    "AssetBundleに含まれるAssetの依存関係を表示します\n\n" +
                    "You can check dependencies between assets.");
                button.clicked += () =>
                {
                    this.bundleRule.Execute();

                    var selectedEntry = selectedEntryField.index > 0 ? selectedEntryField.value : null;
                    this.graphSetting = new GraphSetting()
                    {
                        rootGraph = this,
                        selectedGroup = selectedGroupField.value,
                        selectedEntry = selectedEntry,
                        enabledShaderNode = shaderNodeToggle.value,
                        enabledSharedNode = sharedNodeToggle.value,
                        enabledResidentNode = residentNodeToggle.value,
                        enabledDepth = depthSlider.value,
                        ignoreList = new List<IgnorePrefix>(this.ignorePrefixList),
                    };
                    this.UpdateBundleDependencies("", BundlesGraph.TYPE.ASSET_DEPENDENCE);
                };
            }

            AddrUtility.CreateSpace(this.mainBox);
        }

        void CreateStringList(VisualElement root, string title, string tooltip)
        {
            root.Add(new Label() { text = title, tooltip = tooltip, style = { paddingLeft = 4f } });

            var so = new SerializedObject(this);
            // NOTE:
            // GraphViewのNodeだとStyleを変更してもListViewを追加するとForcus範囲が縦いっぱいになる不具合がある
            // GraphViewやめてUIElements.Boxにする
            var listView = new ListView
            {
                bindingPath = "ignorePrefixList",
                reorderable = true,
                showAddRemoveFooter = true,
                showBorder = true,
                showFoldoutHeader = false,
                showBoundCollectionSize = false,
                makeItem = () =>
                {
                    var element = new VisualElement();
                    var textField = new TextField
                    {
                        bindingPath = "text",
                    };
                    element.Add(textField);
                    return element;
                }
            };
            listView.Bind(so);
            root.Add(listView);
        }

        #endregion


        #region BUNDLE RULE

        /// <summary>
        ///  Analyze
        /// </summary>
        public class DependenciesRule
        {
            public AddressableAssetsBuildContext context;
            public ExtractDataTask extractData;
            public List<AssetBundleBuild> allBundleInputDefs { get; private set; }

            public delegate bool IsPathCallback(string path);

            public void Execute()
            {
                var settings = AddressableAssetSettingsDefaultObject.Settings;

                if (this.context == null)
                {
                    this.allBundleInputDefs = new List<AssetBundleBuild>();
                    var bundleToAssetGroup = new Dictionary<string, string>();
                    AddrUtility.CalculateInputDefinitions(settings, this.allBundleInputDefs, bundleToAssetGroup);
                    this.context = AddrUtility.GetBuildContext(settings, bundleToAssetGroup);
                    this.extractData = new ExtractDataTask();
                    var exitCode = AddrUtility.RefleshBuild(settings, this.allBundleInputDefs, this.extractData, this.context);
                    if (exitCode < ReturnCode.Success)
                    {
                        Debug.LogError($"Analyze build failed. {exitCode}");
                    }
                }
            }

            public void Clear()
            {
                this.context = null;
                this.extractData = null;
                this.allBundleInputDefs = null;
            }
        }

        #endregion


        #region NODE

        /// <summary>
        /// ノード拡張
        /// </summary>
        class BundleNode : Node
        {
            AddrDependenciesGraph graph;
            public string bundleName = string.Empty;
            public Dictionary<string, Port> input = new (); // InputContainerに登録されているPort
            public Dictionary<string, Port> output = new (); // OutputContainerに登録されているPort

            public Dictionary<string, GUID> assetGuid = new (); // Portに登録されているAssetのGUID（依存関係で使うのでキャッシュ）

            public Dictionary<Port, HashSet<Port>> connectTo = new (); // 接続されたPort
            public Dictionary<Port, List<FlowingEdge>> edgeTo = new (); // 接続されたEdge

            public Dictionary<Port, List<FlowingEdge>> edgeFrom = new (); // 接続されたEdge

            public BundleNode(AddrDependenciesGraph graph)
            {
                this.graph = graph;
                this.capabilities &= ~Capabilities.Deletable; // 削除を禁止
            }

            public override void OnSelected()
            {
                base.OnSelected();

                foreach (var pair in edgeTo)
                {
                    foreach (var edge in pair.Value)
                        edge.activeFlow = true;
                }

                foreach (var pair in edgeFrom)
                {
                    foreach (var edge in pair.Value)
                        //edge.selected = true; // 色をOutputとは変える
                        edge.activeFlow = true;
                }
            }

            public override void OnUnselected()
            {
                base.OnUnselected();

                foreach (var pair in edgeTo)
                {
                    foreach (var edge in pair.Value)
                        edge.activeFlow = false;
                }

                foreach (var pair in edgeFrom)
                {
                    foreach (var edge in pair.Value)
                        //edge.selected = false;
                        edge.activeFlow = false;
                }
            }
            
            // 右クリックメニューをカスタマイズ
            public override void BuildContextualMenu(ContextualMenuPopulateEvent evt)
            {
                //base.BuildContextualMenu(evt); // no need "Disconnect All"
                evt.menu.AppendAction("Focus the dependencies on this bundle", FocusNode);
            }
            // カスタムアクション1の処理
            private void FocusNode(DropdownMenuAction action)
            {
                graph.UpdateBundleDependencies(this.bundleName);
            }
        }

        #endregion


        #region GraphView

        internal struct GraphSetting
        {
            public string residentGroupGuid;
            public AddrDependenciesGraph rootGraph;
            public AddressableAssetGroup selectedGroup;
            public AddressableAssetEntry selectedEntry;
            public bool enabledResidentNode;
            public bool enabledShaderNode;
            public bool enabledSharedNode;
            public int enabledDepth;
            public List<IgnorePrefix> ignoreList;
        }

        /// <summary>
        /// Graph表示
        /// </summary>
        internal class BundlesGraph : GraphView
        {
            public enum TYPE
            {
                BUNDLE_DEPENDENCE, // bundle間の依存関係
                ASSET_DEPENDENCE, // bundle内のAsset間の依存関係
            }

            const float NODE_OFFSET_H = 140f;
            const float NODE_OFFSET_V = 20f;

            //BundleNode shaderNode, builtinNode;
            List<FlowingEdge> allEdges = new ();
            Dictionary<string, BundleNode> bundleNodes = new ();
            List<string> explicitNodes = new ();
            GraphSetting graphSetting;

            // TODO: リファクタ
            /// <summary>
            /// 指定ノード
            /// </summary>
            /// <param name="self"></param>
            /// <param name="bundleName"></param>
            /// <param name="focusBundleName"></param>
            /// <param name="parentStack"></param>
            /// <param name="enabledNodes"></param>
            /// <param name="context"></param>
            void CheckNodeName(bool self, string bundleName, string focusBundleName, Stack<string> parentStack, HashSet<string> enabledNodes, AddressableAssetsBuildContext context)
            {
                if (!self && bundleName == focusBundleName)
                {
                    foreach (var stack in parentStack)
                        enabledNodes.Add(stack);
                    return;
                }
                
                if (!self)
                    parentStack.Push(bundleName);
                else
                    enabledNodes.Add(bundleName);
                if (this.bundleNodes.TryGetValue(bundleName, out var node))
                {
                    if (context.bundleToImmediateBundleDependencies.TryGetValue(bundleName, out var depBundleNames))
                    {
                        foreach (var depBundleName in depBundleNames)
                        {
                            // 自身は無視
                            if (depBundleName == bundleName)
                                continue;
                            // 循環参照
                            if (parentStack.Contains(depBundleName))
                                continue;
                            CheckNodeName(self, depBundleName, focusBundleName, parentStack, enabledNodes, context);
                        }
                    }
                    if (!self)
                        parentStack.Pop();
                }
            }
            /// <summary>
            /// Bundleの全依存関係
            /// </summary>
            public BundlesGraph(TYPE type, GraphSetting setting, DependenciesRule rule, string focusBundleName)
            {
                this.graphSetting = setting;

                switch (type)
                {
                    case TYPE.BUNDLE_DEPENDENCE:
                        this.ViewBundles(rule, focusBundleName);
                        break;
                    case TYPE.ASSET_DEPENDENCE:
                        this.ViewEntries(rule, focusBundleName);
                        break;
                }

                // NOTE: レイアウトが一旦完了しないとノードサイズがとれないのでViewの描画前コールバックに仕込む
                this.RegisterCallback<GeometryChangedEvent>((ev) =>
                {
                    var parentStack = new Stack<string>(); // 親ノード
                    var enabledNodes = new HashSet<string>();
                    if (!string.IsNullOrEmpty(focusBundleName))
                    {
                        enabledNodes.Add(focusBundleName);
                        CheckNodeName(true, focusBundleName, focusBundleName, parentStack, enabledNodes, rule.context);
                        
                        foreach (var bundleName in this.explicitNodes)
                        {
                            parentStack.Clear();
                            CheckNodeName(false, bundleName, focusBundleName, parentStack, enabledNodes, rule.context);
                        }

                        // 削除
                        var deleteKeys = new List<string>(bundleNodes.Keys.Count);
                        foreach (var node in this.bundleNodes)
                        {
                            if (enabledNodes.Contains(node.Key))
                                continue;

                            foreach (var nodeEdges in node.Value.edgeFrom.Values)
                            {
                                foreach (var edge in nodeEdges)
                                    this.RemoveElement(edge);
                            }

                            foreach (var nodeEdges in node.Value.edgeTo.Values)
                            {
                                foreach (var edge in nodeEdges)
                                    this.RemoveElement(edge);
                            }

                            this.RemoveElement(node.Value);
                            deleteKeys.Add(node.Key);
                        }
                        foreach (var key in deleteKeys)
                            this.bundleNodes.Remove(key);
                    }

                    // TODO: メンバ変数とローカル変数の定義がぐちゃぐちゃなので整理すべき
                    parentStack.Clear();
                    var position = new Vector2(400f, 50f);
                    var placedNodes = new HashSet<string>(); // 整列済みノード
                    var depth = 0;
                    foreach (var bundleName in this.explicitNodes)
                    {
                        if (!string.IsNullOrEmpty(focusBundleName) && !enabledNodes.Contains(bundleName))
                            continue;
                        (bool aligned, Vector2 pos) = this.AlignNode(rule.context, bundleName, parentStack, placedNodes, position, depth);
                        if (true)
                            position = pos;
                    }
                });

                this.StretchToParentSize(); // 親のサイズに合わせてGraphViewのサイズを設定
                this.SetupZoom(ContentZoomer.DefaultMinScale, ContentZoomer.DefaultMaxScale); // スクロールズーム
                this.AddManipulator(new ContentDragger()); // 描画範囲ドラッグ
                this.AddManipulator(new SelectionDragger()); // 選択ノードドラッグ
                this.AddManipulator(new RectangleSelector()); // 範囲ノード選択
            }

            bool IsShaderGroupNode(BundleNode node)
            {
                return node.title.Contains(AddrAutoGrouping.SHADER_GROUP_NAME);
            }

            bool IsBuiltInShaderNode(BundleNode node)
            {
                return node.bundleName.Contains(AddrUtility.UNITY_BUILTIN_SHADERS);
            }


            #region PRIVATE FUNCTION

            /// <summary>
            /// ノード整列
            /// </summary>
            private (bool, Vector2) AlignNode(AddressableAssetsBuildContext context, string bundleName,
                Stack<string> parentStack, HashSet<string> placedNodes, Vector2 position, int limitedDepth)
            {
                var aligned = false;
                
                if (this.bundleNodes.TryGetValue(bundleName, out var node))
                {
                    var rect = node.GetPosition();
                    
                    // Shaderグループ非表示
                    if (!this.graphSetting.enabledShaderNode)
                    {
                        //if (node.title == this.shaderNode || node == this.builtinNode)
                        if (this.IsShaderGroupNode(node) || this.IsBuiltInShaderNode(node))
                        {
                            node.visible = false;
                            foreach (var edgeList in node.edgeFrom.Values)
                            {
                                foreach (var edge in edgeList)
                                    edge.visible = false;
                            }

                            return (false, position);
                        }
                    }
                    // フィルタリングによって独立している依存ノードは非表示
                    if (limitedDepth > 0 && node.input.Count == 0)
                    {
                        node.visible = false;
                        return (false, position);
                    }

                    // 未配置ノードを配置
                    if (!placedNodes.Contains(bundleName))
                    {
                        rect.x = position.x;
                        rect.y = position.y;
                        node.SetPosition(rect);
                        placedNodes.Add(bundleName);
                    }

                    var addChild = false;
                    // 表示深度制限
                    limitedDepth++;
                    if (this.graphSetting.enabledDepth == 0 || limitedDepth <= this.graphSetting.enabledDepth)
                    {
                        parentStack.Push(bundleName);

                        if (context.bundleToImmediateBundleDependencies.TryGetValue(bundleName, out var depBundleNames))
                        {
                            // アルファベット順にソート
                            depBundleNames.Sort(AddrUtility.CompareGroup);

                            if (depBundleNames.Count > 1)
                            {
                                var pos = position;
                                pos.x += rect.width + NODE_OFFSET_H;

                                // 依存bundleを再帰検索
                                foreach (var depBundleName in depBundleNames)
                                {
                                    // 自身は無視
                                    if (depBundleName == bundleName)
                                        continue;

                                    // 循環参照
                                    if (parentStack.Contains(depBundleName))
                                    {
                                        if (this.bundleNodes.TryGetValue(depBundleName, out var depNode))
                                            Debug.LogWarning($"循環参照 : {node.title} <-> {depNode.title}");
                                        continue;
                                    }

                                    // 配置済
                                    if (placedNodes.Contains(depBundleName))
                                    {
                                        addChild = true;
                                    }
                                    // 再帰検索
                                    else
                                    {
                                        (addChild, pos) = this.AlignNode(context, depBundleName, parentStack, placedNodes, pos, limitedDepth);
                                    }

                                    if (addChild)
                                        position.y = pos.y;
                                }
                            }
                            else
                            {
                                addChild = true;
                            }
                        }

                        parentStack.Pop();
                    }
                    
                    if (!addChild)
                        position.y += rect.height + NODE_OFFSET_V;
                    position.y = Mathf.Max(position.y, rect.y + rect.height + NODE_OFFSET_V);
                    aligned |= addChild;
                }
                else
                {
                    // bundleNodeにない場合は設定でSkipされてるものとみなす
                    aligned = true;

                    // for test
                    // if (context.bundleToAssetGroup.TryGetValue(bundleName, out var groupGuid))
                    // {
                    //     var groupName = context.Settings.FindGroup(findGroup => findGroup is not null && findGroup.Guid == groupGuid).name;
                    //     Debug.LogWarning($"Skip bundle : {groupName} : {bundleName}");
                    // }
                }

                return (aligned, position);
            }

            /// <summary>
            /// 新規ノード
            /// </summary>
            /// <param name="bundleName">AssetBundle名</param>
            /// <param name="isExplicit">明示的に呼ばれる（カタログに追加されている）グループか</param>
            /// <param name="createSharedNode">Sharedノード有効か</param>
            private BundleNode CreateBundleNode(AddressableAssetsBuildContext context, string bundleName, bool isExplicit)
            {
                if (this.bundleNodes.TryGetValue(bundleName, out var node))
                {
                    Debug.LogWarning($"Exist the same bundleName for Nodes : {bundleName}");
                    return node;
                }

                var title = string.Empty;
                //var isBuiltIn = false;
                //var isShaderNode = false; // Shader
                // ノード名
                if (bundleName.Contains(AddrUtility.UNITY_BUILTIN_SHADERS))
                {
                    title = "Unity Built-in Shaders";
                    //isBuiltIn = true;
                }
                else
                {
                    // Hashを取り除いてグループ名と結合
                    var temp = System.IO.Path.GetFileName(bundleName).Split(new string[] { "_assets_", "_scenes_" }, System.StringSplitOptions.None);
                    title = temp[temp.Length - 1];
                    if (context.bundleToAssetGroup.TryGetValue(bundleName, out var groupGuid))
                    {
                        var groupName = context.Settings.FindGroup(findGroup => findGroup is not null && findGroup.Guid == groupGuid).name;
                        title = $"{groupName}/{title}";

                        //isShaderNode = (this.shaderNode == null && groupName == AddressablesAutoGrouping.SHADER_GROUP_NAME);

                        // 常駐アセットグループを除外
                        if (!this.graphSetting.enabledResidentNode)
                        {
                            if (groupName.Contains(AddrAutoGrouping.RESIDENT_GROUP_NAME) || groupGuid == this.graphSetting.residentGroupGuid)
                                return null;
                        }
                    }
                }

                // プロジェクト規模が大きくなると表示しきれないので表示量を減らす為の措置
                if (!isExplicit)
                {
                    // 任意名無視
                    foreach (var ignore in this.graphSetting.ignoreList)
                    {
                        if (title.Contains(ignore.text))
                            return null;
                    }

                    // 依存グループ無視
                    if (!this.graphSetting.enabledSharedNode && title.Contains(AddrAutoGrouping.SHARED_GROUP_NAME))
                        return null;
                }

                node = new BundleNode(this.graphSetting.rootGraph);
                node.title = title;
                node.name = node.bundleName = bundleName;
                //if (isBuiltIn)
                //    this.builtinNode = node;
                //if (isShaderNode)
                //    this.shaderNode = node;

                // 明示的なノードは黄色
                if (isExplicit)
                    node.titleContainer.style.backgroundColor = new Color(0.3f, 0.3f, 0f, 1f);

                this.AddElement(node);
                this.bundleNodes.Add(bundleName, node);

                // タイトル横のボタン削除
                node.titleButtonContainer.Clear();
                node.titleButtonContainer.Add(new Label(string.Empty)); // NOTE: 空白入れないと詰められて見た目が悪い

                return node;
            }

            /// <summary>
            /// Inputポートの作成
            /// </summary>
            private Port CreateInputPort(BundleNode node, string portName, string keyName)
            {
                var input = Port.Create<FlowingEdge>(Orientation.Horizontal, Direction.Input, Port.Capacity.Multi, typeof(float));
                input.portName = portName;
                input.capabilities = 0;
                node.inputContainer.Add(input);
                node.input.Add(keyName, input);
                return input;
            }

            /// <summary>
            /// Edge接続
            /// </summary>
            private void AddEdge(BundleNode fromNode, Port from, BundleNode toNode, Port to)
            {
                var addEdge = false;
                if (fromNode.connectTo.TryGetValue(from, out var ports))
                {
                    // 2回目以降の接続
                    // NOTE: 複数のSubAssetが同一のAssetに依存関係を持つ場合に発生するので弾く、主にSceneで発生
                    if (!ports.Contains(to))
                        addEdge = true;
                }
                else
                {
                    // 初回接続
                    addEdge = true;
                    ports = new HashSet<Port>();
                    fromNode.connectTo.Add(from, ports);
                }

                if (addEdge)
                {
                    // 新規Edge作成
                    var edge = from.ConnectTo<FlowingEdge>(to);
                    this.AddElement(edge);
                    if (!fromNode.edgeTo.TryGetValue(from, out var toEdges))
                    {
                        toEdges = new List<FlowingEdge>();
                        fromNode.edgeTo.Add(from, toEdges);
                    }

                    if (!toNode.edgeFrom.TryGetValue(to, out var fromEdges))
                    {
                        fromEdges = new List<FlowingEdge>();
                        toNode.edgeFrom.Add(to, fromEdges);
                    }

                    toEdges.Add(edge);
                    fromEdges.Add(edge);
                    ports.Add(to);
                    this.allEdges.Add(edge);
                }
            }

            private static int CompareGroup(VisualElement a, VisualElement b)
            {
                return AddrUtility.CompareGroup(a.name, b.name);
            }

            #endregion


            #region BUNDLES

            /// <summary>
            /// 内容物をOutputポートに登録
            /// </summary>
            void CreateOutputPort(BundleNode node)
            {
                if (node is null || node.bundleName.Contains(AddrUtility.UNITY_BUILTIN_SHADERS))
                    return;

                var output = Port.Create<FlowingEdge>(Orientation.Horizontal, Direction.Output, Port.Capacity.Multi,
                    typeof(float));
                output.portName = string.Empty;
                output.capabilities = 0;
                node.outputContainer.Add(output);
                node.output.Add(node.bundleName, output);
            }

            void AddBundleNode(DependenciesRule rule, BundleNode parentNode, string bundleName, int depth)
            {
                var onlyConnect = false;
                if (this.bundleNodes.TryGetValue(bundleName, out var node))
                {
                    onlyConnect = true;
                }
                else
                {
                    // 新規登録
                    node = this.CreateBundleNode(rule.context, bundleName, isExplicit: false);
                    // 無視されるノードの場合はnull
                    if (node == null)
                        return;
                }

                // 接続ポート
                Port input = null;
                if (node.inputContainer.childCount == 0)
                    input = this.CreateInputPort(node, string.Empty, parentNode.bundleName); // 新規Port作成
                else
                    input = node.inputContainer.ElementAt(0) as Port;

                // 接続
                var parentPort = parentNode.outputContainer.ElementAt(0) as Port;
                if (rule.context.bundleToImmediateBundleDependencies.TryGetValue(parentNode.bundleName, out var depNames))
                {
                    if (depNames.Contains(bundleName))
                        this.AddEdge(parentNode, parentPort, node, input);
                }

                if (onlyConnect)
                    return;

                // 表示深度制限
                depth++;
                if (this.graphSetting.enabledDepth > 0 && this.graphSetting.enabledDepth <= depth)
                    return;

                // 依存先がある場合は作成
                if (rule.context.bundleToImmediateBundleDependencies.TryGetValue(bundleName, out var depBundleNames))
                {
                    // アルファベット順にソート
                    depBundleNames.Sort(AddrUtility.CompareGroup);

                    if (depBundleNames.Count > 1)
                    {
                        var output = Port.Create<FlowingEdge>(Orientation.Horizontal, Direction.Output,
                            Port.Capacity.Multi, typeof(float));
                        output.portName = name;
                        output.capabilities = 0;
                        node.outputContainer.Add(output);

                        // 再帰的に辿る
                        foreach (var depBundleName in depBundleNames)
                        {
                            // 自分も含まれるのでスキップ
                            if (bundleName == depBundleName)
                                continue;

                            this.AddBundleNode(rule, node, depBundleName, depth);
                        }
                    }
                }

                node.RefreshPorts();
                node.RefreshExpandedState();
            }

            void ViewBundles(DependenciesRule rule, string focusBundleName)
            {
                this.bundleNodes.Clear();
                var context = rule.context;
                var depth = 0;

                if (context.assetGroupToBundles.TryGetValue(this.graphSetting.selectedGroup, out var bundleNames))
                {
                    foreach (var bundleName in bundleNames)
                    {
                        if (bundleName.Contains(AddrUtility.UNITY_BUILTIN_SHADERS))
                            continue;

                        // 指定エントリ名でフィルタリング
                        if (this.graphSetting.selectedEntry != null)
                        {
                            var hit = false;
                            var bundleInfo = rule.allBundleInputDefs.Find(val => val.assetBundleName == bundleName);
                            foreach (var assetName in bundleInfo.assetNames)
                            {
                                if (assetName == this.graphSetting.selectedEntry.AssetPath)
                                {
                                    hit = true;
                                    break;
                                }
                            }

                            if (!hit)
                                continue;
                        }

                        var node = this.CreateBundleNode(context, bundleName, isExplicit: true);
                        this.explicitNodes.Add(bundleName);

                        // implicitノード作成
                        if (context.bundleToImmediateBundleDependencies.TryGetValue(bundleName, out var depBundleNames))
                        {
                            if (depBundleNames.Count > 1)
                            {
                                this.CreateOutputPort(node);

                                // 再帰的に辿る
                                foreach (var depBundleName in depBundleNames)
                                {
                                    // 自分も含まれるのでスキップ
                                    if (bundleName == depBundleName)
                                        continue;

                                    this.AddBundleNode(rule, node, depBundleName, depth);
                                }
                            }
                        }
                    }
                }

                // 不要なPortを取り除く
                foreach (var node in this.bundleNodes.Values)
                {
                    node.RefreshPorts();
                    node.RefreshExpandedState();
                }
            }

            #endregion


            #region ENTRIES

            /// <summary>
            /// 内容物をOutputポートに登録
            /// </summary>
            private void CreateOutputPortsWithAssets(DependenciesRule rule, BundleNode node, AddressableAssetEntry selectedEntry)
            {
                if (node.bundleName.Contains(AddrUtility.UNITY_BUILTIN_SHADERS))
                {
                    node.assetGuid.Add(AddrUtility.UNITY_BUILTIN_SHADERS, AddrUtility.UNITY_BUILTIN_SHADERS_GUID);
                    return;
                }

                // 内容物登録
                var info = rule.allBundleInputDefs.Find((info) => info.assetBundleName == node.bundleName);

                foreach (var assetName in info.assetNames)
                {
                    // 指定エントリ名でフィルタリング for Pack Together
                    if (selectedEntry != null && selectedEntry.AssetPath != assetName)
                        continue;

                    var output = Port.Create<FlowingEdge>(Orientation.Horizontal, Direction.Output, Port.Capacity.Multi, typeof(float));
                    output.portName = assetName;
                    output.capabilities = 0;
                    node.outputContainer.Add(output);
                    node.output.Add(assetName, output);
                    node.assetGuid.Add(assetName, new GUID(AssetDatabase.AssetPathToGUID(assetName)));
                }

                // アルファベット順
                node.outputContainer.Sort(CompareGroup);
            }

            int AddEntriesNode(DependenciesRule rule, BundleNode parentNode, string bundleName, int depth)
            {
                var onlyConnect = false;

                if (this.bundleNodes.TryGetValue(bundleName, out var node))
                {
                    onlyConnect = true;
                }
                else
                {
                    // 新規登録
                    node = this.CreateBundleNode(rule.context, bundleName, isExplicit: false);
                    // 無視されるノードの場合はnull
                    if (node == null)
                        return 0;

                    // 内容物表示
                    // NOTE: 依存ノードのエントリフィルタは不要
                    this.CreateOutputPortsWithAssets(rule, node, selectedEntry: null);
                }

                // 親のエントリアセットを走査
                foreach (var pair in parentNode.output)
                {
                    // ExtractData.DependencyDataからAssetの依存関係を取得
                    // NOTE: AssetDatabase.GetDependenciesでも取れるがすこぶる遅いので却下
                    var parentAsset = pair.Key;
                    var parentPort = pair.Value;
                    var parentGuid = new GUID(AssetDatabase.AssetPathToGUID(parentAsset));
                    ICollection<ObjectIdentifier> parentObjects = null;

                    // Sceneは特殊なので分ける
                    if (parentAsset.Contains(".unity"))
                    {
                        if (rule.extractData.DependencyData.SceneInfo.TryGetValue(parentGuid,
                                out var parentDependencies))
                            parentObjects = parentDependencies.referencedObjects;
                    }
                    else
                    {
                        if (rule.extractData.DependencyData.AssetInfo.TryGetValue(parentGuid,
                                out var parentDependencies))
                            parentObjects = parentDependencies.referencedObjects;
                    }

                    if (parentObjects == null)
                        continue;

                    foreach (var parentObj in parentObjects)
                    {
                        // Built-in Shader対応
                        if (parentObj.guid == AddrUtility.UNITY_BUILTIN_SHADERS_GUID)
                        {
                            if (node.assetGuid.ContainsKey(AddrUtility.UNITY_BUILTIN_SHADERS))
                            {
                                Port input = null;
                                if (node.inputContainer.childCount == 0)
                                    input = this.CreateInputPort(node, string.Empty, AddrUtility.UNITY_BUILTIN_SHADERS); // 新規Port作成
                                else
                                    input = node.inputContainer.ElementAt(0) as Port;

                                // Port間接続
                                this.AddEdge(parentNode, parentPort, node, input);
                            }

                            continue;
                        }

                        // スクリプトだとかを除外
                        var parentPath = AssetDatabase.GUIDToAssetPath(parentObj.guid);
                        if (!AddrUtility.IsPathValidForEntry(parentPath))
                            continue;
                        // Prefabは依存関係にならないので除外
                        if (parentPath.Contains(".prefab"))
                            continue;

                        // ノード接続
                        foreach (var myAssetName in node.output.Keys)
                        {
                            if (node.assetGuid.TryGetValue(myAssetName, out var myGuid))
                            {
                                // MainAsset判定
                                var hit = (parentObj.guid == myGuid);
                                // SubAsset検索
                                if (!hit)
                                {
                                    if (rule.extractData.DependencyData.AssetInfo.TryGetValue(myGuid,
                                            out var myAllAssets))
                                    {
                                        foreach (var refAsset in myAllAssets.referencedObjects)
                                        {
                                            // NOTE: インスタンス作成するのでメモリオーバーヘッドがあるが素直にSubAsset判定した方がセーフティ
                                            var instance = ObjectIdentifier.ToObject(refAsset);
                                            if (instance == null || !AssetDatabase.IsSubAsset(instance))
                                                continue;
                                            if (parentObj == refAsset)
                                            {
                                                hit = true;
                                                break;
                                            }
                                            //if (parentObj == refAsset) {
                                            //    // 参照されているがGUIDが独立していればSubAssetとみなさない
                                            //    // e.g. fbxに含まれるMaterial/Texture/ShaderはGUIDが振られるのでSubAssetではない
                                            //    if (parentObj.guid != refAsset.guid)
                                            //        hit = true;
                                            //    break;
                                            //}
                                        }
                                    }
                                }

                                // 依存関係を発見
                                if (hit)
                                {
                                    if (!node.input.TryGetValue(myAssetName, out var input))
                                        input = this.CreateInputPort(node, myAssetName, myAssetName); // 新規Port作成

                                    // Port間接続
                                    this.AddEdge(parentNode, parentPort, node, input);
                                }
                            }
                        }
                    }
                }

                if (onlyConnect)
                    return 0;

                // 表示深度制限
                depth++;
                if (this.graphSetting.enabledDepth <= depth)
                    return 0;

                var totalDepCount = 0;
                // 依存先がある場合は作成
                if (rule.context.bundleToImmediateBundleDependencies.TryGetValue(bundleName, out var depBundleNames))
                {
                    if (depBundleNames.Count > 1)
                    {
                        // 再帰的に辿る
                        foreach (var depBundleName in depBundleNames)
                        {
                            // 自分も含まれるのでスキップ
                            if (bundleName == depBundleName)
                                continue;

                            totalDepCount += this.AddEntriesNode(rule, node, depBundleName, depth);
                        }
                    }
                }

                totalDepCount = Mathf.Max(totalDepCount, 1);

                return totalDepCount;
            }

            void ViewEntries(DependenciesRule rule, string focusBundleName)
            {
                this.bundleNodes.Clear();
                var context = rule.context;
                var depth = 0;

                if (context.assetGroupToBundles.TryGetValue(this.graphSetting.selectedGroup, out var bundleNames))
                {
                    foreach (var bundleName in bundleNames)
                    {
                        if (bundleName.Contains(AddrUtility.UNITY_BUILTIN_SHADERS))
                            continue;

                        // 指定エントリ名でフィルタリング for Pack Separately
                        if (this.graphSetting.selectedEntry != null)
                        {
                            var hit = false;
                            var bundleInfo = rule.allBundleInputDefs.Find(val => val.assetBundleName == bundleName);
                            foreach (var assetName in bundleInfo.assetNames)
                            {
                                if (assetName == this.graphSetting.selectedEntry.AssetPath)
                                {
                                    hit = true;
                                    break;
                                }
                            }

                            if (!hit)
                                continue;
                        }

                        // explicitノード作成
                        var node = this.CreateBundleNode(rule.context, bundleName, isExplicit: true);
                        this.explicitNodes.Add(bundleName);

                        // 内容物表示
                        this.CreateOutputPortsWithAssets(rule, node, this.graphSetting.selectedEntry);

                        // implicitノード作成
                        //var totalDepth = 0;
                        if (context.bundleToImmediateBundleDependencies.TryGetValue(bundleName, out var depBundleNames))
                        {
                            if (depBundleNames.Count > 1)
                            {
                                // 再帰的に辿る
                                foreach (var depBundleName in depBundleNames)
                                {
                                    // 自分も含まれるのでスキップ
                                    if (bundleName == depBundleName)
                                        continue;

                                    var depCount = this.AddEntriesNode(rule, node, depBundleName, depth);
                                    //totalDepth += depCount;
                                }
                            }
                        }
                    }

                    // 不要なPortを取り除く
                    foreach (var node in this.bundleNodes.Values)
                    {
                        node.RefreshPorts();
                        node.RefreshExpandedState();
                    }
                }
            }

            #endregion

            // MEMO: 右クリックのコンテキストメニュー
            public override void BuildContextualMenu(ContextualMenuPopulateEvent evt)
            {
                // 今回は確認専用ビューなので空にする（何も出さない）
            }
        }

        #endregion
    }
}