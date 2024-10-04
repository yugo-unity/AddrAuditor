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
using UnityEditor.Build.Pipeline;
using UnityEditor.Experimental.GraphView;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace AddrAuditor.Editor
{
    [System.Serializable]
    public class IgnorePrefix
    {
        public string text;
    }

    /// <summary>
    ///  Analyze
    /// </summary>
    internal class DependenciesRule
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
    /// Addressablesで自動解決した依存関係をノードグラフで確認する
    /// </summary>
    internal class AddrDependenciesGraph : EditorWindow
    {
        private GraphView graphView = null;
        private DependenciesRule bundleRule = new ();
        private AddrAutoGroupingSettings groupingSettings;
        private GraphSetting graphSetting;
        private Box mainBox;

        [SerializeField] private List<IgnorePrefix> ignorePrefixList = new ();

        internal void UpdateBundleDependencies(string bundleName, BundlesGraph.TYPE graphType)
        {
            if (this.mainBox is not null)
                this.rootVisualElement.Remove(this.mainBox);
            if (this.graphView is not null)
                this.rootVisualElement.Remove(this.graphView);
            this.graphView = new BundlesGraph(graphType, this.graphSetting, this.bundleRule, bundleName);
            this.rootVisualElement.Add(this.graphView);
            this.rootVisualElement.Add(this.mainBox);
        }

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
    }
}