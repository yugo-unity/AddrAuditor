/***********************************************************************************************************
 * AddrAuditor.cs
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
using UnityEditor.AddressableAssets.Build.DataBuilders;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEditor.Build.Pipeline.Utilities;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine;
using UnityEngine.ResourceManagement.Util;
using UnityEngine.UIElements;

namespace AddrAuditor.Editor
{
    internal partial class AddrAuditor : EditorWindow
    {
        const int RECURSIVE_COUNT = 10;
        const float SINGLE_BUTTON_WIDTH = 366f;
        const float TWIN_BUTTON_WIDTH = 180f;
        
        [MenuItem("UTJ/ADDR Auditor")]
        static void OpenWindow()
        {
            var window = GetWindow<AddrAuditor>();
            window.titleContent = new GUIContent("ADDR Auditor");
            window.minSize = new Vector2(400f, 510f);
            window.Show();
        }

        public void CreateGUI()
        {
            // find settings file
            var settingsPath = $"Assets/{nameof(AddrAutoGroupingSettings)}.asset";
            var groupingSettings = AssetDatabase.LoadAssetAtPath<AddrAutoGroupingSettings>(settingsPath);
            if (groupingSettings == null)
            {
                groupingSettings = ScriptableObject.CreateInstance<AddrAutoGroupingSettings>();
                AssetDatabase.CreateAsset(groupingSettings, settingsPath);
            }
            
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            var mainElement = this.rootVisualElement;
            
            // initialize static members
            AddrUtility.defaultGroupGuid = settings.DefaultGroup.Guid;
            AddrUtility.ReloadInternalAPI();
            
            AddrUtility.CreateSpace(mainElement);

            UtilityButtons(mainElement, settings);
            
            AddrUtility.CreateSpace(mainElement);

            CreateSharedGroups(mainElement, settings, groupingSettings);
            
            AddrUtility.CreateSpace(mainElement);
            
            BuildButton(mainElement, settings, groupingSettings);
            
            AddrUtility.CreateSpace(mainElement, 2f);
            
            OptionalButtons(mainElement);
        }

        static void UtilityButtons(VisualElement root, AddressableAssetSettings settings)
        {
            var button = AddrUtility.CreateButton(root, "Open and Sort Addressables Groups",
                "Addressables Group Windowを開きます。\n\n" +
                "Open Addressables window, just as shortcut.");
            button.style.width = SINGLE_BUTTON_WIDTH;
            button.style.alignSelf = new StyleEnum<Align>(Align.Center);
            button.clicked += () =>
            {
                EditorApplication.ExecuteMenuItem("Window/Asset Management/Addressables/Groups");
                
                settings.groups.Sort(AddrUtility.CompareGroup);
                settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryModified, eventData: null,
                    postEvent: true, settingsModified: true);
            };
            
            button = AddrUtility.CreateButton(root, "Open Dependencies Graph",
                "依存関係をグラフ化します。\n\n" +
                "Display dependencies as node-graph.");
            button.style.width = SINGLE_BUTTON_WIDTH;
            button.style.alignSelf = new StyleEnum<Align>(Align.Center);
            button.clicked += () =>
            {
                var window = GetWindow<AddrDependenciesGraph>();
                window.titleContent = new GUIContent("ADDR Dependencies Graph");
                window.minSize = new Vector2(400f, 450f);
                window.Show();
            };
            
            button = AddrUtility.CreateButton(root, "Analyze & Suggest any settings",
                "プロジェクトを解析して設定の提案を行います。\n\n" +
                "Analyze Addressables settings and Suggest better one for console.");
            button.style.width = SINGLE_BUTTON_WIDTH;
            button.style.alignSelf = new StyleEnum<Align>(Align.Center);
            button.clicked += () =>
            {
                var window = GetWindow<AddrAnalyzeWindow>("ADDR Analyze Window");
                window.minSize = new Vector2(400f, 450f);
                window.Show();
            };
        }

        static void SelectResidentGroup(VisualElement root, AddressableAssetSettings settings, AddrAutoGroupingSettings groupingSettings)
        {
            var choices = new List<string>(settings.groups.Count + 1) { "none" };
            settings.groups.Sort(AddrUtility.CompareGroup);
            foreach (var group in settings.groups)
            {
                if (AddrAutoGrouping.IsAutoGroup(group))
                    continue;
                choices.Add(group.Name);
            }

            var field = new DropdownField("Resident Group", choices, 0);
            field.tooltip = "常駐アセットのグループを指定します。\n\n" +
                            "You can specify asset group that remain on memory persistently.";
            //field.AddToClassList("some-styled-field");
            var firstGroup = settings.FindGroup(g => g && g.Guid == groupingSettings.residentGroupGUID);
            field.value = firstGroup != null ? firstGroup.Name : "";
            field.labelElement.style.minWidth = (StyleLength)200f;
            field.RegisterValueChangedCallback((evt) =>
            {
                var group = settings.FindGroup(evt.newValue);
                groupingSettings.residentGroupGUID = group != null ? group.Guid : "";
                EditorUtility.SetDirty(groupingSettings);
            });
            root.Add(field);
        }
        
        static void CreateSharedGroups(VisualElement root, AddressableAssetSettings settings, AddrAutoGroupingSettings groupingSettings)
        {
            var box = new VisualElement();
            box.style.alignSelf = Align.Center;
            box.style.minWidth = 371f;
            
            var toggle = AddrUtility.CreateToggle(box,
                "Create Shader Group (optional)",
                "Shader専用のグループを作ります。\n\n" +
                "[optional] Create all used shader group.",
                groupingSettings.shaderGroup);
            toggle.RegisterValueChangedCallback((evt) =>
            {
                groupingSettings.shaderGroup = evt.newValue;
                EditorUtility.SetDirty(groupingSettings);
            });
            
            toggle = AddrUtility.CreateToggle(box,
                "Allow Duplicated Materials",
                "Materialの重複を許容します。過剰なbundleの細分化は避けた方がベターです。\n\n" +
                "You can ignore duplicated materials. It is better to do if their size are smaller than 32KB.",
                groupingSettings.allowDuplicatedMaterial);
            toggle.RegisterValueChangedCallback((evt) =>
            {
                groupingSettings.allowDuplicatedMaterial = evt.newValue;
                EditorUtility.SetDirty(groupingSettings);
            });
            // NOTE: パッチ差分が修正されたので特に不要
            // var thresholdField = AddrUtility.CreateInteger(root,
            //     "Threshold (KiB)",
            //     "ファイルサイズが閾値を超える場合にSingleグループに割り振ります。0の場合は行いません。LZ4圧縮された後のサイズではないので検証目的で使用してください。",
            //     groupingSettings.singleThreshold);
            // thresholdField.RegisterValueChangedCallback((evt) => groupingSettings.singleThreshold = evt.newValue);
            
            SelectResidentGroup(box, settings, groupingSettings);
            
            root.Add(box);
            
            box = new VisualElement();
            box.style.flexDirection = FlexDirection.Row;
            box.style.alignSelf = Align.Center;

            var button = AddrUtility.CreateButton(box, "Remove Shared-Groups",
                "自動生成されたグループを一括削除します。\n\n" +
                "Remove all shared groups created automatically.");
            button.style.width = TWIN_BUTTON_WIDTH;
            button.clicked += () =>
            {
                var deletedGroupList = new List<AddressableAssetGroup>();
                foreach (var group in settings.groups)
                {
                    //if (group.ReadOnly && group.GetSchema<PlayerDataGroupSchema>() == null)
                    if (AddrAutoGrouping.IsAutoGroup(group))
                        deletedGroupList.Add(group);
                }
            
                foreach (var group in deletedGroupList)
                    settings.RemoveGroup(group);
                // alphanumericソート
                settings.groups.Sort(AddrUtility.CompareGroup);
                settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryModified, eventData: null,
                    postEvent: true, settingsModified: true);
            };

            button = AddrUtility.CreateButton(box, "Create Shared-Groups",
                "重複アセットを解決するShared Assets Groupを作成します。エントリ済のAssetは変更されません。\n\n" +
                "Create shared groups automatically. Any entries are not modified.");
            button.style.width = TWIN_BUTTON_WIDTH;
            button.clicked += () =>
            {
                // set to Default Build Index
                settings.ActivePlayerDataBuilderIndex = 0;
                
                var completed = false;
                // recursive process for duplicated assets that duplicated assets have
                for (var i = 0; i < RECURSIVE_COUNT; ++i)
                {
                    if (AddrAutoGrouping.Execute(groupingSettings))
                        continue;
                    completed = true;
                    break;
                }
                if (!completed)
                    Debug.LogError($"Canceled Auto-Grouping. Dependencies occurs more than the specified counts ({RECURSIVE_COUNT}).");
            };
            
            root.Add(box);
        }

        static void BuildButton(VisualElement root, AddressableAssetSettings settings, AddrAutoGroupingSettings groupingSettings)
        {
            var box = new VisualElement();
            box.style.alignSelf = new StyleEnum<Align>(Align.Center);
            box.style.minWidth = 371f;

            var toggle = AddrUtility.CreateToggle(box, "Bundle Name is Hash of Filename",
                "出力されるBundle名をファイル名のHashにします。\n\n" +
                "Set Bundle Naming Mode to Hash of Filename.", groupingSettings.hashName);
            toggle.RegisterValueChangedCallback((evt) =>
            {
                groupingSettings.hashName = evt.newValue;
                foreach (var group in settings.groups)
                {
                    var schema = group.GetSchema<BundledAssetGroupSchema>();
                    schema.BundleNaming = groupingSettings.hashName
                        ? BundledAssetGroupSchema.BundleNamingStyle.FileNameHash
                        : BundledAssetGroupSchema.BundleNamingStyle.NoHash;
                }

                settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryModified, eventData: null,
                    postEvent: true, settingsModified: true);
            });
            
            toggle = AddrUtility.CreateToggle(box, "Optimized Provider (for local bundles)",
                "GroupSchemaにローカル用に最適化したProviderを設定します。\n\n" +
                "Use optimized providers for local bundles to all GroupSchemas.", groupingSettings.useLocalProvider);
            toggle.RegisterValueChangedCallback((evt) =>
            {
                groupingSettings.useLocalProvider = evt.newValue;
                // NOTE: all groups are updated if AddressablesAssetSettings is updated
                if (groupingSettings.useLocalProvider)
                {
                    settings.BundledAssetProviderType = new SerializedType() { Value = typeof(LocalBundledAssetProvider) };
                    settings.AssetBundleProviderType = new SerializedType() { Value = typeof(LocalAssetBundleProvider) };
                }
                else
                {
                    settings.BundledAssetProviderType = new SerializedType() { Value = typeof(BundledAssetProvider) };
                    settings.AssetBundleProviderType = new SerializedType() { Value = typeof(AssetBundleProvider) };
                }
                settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryModified, eventData: null,
                    postEvent: true, settingsModified: true);
            });
            
            toggle = AddrUtility.CreateToggle(box, "Optimized Build (for local bundles)",
                "ローカル用に最適化したAddressablesビルドを行います。\n" +
                "リモートコンテンツを扱う場合、またはEditorでのAssetBundleの挙動を確認する場合は使用しないでください。\n\n" +
                "Build Addressables with optimization for local bundles.\n" +
                "Do not use if you have remote contents or verify bundle work on Editor.",
                groupingSettings.useLocalBuild);
            toggle.RegisterValueChangedCallback((evt) =>
            {
                groupingSettings.useLocalBuild = evt.newValue;
            });
            
            toggle = AddrUtility.CreateToggle(box, "Clear Build Cache",
                "Build Cacheを消去します。\n\n" +
                "Clear all build cache before building Addressables.", groupingSettings.clearBuildCache);
            toggle.RegisterValueChangedCallback((evt) =>
            {
                groupingSettings.clearBuildCache = evt.newValue;
            });

            var button = AddrUtility.CreateButton(box, "Addressables Build",
                "Addressablesビルドを行います。\n\n" +
                "Build Addressables defaults. Please use this if you want to confirm on Editor.");            
            button.style.width = SINGLE_BUTTON_WIDTH;
            button.style.alignSelf = new StyleEnum<Align>(Align.Center);
            button.clicked += () =>
            {
                var buildPackedModeType = typeof(BuildScriptPackedMode);
                if (groupingSettings.useLocalBuild)
                    buildPackedModeType = typeof(OptimizedBuildScriptPackedMode);
                var dataBuilder = settings.DataBuilders.Find(s => s.GetType() == buildPackedModeType);
                if (dataBuilder == null)
                {
                    // create builder
                    var savePath = $"Assets/{nameof(OptimizedBuildScriptPackedMode)}.asset";
                    dataBuilder = ScriptableObject.CreateInstance<OptimizedBuildScriptPackedMode>();
                    AssetDatabase.CreateAsset(dataBuilder, savePath);
                    settings.DataBuilders.Add(dataBuilder);
                }
                settings.ActivePlayerDataBuilderIndex = settings.DataBuilders.IndexOf(dataBuilder);
                
                // verify settings for Provider
                if (groupingSettings.useLocalProvider)
                {
                    settings.BundledAssetProviderType = new SerializedType() { Value = typeof(LocalBundledAssetProvider) };
                    settings.AssetBundleProviderType = new SerializedType() { Value = typeof(LocalAssetBundleProvider) };
                }
                else
                {
                    settings.BundledAssetProviderType = new SerializedType() { Value = typeof(BundledAssetProvider) };
                    settings.AssetBundleProviderType = new SerializedType() { Value = typeof(AssetBundleProvider) };
                }
                foreach (var group in settings.groups)
                {
                    var schema = group.GetSchema<BundledAssetGroupSchema>();
                    schema.BundleNaming = groupingSettings.hashName
                        ? BundledAssetGroupSchema.BundleNamingStyle.FileNameHash
                        : BundledAssetGroupSchema.BundleNamingStyle.NoHash;
                }
                settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryModified, eventData: null,
                    postEvent: true, settingsModified: true);
                
                // clear cache
                if (groupingSettings.clearBuildCache)
                {
                    AddressableAssetSettings.CleanPlayerContent(settings.GetDataBuilder(settings.ActivePlayerDataBuilderIndex));
                    BuildCache.PurgeCache(false);
                }
                
                // build
                AddressableAssetSettings.BuildPlayerContent(out var result);
            };
            
            root.Add(box);
        }

        static void OptionalButtons(VisualElement root)
        {
            var box = new VisualElement();
            box.style.flexDirection = FlexDirection.Row;
            box.style.alignSelf = new StyleEnum<Align>(Align.Center);
            var button = AddrUtility.CreateButton(box, "Create Address Defines",
                "GUIDのAddress定義クラスを出力します。\n\n" +
                "[optional] Create a file that is defined GUID of entries.");
            button.style.width = TWIN_BUTTON_WIDTH;
            button.style.alignSelf = new StyleEnum<Align>(Align.Center);
            button.clicked += CreateAddressDefines;
            
            button = AddrUtility.CreateButton(box, "Dump Hash/Bundle Name",
                "Bundle名をHashにした際に元の名前をログに出力します。\n\n" +
                "[optional] Output logs to match hash to bundle name.");
            button.style.width = TWIN_BUTTON_WIDTH;
            button.style.alignSelf = new StyleEnum<Align>(Align.Center);
            button.clicked += DumpBundleName;
            
            root.Add(box);
            
            // integrated in Analyze Window
            // button = AddrUtility.CreateButton(root, "Remove Unused Material properties", 
            //     "未使用のShader Propertyを削除します。\n\n" +
            //     "[optional] Remove unused properties in Material.");
            // button.style.width = SINGLE_BUTTON_WIDTH;
            // button.style.alignSelf = new StyleEnum<Align>(Align.Center);
            // button.clicked += CleanupMaterialProperty;
        }
    }
}
