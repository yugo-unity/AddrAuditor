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

        void ViewBundles(DependenciesRule rule)
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
                    
                    // ありえないはずだが重複チェック
                    if (this.bundleNodes.ContainsKey(bundleName))
                    {
                        Debug.LogWarning($"Exist the same bundleName for Nodes : {bundleName}");
                        continue;
                    }

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