/***********************************************************************************************************
 * AddrBundlesGraph.cs
 * Copyright (c) Yugo Fujioka - Unity Technologies Japan K.K.
 * 
 * Licensed under the Unity Companion License for Unity-dependent projects--see Unity Companion License.
 * https://unity.com/legal/licenses/unity-companion-license
 * Unless expressly provided otherwise, the Software under this license is made available strictly
 * on an "AS IS" BASIS WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED.
 * Please review the license for details on these and other terms and conditions.
***********************************************************************************************************/

using System.Collections.Generic;
using UnityEditor.AddressableAssets.Build.DataBuilders;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace AddrAuditor.Editor
{
    /// <summary>
    /// AssetBundleノード
    /// </summary>
    internal partial class BundlesGraph : GraphView
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

        static bool IsShaderGroupNode(BundleNode node) => node.title.Contains(AddrAutoGrouping.SHADER_GROUP_NAME);
        static bool IsBuiltInShaderNode(BundleNode node) => node.bundleName.Contains(AddrUtility.UNITY_BUILTIN_SHADERS);
        static int CompareGroup(VisualElement a, VisualElement b) => AddrUtility.CompareGroup(a.name, b.name);

        HashSet<string> CollectAvailableBundleNames(string focusBundleName, AddressableAssetsBuildContext context)
        {
            var parentStack = new Stack<string>(this.bundleNodes.Count);
            var availableNames = new HashSet<string>(this.bundleNodes.Count);
            
            // 子供の登録
            CheckAvailableBundle(true, focusBundleName, focusBundleName, context, parentStack, availableNames);
            // 明示的なbundleから辿る
            foreach (var bundleName in this.explicitNodes)
            {
                parentStack.Clear();
                CheckAvailableBundle(false, bundleName, focusBundleName, context, parentStack, availableNames);
            }

            return availableNames;
        }

        /// <summary>
        /// 指定bundle名と依存関係があるbundle名なのか再帰的に判定する
        /// </summary>
        /// <param name="self">指定bundle自身の判定か？truneなら子は全て登録</param>
        /// <param name="bundleName">判定するbundle名</param>
        /// <param name="focusBundleName">指定されたbundle名</param>
        /// <param name="context">解析データ</param>
        /// <param name="parentStack">検出中の親bundle</param>
        /// <param name="availableNames">依存関係が認められたbundle名</param>
        void CheckAvailableBundle(bool self, string bundleName, string focusBundleName, AddressableAssetsBuildContext context,
            Stack<string> parentStack, HashSet<string> availableNames)
        {
            // 指定bundleにたどり着いたらそれまでのbundleを登録
            if (!self && bundleName == focusBundleName)
            {
                foreach (var stack in parentStack)
                    availableNames.Add(stack);
                return;
            }

            if (!self)
                parentStack.Push(bundleName);
            else
                availableNames.Add(bundleName);
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
                    CheckAvailableBundle(self, depBundleName, focusBundleName, context, parentStack, availableNames);
                }
            }

            if (!self)
                parentStack.Pop();
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
                    this.ViewBundles(rule);
                    break;
                case TYPE.ASSET_DEPENDENCE:
                    this.ViewEntries(rule);
                    break;
            }

            // NOTE: レイアウトが一旦完了しないとノードサイズがとれないのでViewの描画前コールバックに仕込む
            this.RegisterCallback<GeometryChangedEvent>((ev) =>
            {
                HashSet<string> availableNames = null;
                if (!string.IsNullOrEmpty(focusBundleName))
                {
                    // 指定bundle名と依存関係のあるbundle名を検出
                    availableNames = this.CollectAvailableBundleNames(focusBundleName, rule.context);

                    // 指定bundleと依存関係を持たないBundleNodeを削除
                    var deleteKeys = new List<string>(this.bundleNodes.Keys.Count);
                    foreach (var node in this.bundleNodes)
                    {
                        if (availableNames.Contains(node.Key))
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

                var position = new Vector2(400f, 50f);
                var parentStack = new Stack<string>(this.bundleNodes.Count); // 親ノード
                var placedNodes = new HashSet<string>(this.bundleNodes.Count); // 整列済みノード
                foreach (var bundleName in this.explicitNodes)
                    this.AlignNode(rule.context, bundleName, parentStack, placedNodes, currentDepth:0, ref position);
            });

            this.StretchToParentSize(); // 親のサイズに合わせてGraphViewのサイズを設定
            this.SetupZoom(ContentZoomer.DefaultMinScale, ContentZoomer.DefaultMaxScale); // スクロールズーム
            this.AddManipulator(new ContentDragger()); // 描画範囲ドラッグ
            this.AddManipulator(new SelectionDragger()); // 選択ノードドラッグ
            this.AddManipulator(new RectangleSelector()); // 範囲ノード選択
        }

        /// <summary>
        /// ノード整列
        /// </summary>
        /// <param name="context">解析用BuildContext</param>
        /// <param name="bundleName">AssetBundle名</param>
        /// <param name="parentStack">親のAssetBundle名</param>
        /// <param name="placedNodes">配置済みノード</param>
        /// <param name="currentDepth">現在の依存関係深度</param>
        /// <param name="position">配置座標</param>
        /// <returns>新規配置したか</returns>
        bool AlignNode(AddressableAssetsBuildContext context, string bundleName,
            Stack<string> parentStack, HashSet<string> placedNodes, int currentDepth, ref Vector2 position)
        {
            var aligned = false;
            
            if (this.bundleNodes.TryGetValue(bundleName, out var node))
            {
                var rect = node.GetPosition();
                
                // Shaderグループ非表示
                if (!this.graphSetting.enabledShaderNode)
                {
                    //if (node.title == this.shaderNode || node == this.builtinNode)
                    if (IsShaderGroupNode(node) || IsBuiltInShaderNode(node))
                    {
                        node.visible = false;
                        foreach (var edgeList in node.edgeFrom.Values)
                        {
                            foreach (var edge in edgeList)
                                edge.visible = false;
                        }

                        return false;
                    }
                }
                // フィルタリングによって独立している依存ノードは非表示
                if (currentDepth > 0 && node.input.Count == 0)
                {
                    node.visible = false;
                    return false;
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
                currentDepth++;
                if (this.graphSetting.enabledDepth == 0 || currentDepth <= this.graphSetting.enabledDepth)
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
                                    addChild = true;
                                // 再帰検索
                                else
                                    addChild |= this.AlignNode(context, depBundleName, parentStack, placedNodes, currentDepth, ref pos);
                            }

                            if (addChild)
                                position.y = pos.y;
                        }
                        else
                        {
                            // 終端
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

            return aligned;
        }

        /// <summary>
        /// 新規ノード
        /// </summary>
        /// <param name="context">解析用のBuildContext</param>
        /// <param name="bundleName">AssetBundle名</param>
        /// <param name="isExplicit">明示的に呼ばれる（カタログに追加されている）グループか</param>
        /// <param name="graphSetting">常駐ノードを表示するか</param>
        /// <param name="node">生成されたBundleNode</param>
        static bool CreateBundleNode(AddressableAssetsBuildContext context, string bundleName, bool isExplicit, GraphSetting graphSetting, out BundleNode node)
        {
            node = null;
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

                    //isShaderNode = (this.shaderNode == null && groupName == AddressableAutoGrouping.SHADER_GROUP_NAME);

                    // 常駐アセットグループを除外
                    if (!graphSetting.enabledResidentNode)
                    {
                        if (groupName.Contains(AddrAutoGrouping.RESIDENT_GROUP_NAME) || groupGuid == graphSetting.residentGroupGuid)
                            return false;
                    }
                }
            }

            // プロジェクト規模が大きくなると表示しきれないので表示量を減らす為の措置
            if (!isExplicit)
            {
                // 任意名無視
                foreach (var ignore in graphSetting.ignoreList)
                {
                    if (title.Contains(ignore.text))
                        return false;
                }

                // 依存グループ無視
                if (!graphSetting.enabledSharedNode && title.Contains(AddrAutoGrouping.SHARED_GROUP_NAME))
                    return false;
            }

            node = new BundleNode(graphSetting);
            node.title = title;
            node.name = node.bundleName = bundleName;
            //if (isBuiltIn)
            //    this.builtinNode = node;
            //if (isShaderNode)
            //    this.shaderNode = node;

            // 明示的なノードは黄色
            if (isExplicit)
                node.titleContainer.style.backgroundColor = new Color(0.3f, 0.3f, 0f, 1f);

            // タイトル横のボタン削除
            node.titleButtonContainer.Clear();
            node.titleButtonContainer.Add(new Label(string.Empty)); // NOTE: 空白入れないと詰められて見た目が悪い

            return true;
        }

        /// <summary>
        /// Inputポートの作成
        /// </summary>
        static Port CreateInputPort(BundleNode node, string portName, string keyName)
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
        static bool AddEdge(BundleNode fromNode, Port from, BundleNode toNode, Port to, out FlowingEdge edge)
        {
            edge = null;
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
                edge = from.ConnectTo<FlowingEdge>(to);
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
            }
            return addEdge;
        }

        // MEMO: 右クリックのコンテキストメニュー
        public override void BuildContextualMenu(ContextualMenuPopulateEvent evt)
        {
            // 今回は確認専用ビューなので空にする（何も出さない）
        }
    }
}