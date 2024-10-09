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
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.Experimental.GraphView;
using UnityEngine;

namespace AddrAuditor.Editor
{
    /// <summary>
    /// AssetBundleノード
    /// </summary>
    internal partial class BundlesGraph
    {
        /// <summary>
        /// 内容物をOutputポートに登録
        /// </summary>
        /// <param name="node">判定ノード</param>
        static void CreateOutputPort(BundleNode node)
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

        /// <summary>
        /// 指定bundleのノードを追加
        /// </summary>
        /// <param name="rule">解析データ</param>
        /// <param name="parentNode">親ノード</param>
        /// <param name="bundleName">指定bundle</param>
        /// <param name="depth">現在の深度</param>
        void AddBundleNode(DependenciesRule rule, BundleNode parentNode, string bundleName, int depth)
        {
            BundleNode node;
            var onlyConnect = false;
            if (this.bundleNodes.TryGetValue(bundleName, out node))
            {
                onlyConnect = true;
            }
            else
            {
                // 新規登録
                if (!CreateBundleNode(rule.context, bundleName, isExplicit: false, this.graphSetting, out node))
                    return; // 無視されるノード
                this.bundleNodes.Add(bundleName, node);
                this.AddElement(node);
            }

            // 接続ポート
            Port input = null;
            if (node.inputContainer.childCount == 0)
                input = CreateInputPort(node, string.Empty, parentNode.bundleName); // 新規Port作成
            else
                input = node.inputContainer.ElementAt(0) as Port;

            // 接続
            var parentPort = parentNode.outputContainer.ElementAt(0) as Port;
            if (rule.context.bundleToImmediateBundleDependencies.TryGetValue(parentNode.bundleName, out var depNames))
            {
                if (depNames.Contains(bundleName))
                {
                    if (AddEdge(parentNode, parentPort, node, input, out var edge))
                    {
                        this.allEdges.Add(edge);
                        this.AddElement(edge);
                    }
                }
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
                    var output = Port.Create<FlowingEdge>(Orientation.Horizontal, Direction.Output, Port.Capacity.Multi, typeof(float));
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

        /// <summary>
        /// 指定エントリが指定bundleに含まれてるか
        /// </summary>
        /// <param name="rule">解析データ</param>
        /// <param name="bundleName">指定bundle</param>
        /// <param name="entries">指定エントリ</param>
        /// <returns>含まれてるか</returns>
        static bool IsContainedEntries(DependenciesRule rule, string bundleName, List<AddressableAssetEntry> entries)
        {
            // if null exists, ALL is selected
            if (entries.Contains(null))
                return true;
            
            var info = rule.allBundleInputDefs.Find(val => val.assetBundleName == bundleName);
            foreach (var assetName in info.assetNames)
            {
                foreach (var entry in entries)
                {
                    // null entry if ALL is selected
                    if (entry == null)
                        continue;
                    if (assetName == entry.AssetPath)
                        return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Bundle依存関係の表示
        /// </summary>
        /// <param name="rule">解析データ</param>
        void ViewBundles(DependenciesRule rule)
        {
            this.bundleNodes.Clear();
            var context = rule.context;
            var depth = 0;

            var bundleNames = new List<string>(20000);
            foreach (var group in this.graphSetting.selectedGroups)
            {
                // null group if ALL is selected
                if (group == null)
                    continue;
                if (context.assetGroupToBundles.TryGetValue(group, out var names))
                    bundleNames.AddRange(names);
            }

            foreach (var bundleName in bundleNames)
            {
                if (bundleName.Contains(AddrUtility.UNITY_BUILTIN_SHADERS))
                    continue;
                
                // ありえないはずだが重複チェック
                if (this.bundleNodes.ContainsKey(bundleName))
                {
                    Debug.LogWarning($"Exist the same bundleName for Nodes : {bundleName}");
                    continue;
                }

                // 指定エントリ名が含まれてるか
                if (!IsContainedEntries(rule, bundleName, this.graphSetting.selectedEntries))
                    return;

                // explicitノード作成
                if (!CreateBundleNode(context, bundleName, isExplicit: true, this.graphSetting, out var node))
                        continue;
                this.explicitNodes.Add(bundleName);
                this.bundleNodes.Add(bundleName, node);
                this.AddElement(node);

                // implicitノード作成
                if (context.bundleToImmediateBundleDependencies.TryGetValue(bundleName, out var depBundleNames))
                {
                    if (depBundleNames.Count > 1)
                    {
                        CreateOutputPort(node);

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

            // 不要なPortを取り除く
            foreach (var node in this.bundleNodes.Values)
            {
                node.RefreshPorts();
                node.RefreshExpandedState();
            }
        }
    }
}