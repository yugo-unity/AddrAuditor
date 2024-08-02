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
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;
using UnityEngine.UIElements;

namespace UTJ
{
    internal partial class AddrAuditor : EditorWindow
    {
        [MenuItem("UTJ/ADDR Auditor")]
        private static void OpenWindow()
        {
            var window = GetWindow<AddrAuditor>();
            window.titleContent = new GUIContent("ADDR Auditor");
            window.minSize = new Vector2(400f, 500f);
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

            this.UtilityButtons(mainElement, settings);
            
            AddrUtility.CreateSpace(mainElement);

            this.CreateSharedGroups(mainElement, settings, groupingSettings);
            
            AddrUtility.CreateSpace(mainElement);
            
            var button = AddrUtility.CreateButton(mainElement, "Dependencies Graph",
                "依存関係をグラフ化します。\n\n" +
                "Display dependencies as node-graph.");
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
            button.clicked += () =>
            {
                Debug.LogError("TODO");
            };
            
            AddrUtility.CreateSpace(mainElement, 2f);
            
            button = AddrUtility.CreateButton(mainElement, "Create Address Defines",
                "GUIDのAddress定義クラスを出力します。\n\n" +
                "[optional] Create a file that is defined GUID of entries.");
            button.clicked += () =>
            {
                CreateAddressDefines();
            };
            button = AddrUtility.CreateButton(mainElement, "Dump Hash/Bundle Name",
                "Bundle名をHashにした際に元の名前をログに出力します。\n\n" +
                "[optional] Output logs to match hash to bundle name.");
            button.clicked += () =>
            {
                DumpBundleName();
            };
            
            button = AddrUtility.CreateButton(mainElement, "Clean up Material properties", 
                "未使用のShader Propertyを削除します。\n\n" +
                "[optional] Remove unused properties in Material.");
            button.clicked += () =>
            {
                CreateAddressDefines();
            };
        }

        void UtilityButtons(VisualElement root, AddressableAssetSettings settings)
        {
            const float BUTTON_WIDTH = 180f;
            var box = new VisualElement();
            box.style.flexDirection = FlexDirection.Row;
            box.style.alignSelf = new StyleEnum<Align>(Align.Center);
            
            var button = AddrUtility.CreateButton(box, "Open Groups",
                "Addressables Group Windowを開きます。\n\n" +
                "Open Addressables window, just as shortcut.");
            button.style.minWidth = BUTTON_WIDTH;
            button.clicked += () =>
            {
                EditorApplication.ExecuteMenuItem("Window/Asset Management/Addressables/Groups");
            };
            
            button = AddrUtility.CreateButton(box, "Sort Groups",
                "グループを降順ソートします。\n\n" +
                "Sort groups by alphanumeric.");
            button.style.width = BUTTON_WIDTH;
            button.clicked += () =>
            {
                // alphanumericソート
                settings.groups.Sort(AddrUtility.CompareGroup);
                settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryModified, eventData: null,
                    postEvent: true, settingsModified: true);
            };
            
            root.Add(box);
        }
        
        void CreateSharedGroups(VisualElement root, AddressableAssetSettings settings, AddrAutoGroupingSettings groupingSettings)
        {
            var fileNameToggle = AddrUtility.CreateToggle(root,
                "Bundle Name is Hash",
                "自動生成されるShared groupのBundle名をハッシュにします。\n\n" +
                "Set Bundle Naming Mode to File Name Hash for shared groups,",
                groupingSettings.hashName);
            fileNameToggle.RegisterValueChangedCallback((evt) =>
            {
                groupingSettings.hashName = evt.newValue;
                EditorUtility.SetDirty(groupingSettings);
            });
            var shaderGroupToggle = AddrUtility.CreateToggle(root,
                "Shader Group",
                "Shader専用のグループを作ります。\n\n" +
                "[optional] Create all used shader group.",
                groupingSettings.shaderGroup);
            shaderGroupToggle.RegisterValueChangedCallback((evt) =>
            {
                groupingSettings.shaderGroup = evt.newValue;
                EditorUtility.SetDirty(groupingSettings);
            });
            var allowDuplicatedMaterial = AddrUtility.CreateToggle(root,
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
            
            this.CreatePopup(root, settings, groupingSettings);
            
            const float BUTTON_WIDTH = 180f;
            var box = new VisualElement();
            box.style.flexDirection = FlexDirection.Row;
            box.style.alignSelf = new StyleEnum<Align>(Align.Center);

            // Remove Button
            var button = AddrUtility.CreateButton(box, "Remove Shared-Groups",
                "自動生成されたグループを一括削除します。\n\n" +
                "Remove all shared groups created automatically.");
            button.style.width = BUTTON_WIDTH;
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
            button.style.width = BUTTON_WIDTH;
            button.clicked += () =>
            {
                // 重複アセット同士の重複アセットが存在するので再帰で行う、念のため上限10回
                for (var i = 0; i < 10; ++i)
                {
                    if (AddrAutoGrouping.Execute(groupingSettings))
                        continue;
                    break;
                }
            };
            
            root.Add(box);
        }

        void CreatePopup(VisualElement root, AddressableAssetSettings settings, AddrAutoGroupingSettings groupingSettings)
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
    }
}
