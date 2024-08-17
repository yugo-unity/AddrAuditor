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

using System;
using System.Reflection;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build.DataBuilders;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine;
using UnityEngine.ResourceManagement.Util;
using UnityEngine.UIElements;

namespace UTJ
{
    internal partial class AddrAuditor
    {
        const int RECURSIVE_COUNT = 10;
        const float SINGLE_BUTTON_WIDTH = 366f;
        const float TWIN_BUTTON_WIDTH = 180f;
        
        [MenuItem("UTJ/ADDR Auditor")]
        private static void OpenWindow()
        {
            var window = GetWindow<AddrAuditor>();
            window.titleContent = new GUIContent("ADDR Auditor");
            window.minSize = new Vector2(400f, 650f);
            window.Show();
        }

        public void CreateGUI()
        {
            // 設定ファイル
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
            
            var button = AddrUtility.CreateButton(mainElement, "Dependencies Graph",
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
            
            button = AddrUtility.CreateButton(mainElement, "Analyze & Suggest any settings",
                "プロジェクトを解析して設定の提案を行います。\n\n" +
                "Analyze Addressables settings and Suggest better one for console.");
            button.style.color = Color.gray;
            button.style.width = SINGLE_BUTTON_WIDTH;
            button.style.alignSelf = new StyleEnum<Align>(Align.Center);
            button.clicked += () =>
            {
                throw new ArithmeticException("TODO: new feature");
            };
            
            AddrUtility.CreateSpace(mainElement);
            
            BundleNamingButtons(mainElement, settings);
            ProviderButtons(mainElement, settings);
            
            AddrUtility.CreateSpace(mainElement);
            
            BuildButtons(mainElement, settings);
            
            AddrUtility.CreateSpace(mainElement);
            
            OptionalButtons(mainElement);
        }

        static void UtilityButtons(VisualElement root, AddressableAssetSettings settings)
        {
            var box = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignSelf = new StyleEnum<Align>(Align.Center)
                }
            };

            var button = AddrUtility.CreateButton(box, "Open Groups",
                "Addressables Group Windowを開きます。\n\n" +
                "Open Addressables window, just as shortcut.");
            button.style.minWidth = TWIN_BUTTON_WIDTH;
            button.clicked += () =>
            {
                EditorApplication.ExecuteMenuItem("Window/Asset Management/Addressables/Groups");
            };
            
            button = AddrUtility.CreateButton(box, "Sort Groups",
                "グループを降順ソートします。\n\n" +
                "Sort groups by alphanumeric.");
            button.style.width = TWIN_BUTTON_WIDTH;
            button.clicked += () =>
            {
                // alphanumericソート
                settings.groups.Sort(AddrUtility.CompareGroup);
                settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryModified, eventData: null,
                    postEvent: true, settingsModified: true);
            };
            
            root.Add(box);
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
            box.style.alignSelf = new StyleEnum<Align>(Align.Center);
            box.style.minWidth = 371f;
            
            var shaderGroupToggle = AddrUtility.CreateToggle(box,
                "Create Shader Group",
                "Shader専用のグループを作ります。\n\n" +
                "[optional] Create all used shader group.",
                groupingSettings.shaderGroup);
            shaderGroupToggle.RegisterValueChangedCallback((evt) =>
            {
                groupingSettings.shaderGroup = evt.newValue;
                EditorUtility.SetDirty(groupingSettings);
            });
            
            var allowDuplicatedMaterial = AddrUtility.CreateToggle(box,
                "Allow duplicated materials",
                "Materialの重複を許容します。過剰なbundleの細分化は避けた方がベターです。\n\n" +
                "You can ignore duplicated materials. It is better to do if their size are smaller than 32KB.",
                groupingSettings.allowDuplicatedMaterial);
            allowDuplicatedMaterial.RegisterValueChangedCallback((evt) =>
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
            box.style.alignSelf = new StyleEnum<Align>(Align.Center);

            // Remove Button
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
        
        static void BundleNamingButtons(VisualElement root, AddressableAssetSettings settings)
        {
            var box = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignSelf = new StyleEnum<Align>(Align.Center)
                }
            };

            var button = AddrUtility.CreateButton(box, "Bundle Naming\nFilename",
                "出力されるBundle名をファイル名にします。\n\n" +
                "Set Bundle Naming Mode to File Name.");
            button.style.minWidth = TWIN_BUTTON_WIDTH;
            button.clicked += () =>
            {
                foreach (var group in settings.groups)
                {
                    var schema = group.GetSchema<BundledAssetGroupSchema>();
                    schema.BundleNaming = BundledAssetGroupSchema.BundleNamingStyle.NoHash;
                }
                settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryModified, eventData: null,
                    postEvent: true, settingsModified: true);
                settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryModified, eventData: null,
                    postEvent: true, settingsModified: true);
            };
            
            button = AddrUtility.CreateButton(box, "Bundle Naming\nHash of Filename",
                "出力されるBundle名をハッシュにします。\n\n" +
                "Set Bundle Naming Mode to File Name Hash.");
            button.style.width = TWIN_BUTTON_WIDTH;
            button.clicked += () =>
            {
                foreach (var group in settings.groups)
                {
                    var schema = group.GetSchema<BundledAssetGroupSchema>();
                    schema.BundleNaming = BundledAssetGroupSchema.BundleNamingStyle.FileNameHash;
                }
                settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryModified, eventData: null,
                    postEvent: true, settingsModified: true);
            };
            
            root.Add(box);
        }
        
        static void ProviderButtons(VisualElement root, AddressableAssetSettings settings)
        {
            var box = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignSelf = new StyleEnum<Align>(Align.Center)
                }
            };

            var button = AddrUtility.CreateButton(box, "Default Provider",
                "GroupSchemaに標準のProviderを設定します。\n\n" +
                "Set default providers to all GroupSchemas.");
            button.style.minWidth = TWIN_BUTTON_WIDTH;
            button.clicked += () =>
            {
                foreach (var group in settings.groups)
                {
                    var schema = group.GetSchema<BundledAssetGroupSchema>();
                    schema.BundledAssetProviderType = new SerializedType() { Value = typeof(BundledAssetProvider) };
                    schema.AssetBundleProviderType = new SerializedType() { Value = typeof(AssetBundleProvider) };
                }
                settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryModified, eventData: null,
                    postEvent: true, settingsModified: true);
            };
            
            button = AddrUtility.CreateButton(box, "Optimized Local Provider",
                "GroupSchemaにLocal向けのProviderを設定します。\n\n" +
                "Set local providers to all GroupSchemas.");
            button.style.width = TWIN_BUTTON_WIDTH;
            //button.style.backgroundColor = new StyleColor(Color.yellow * 0.5f); // TODO: set yellow if selected  
            button.clicked += () =>
            {
                foreach (var group in settings.groups)
                {
                    var schema = group.GetSchema<BundledAssetGroupSchema>();
                    schema.BundledAssetProviderType = new SerializedType() { Value = typeof(LocalBundledAssetProvider) };
                    schema.AssetBundleProviderType = new SerializedType() { Value = typeof(LocalAssetBundleProvider) };
                }
                settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryModified, eventData: null,
                    postEvent: true, settingsModified: true);
            };
            
            root.Add(box);
        }
        
        static void BuildButtons(VisualElement root, AddressableAssetSettings settings)
        {
            var box = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignSelf = new StyleEnum<Align>(Align.Center)
                }
            };

            var button = AddrUtility.CreateButton(box, "Default Build",
                "標準のAddressablesビルドを行います。EditorでのAssetBundleの挙動を確認する場合もこちらを使用してください。\n\n" +
                "Build Addressables defaults. Please use this if you want to confirm on Editor.");
            button.style.minWidth = TWIN_BUTTON_WIDTH;
            button.style.color = Color.green;
            button.clicked += () =>
            {
                var dataBuilder = settings.DataBuilders.Find(s => s.GetType() == typeof(BuildScriptPackedMode));
                settings.ActivePlayerDataBuilderIndex = settings.DataBuilders.IndexOf(dataBuilder);
                AddressableAssetSettings.BuildPlayerContent(out var result);
            };
            
            button = AddrUtility.CreateButton(box, "Optimized Build",
                "不要なデータを除外したAddressablesビルドを行います。リモートコンテンツとEditorでの動作は考慮されないことに注意してください。\n\n" +
                "Build Addressables with stripping unused data. Please attention that Remote contents and work on Editor are not supported.");
            button.style.width = TWIN_BUTTON_WIDTH;
            button.style.color = Color.green;
            button.clicked += () =>
            {
                // NOTE: StripUnityVersion is internal property, it should be public.
                var type = typeof(AddressableAssetSettings);
                var prop = type.GetProperty("StripUnityVersionFromBundleBuild", BindingFlags.NonPublic | BindingFlags.Instance);
                var stripVersion = prop.GetValue(settings);
                prop.SetValue(settings, false);

                var dataBuilder = settings.DataBuilders.Find(s => s.GetType() == typeof(OptimizedBuildScriptPackedMode));
                if (dataBuilder == null)
                {
                    throw new ArgumentException("TODO: Automatically create SerializedObject as OptimizedBuildScriptPackedMode");
                }
                settings.ActivePlayerDataBuilderIndex = settings.DataBuilders.IndexOf(dataBuilder);
                AddressableAssetSettings.BuildPlayerContent(out var result);
                
                prop.SetValue(settings, stripVersion);
            };
            
            root.Add(box);
        }

        static void OptionalButtons(VisualElement root)
        {
            var button = AddrUtility.CreateButton(root, "Create Address Defines",
                "GUIDのAddress定義クラスを出力します。\n\n" +
                "[optional] Create a file that is defined GUID of entries.");
            button.style.width = SINGLE_BUTTON_WIDTH;
            button.style.alignSelf = new StyleEnum<Align>(Align.Center);
            button.clicked += CreateAddressDefines;
            
            button = AddrUtility.CreateButton(root, "Dump Hash/Bundle Name",
                "Bundle名をHashにした際に元の名前をログに出力します。\n\n" +
                "[optional] Output logs to match hash to bundle name.");
            button.style.width = SINGLE_BUTTON_WIDTH;
            button.style.alignSelf = new StyleEnum<Align>(Align.Center);
            button.clicked += DumpBundleName;
            
            button = AddrUtility.CreateButton(root, "Remove Unused Material properties", 
                "未使用のShader Propertyを削除します。\n\n" +
                "[optional] Remove unused properties in Material.");
            button.style.width = SINGLE_BUTTON_WIDTH;
            button.style.alignSelf = new StyleEnum<Align>(Align.Center);
            button.clicked += CleanupMaterialProperty;
        }
    }
}
