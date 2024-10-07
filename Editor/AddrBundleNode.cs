/***********************************************************************************************************
 * AddrBundlesNode.cs
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
using UnityEditor.Experimental.GraphView;
using UnityEngine.UIElements;

namespace AddrAuditor.Editor
{
    /// <summary>
    /// AssetBundleノード
    /// </summary>
    internal class BundleNode : Node
    {
        GraphSetting graphSetting;
        public string bundleName = string.Empty;
        public Dictionary<string, Port> input = new(); // InputContainerに登録されているPort
        public Dictionary<string, Port> output = new(); // OutputContainerに登録されているPort

        public Dictionary<string, GUID> assetGuid = new(); // Portに登録されているAssetのGUID（依存関係で使うのでキャッシュ）

        public Dictionary<Port, HashSet<Port>> connectTo = new(); // 接続されたPort
        public Dictionary<Port, List<FlowingEdge>> edgeTo = new(); // 接続されたEdge
        public Dictionary<Port, List<FlowingEdge>> edgeFrom = new(); // 接続されたEdge

        public BundleNode(GraphSetting setting)
        {
            this.graphSetting = setting;
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
            evt.menu.AppendAction("Focus the dependencies on this bundle", FocusSimpleNode);
            evt.menu.AppendAction("Focus the dependencies on this bundle with contained entries", FocusEntriesNode);
        }

        void FocusSimpleNode(DropdownMenuAction action) =>
            this.graphSetting.rootGraph.UpdateBundleDependencies(BundlesGraph.TYPE.BUNDLE_DEPENDENCE, this.graphSetting, this.bundleName);

        void FocusEntriesNode(DropdownMenuAction action) =>
            this.graphSetting.rootGraph.UpdateBundleDependencies(BundlesGraph.TYPE.ASSET_DEPENDENCE, this.graphSetting, this.bundleName);
    }
}