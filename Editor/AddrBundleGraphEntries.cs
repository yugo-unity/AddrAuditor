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
using UnityEditor;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.Build.Content;
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
        static void CreateOutputPortsWithAssets(DependenciesRule rule, BundleNode node, AddressableAssetEntry selectedEntry)
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
                if (!CreateBundleNode(rule.context, bundleName, isExplicit: false, this.graphSetting, out node))
                    return 0; // 無視されるノード
                this.bundleNodes.Add(bundleName, node);
                this.AddElement(node);

                // 内容物表示
                // NOTE: 依存ノードのエントリフィルタは不要
                CreateOutputPortsWithAssets(rule, node, selectedEntry: null);
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
                                input = CreateInputPort(node, string.Empty, AddrUtility.UNITY_BUILTIN_SHADERS); // 新規Port作成
                            else
                                input = node.inputContainer.ElementAt(0) as Port;

                            // Port間接続
                            if (AddEdge(parentNode, parentPort, node, input, out var edge))
                            {
                                this.allEdges.Add(edge);
                                this.AddElement(edge);
                            }
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
                                    input = CreateInputPort(node, myAssetName, myAssetName); // 新規Port作成

                                // Port間接続
                                if (AddEdge(parentNode, parentPort, node, input, out var edge))
                                {
                                    this.allEdges.Add(edge);
                                    this.AddElement(edge);
                                }
                            }
                        }
                    }
                }
            }

            if (onlyConnect)
                return 0;

            // 表示深度制限
            depth++;
            if (this.graphSetting.enabledDepth > 0 && this.graphSetting.enabledDepth <= depth)
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

        void ViewEntries(DependenciesRule rule)
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
                    if (!CreateBundleNode(rule.context, bundleName, isExplicit: true, this.graphSetting, out var node))
                        continue;
                    this.explicitNodes.Add(bundleName);
                    this.bundleNodes.Add(bundleName, node);
                    this.AddElement(node);

                    // 内容物表示
                    CreateOutputPortsWithAssets(rule, node, this.graphSetting.selectedEntry);

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
                                
                                this.AddEntriesNode(rule, node, depBundleName, depth);
                                //var depCount = this.AddEntriesNode(rule, node, depBundleName, depth);
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
    }
}