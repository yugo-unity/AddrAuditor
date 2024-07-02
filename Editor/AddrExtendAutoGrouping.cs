/***********************************************************************************************************
 * AddrExtendAutoGrouping.cs
 * Copyright (c) Yugo Fujioka - Unity Technologies Japan K.K.
 * 
 * Licensed under the Unity Companion License for Unity-dependent projects--see Unity Companion License.
 * https://unity.com/legal/licenses/unity-companion-license
 * Unless expressly provided otherwise, the Software under this license is made available strictly
 * on an "AS IS" BASIS WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED.
 * Please review the license for details on these and other terms and conditions.
***********************************************************************************************************/

using System.Reflection;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Build.BuildPipelineTasks;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEditor.Build.Content;
using UnityEditor.Build.Pipeline;
using UnityEditor.Build.Pipeline.Interfaces;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.U2D;
using UnityEngine.UIElements;

namespace UTJ
{
    class AddrExtendAutoGrouping : EditorWindow
    {
        public const string SHARED_GROUP_NAME = "+Shared_";
        public const string SHADER_GROUP_NAME = "+Shared_Shader";
        public const string SINGLE_GROUP_NAME = "+Shared_Single";
        public const string RESIDENT_GROUP_NAME = "+Residents";

        const string SETTINGS_PATH = "Assets/AddrExtendGroupingSettings.asset";


        [MenuItem("UTJ/ADDR Auto-Grouping Window")]
        private static void OpenWindow()
        {
            var window = GetWindow<AddrExtendAutoGrouping>();
            window.titleContent = new GUIContent("ADDR Auto-Grouping");
            window.minSize = new Vector2(400f, 450f);
            window.Show();
        }

        public void CreateGUI()
        {
            // 設定ファイル
            var groupingSettings = AssetDatabase.LoadAssetAtPath<AddrExtendGroupingSettings>(SETTINGS_PATH);
            if (groupingSettings == null)
            {
                groupingSettings = ScriptableObject.CreateInstance<AddrExtendGroupingSettings>();
                AssetDatabase.CreateAsset(groupingSettings, SETTINGS_PATH);
            }
            
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            var mainElement = this.rootVisualElement;
            defaultGroupGuid = settings.DefaultGroup.Guid;
            
            AddrUtility.CreateSpace(mainElement);

            this.OpenGroupWindow(mainElement, settings);
            
            AddrUtility.CreateSpace(mainElement);

            this.SortGroups(mainElement, settings);
            
            AddrUtility.CreateSpace(mainElement);

            this.ClearSharedGroups(mainElement, settings);
            
            AddrUtility.CreateSpace(mainElement);

            this.CreateSharedGroups(mainElement, settings, groupingSettings);
        }

        void OpenGroupWindow(VisualElement root, AddressableAssetSettings settings)
        {
            AddrUtility.CreateHelpBox(root, "Addressables Group Windowを開きます");
            
            var sortGroupButton = AddrUtility.CreateButton(root, "Open Addressable Group Window");
            sortGroupButton.clicked += () =>
            {
                EditorApplication.ExecuteMenuItem("Window/Asset Management/Addressables/Groups");
            };
        }

        void SortGroups(VisualElement root, AddressableAssetSettings settings)
        {
            AddrUtility.CreateHelpBox(root, "グループを降順ソートします");
            
            var sortGroupButton = AddrUtility.CreateButton(root, "Sort Groups");
            sortGroupButton.clicked += () =>
            {
                // alphanumericソート
                settings.groups.Sort(CompareGroup);
                settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryModified, eventData: null,
                    postEvent: true, settingsModified: true);
            };
        }
        
        void ClearSharedGroups(VisualElement root, AddressableAssetSettings settings)
        {
            AddrUtility.CreateHelpBox(root, "自動生成されたグループを一括削除します\n開発時やテストに使用してください");
            
            // Remove Button
            var removeGroupButton = AddrUtility.CreateButton(root, "Remove Shared-Groups");
            removeGroupButton.clicked += () =>
            {
                var deletedGroupList = new List<AddressableAssetGroup>();
                foreach (var group in settings.groups)
                {
                    //if (group.ReadOnly && group.GetSchema<PlayerDataGroupSchema>() == null)
                    if (group.Name.Contains(SHARED_GROUP_NAME) ||
                        group.Name.Contains(SHADER_GROUP_NAME) ||
                        group.Name.Contains(SINGLE_GROUP_NAME) ||
                        group.Name.Contains(RESIDENT_GROUP_NAME))
                        deletedGroupList.Add(group);
                }
            
                foreach (var group in deletedGroupList)
                    settings.RemoveGroup(group);
                // alphanumericソート
                settings.groups.Sort(CompareGroup);
                settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryModified, eventData: null,
                    postEvent: true, settingsModified: true);
            };
        }
        
        void CreateSharedGroups(VisualElement root, AddressableAssetSettings settings, AddrExtendGroupingSettings groupingSettings)
        {
            AddrUtility.CreateHelpBox(root, "重複アセットを解決するShared Assets Groupを作成します\nエントリ済のAssetは変更されません");
            
            var fileNameToggle = AddrUtility.CreateToggle(root,
                "Bundle Name is Hash",
                "Bundleのファイル名をハッシュ値にします。開発中は無効とした方が便利です。",
                groupingSettings.hashName);
            fileNameToggle.RegisterValueChangedCallback((evt) =>
            {
                groupingSettings.hashName = evt.newValue;
                EditorUtility.SetDirty(groupingSettings);
            });
            var shaderGroupToggle = AddrUtility.CreateToggle(root,
                "Shader Group",
                "Shader専用のグループを作ります。最終的にメモリに適したグルーピングを行ってください。",
                groupingSettings.shaderGroup);
            shaderGroupToggle.RegisterValueChangedCallback((evt) =>
            {
                groupingSettings.shaderGroup = evt.newValue;
                EditorUtility.SetDirty(groupingSettings);
            });
            var allowDuplicatedMaterial = AddrUtility.CreateToggle(root,
                "Allow duplicated materials",
                "Materialの重複を許容します。過剰なbundleの細分化は避けた方がベターです。",
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
            
            var createGroupButton = AddrUtility.CreateButton(root, "Create Shared Assets Group");
            createGroupButton.clicked += () =>
            {
                var instance = new MakerSharedAssetsGroup();
                // 重複アセット同士の重複アセットが存在するので再帰で行う、念のため上限10回
                for (var i = 0; i < 10; ++i)
                {
                    if (instance.Execute(groupingSettings))
                        continue;
                    break;
                }
            };
        }

        void CreatePopup(VisualElement root, AddressableAssetSettings settings, AddrExtendGroupingSettings groupingSettings)
        {
            var choices = new List<string>(settings.groups.Count + 1) { "none" };
            settings.groups.Sort(CompareGroup);
            foreach (var group in settings.groups)
            {
                if (group.Name.Contains(SHARED_GROUP_NAME) ||
                    group.Name.Contains(SHADER_GROUP_NAME) ||
                    group.Name.Contains(SINGLE_GROUP_NAME) ||
                    group.Name.Contains(RESIDENT_GROUP_NAME))
                    continue;
                choices.Add(group.Name);
            }

            var field = new DropdownField("Resident Group", choices, 0);
            field.tooltip = "常駐アセットのグループがあれば指定してください、依存関係にあるアセットが重複アセットとして検出された場合、Residentsとしてまとめられます。";
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

        #region SORTING
        static readonly System.Text.RegularExpressions.Regex NUM_REGEX = new System.Text.RegularExpressions.Regex(@"[^0-9]");
        static string defaultGroupGuid = "";

        /// <summary>
        /// Addressables Groupのalphanumericソート
        /// </summary>
        static int CompareGroup(AddressableAssetGroup a, AddressableAssetGroup b)
        {
            // Legacy...
            // if (a.name == "Built In Data")
            //     return -1;
            // if (b.name == "Built In Data")
            //     return 1;
            
            //if (a.IsDefaultGroup()) // 内部でソート中のgroupsを毎回検索するのでおかしくなる
            if (a.Guid == defaultGroupGuid)
                return -1;
            //if (b.IsDefaultGroup())
            if (b.Guid == defaultGroupGuid)
                return 1;
            //if (a.ReadOnly && !b.ReadOnly)
            //    return 1;
            //if (!a.ReadOnly && b.ReadOnly)
            //    return -1;
            if (a.name[0] == '+' && b.name[0] != '+')
                return 1;
            if (a.name[0] != '+' && b.name[0] == '+')
                return -1;

            var ret = string.CompareOrdinal(a.name, b.name);
            // 桁数の違う数字を揃える
            var regA = NUM_REGEX.Replace(a.name, "");
            var regB = NUM_REGEX.Replace(b.name, "");
            if ((regA.Length > 0 && regB.Length > 0) && regA.Length != regB.Length)
            {
                if (ret > 0 && regA.Length < regB.Length)
                    return -1;
                else if (ret < 0 && regA.Length > regB.Length)
                    return 1;
            }

            return ret;
        }
        #endregion


        #region GROUPING
        /// <summary>
        /// Addressablesの最適な重複解決の自動グルーピング
        /// 暗黙の依存Asset（ImplicitAssets）を検出して同じ依存関係を持つAssetとグループ（＝Assetbundle）をまとめる
        /// </summary>
        class MakerSharedAssetsGroup
        {
            delegate bool IsPathCallback(string path);
            static IsPathCallback IsPathValidForEntry;

            // delegate long GetMemorySizeLongCallback(Texture tex);
            // GetMemorySizeLongCallback GetStorageMemorySizeLong = null;

            public MakerSharedAssetsGroup()
            {
                // Utilityの取得
                if (IsPathValidForEntry == null)
                {
                    var aagAssembly = typeof(AddressableAssetGroup).Assembly;
                    var aauType = aagAssembly.GetType("UnityEditor.AddressableAssets.Settings.AddressableAssetUtility");
                    var validMethod = aauType.GetMethod("IsPathValidForEntry",
                        BindingFlags.Static | BindingFlags.NonPublic,
                        null, new System.Type[] { typeof(string) }, null);
                    if (validMethod != null)
                    {
                        IsPathValidForEntry =
                            System.Delegate.CreateDelegate(typeof(IsPathCallback), validMethod) as IsPathCallback;
                    }
                    else
                    {
                        Debug.LogError("Failed Reflection - IsPathValidForEntry ");
                    }
                }
                // // 圧縮されたテクスチャのファイルサイズ取得
                // var editorAssembly = typeof(TextureImporter).Assembly;
                // var utilType = editorAssembly.GetType("UnityEditor.TextureUtil");
                // var utilMethod = utilType.GetMethod("GetStorageMemorySizeLong",
                //     BindingFlags.Static | BindingFlags.Public, null, new System.Type[] { typeof(Texture) }, null);
                // this.GetStorageMemorySizeLong =
                //     System.Delegate.CreateDelegate(typeof(GetMemorySizeLongCallback), utilMethod) as
                //         GetMemorySizeLongCallback;
            }

            /// <summary>
            /// SharedAssetグループの情報
            /// </summary>
            class SharedGroupParam
            {
                public SharedGroupParam(string name, List<string> bundles)
                {
                    this.name = name;
                    this.bundles = bundles;
                }
                public readonly string name;
                public readonly List<string> bundles; // 依存先のbundle
                public readonly List<ImplicitParam> implicitParams = new (); // 含まれる暗黙のAsset
            }

            /// <summary>
            /// 暗黙の依存Assetの収集情報
            /// </summary>
            class ImplicitParam
            {
                public string guid;
                public string path;
                public bool isSubAsset; // SubAssetかどうか
                public bool isResident; // 常駐アセットか
                public List<System.Type> usedType; // 使用されているSubAssetの型（fbxと用）
                public List<string> bundles; // 参照されているBundle
                //public long fileSize; // Assetのファイルサイズ
            }

            class SpriteAtlasParam
            {
                public bool isResident;
                public SpriteAtlas instance;
            }

            /// <summary>
            /// 重複アセット解決の実行
            /// </summary>
            /// <param name="groupingSettings">自動グルーピング設定</param>
            /// <returns>再帰処理するか</returns>
            public bool Execute(AddrExtendGroupingSettings groupingSettings)
            {
                var settings = AddressableAssetSettingsDefaultObject.Settings;

                // // 単品のbundleにするAssetのファイルサイズの閾値
                // var SEPARATE_ASSET_SIZE = (long)groupingSettings.singleThreshold * 1024L;

                // Analyze共通処理
                if (!BuildUtility.CheckModifiedScenesAndAskToSave())
                {
                    Debug.LogError("Cannot run Analyze with unsaved scenes");
                    return false;
                }

                //var exitCode = AddrUtility.CalculateBundleWriteData(out var aaContext, out var extractData);
                var allBundleInputDefs = new List<AssetBundleBuild>();
                var bundleToAssetGroup = new Dictionary<string, string>();
                AddrUtility.CalculateInputDefinitions(settings, allBundleInputDefs, bundleToAssetGroup);
                var aaContext = AddrUtility.GetBuildContext(settings, bundleToAssetGroup);
                var extractData = new ExtractDataTask();
                var exitCode = AddrUtility.RefleshBuild(settings, allBundleInputDefs, extractData, aaContext);
                if (exitCode < ReturnCode.Success)
                {
                    Debug.LogError($"Analyze build failed. {exitCode}");
                    return false;
                }
                
                // 暗黙の依存Asset情報を抽出
                var (implicitParams, atlases) =
                    CollectImplicitParams(aaContext.bundleToAssetGroup, extractData.WriteData, groupingSettings);

                // 既に配置されてるSharedAssetグループ数
                var sharedGroupCount = settings.groups.FindAll(group => group.name.Contains(SHARED_GROUP_NAME)).Count;

                var sharedGroupParams = new List<SharedGroupParam>();
                var shaderGroupParam = new SharedGroupParam(SHADER_GROUP_NAME, null);
                var singleGroupParam = new SharedGroupParam(SINGLE_GROUP_NAME, null);
                var residentGroupParam = new SharedGroupParam(RESIDENT_GROUP_NAME, null);

                foreach (var implicitParam in implicitParams)
                {
                    // 重複している常駐アセットは一つのGroupにまとめる
                    var residentAsset = implicitParam.isResident && implicitParam.bundles.Count > 1;
                    
                    // Spriteはかなり例外処理なのでSpriteAtlas確認が必要
                    if (implicitParam.usedType.Contains(typeof(Sprite)))
                    {
                        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(implicitParam.path);
                        var packed = false;
                        foreach (var atlas in atlases)
                        {
                            // NOTE: SpriteAtlasに含まれているどうかがインスタンスでないとわからない...？
                            if (atlas.instance.CanBindTo(sprite))
                            {
                                // NOTE: SpriteAtlasに含まれているSpriteは無視
                                packed = implicitParam.usedType.Count == 1;
                                // NOTE: SpriteAtlasに含まれているSpriteの元テクスチャが参照されている、かつ
                                //       元テクスチャは常駐アセットと依存関係を持たない場合、
                                //       SpriteAtlasが常駐なら元Textureも常駐扱いとする
                                //       結局常駐グループに依存関係を持つため循環参照により不要なbundleが生成されてしまうのを避ける
                                residentAsset |= atlas.isResident;
                                
                                break;
                            }
                        }

                        if (packed)
                            continue;
                    }
                    
                    if (residentAsset)
                    {
                        residentGroupParam.implicitParams.Add(implicitParam);
                        continue;
                    }
                    
                    // Shader検出
                    // NOTE: 常駐のShaderは常駐グループにまとめられる
                    if (groupingSettings.shaderGroup)
                    {
                        // Shaderグループにまとめる
                        var assetType = implicitParam.usedType[0];
                        if (assetType == typeof(Shader))
                        {
                            shaderGroupParam.implicitParams.Add(implicitParam);
                            continue;
                        }
                    }

                    // // 指定サイズより大きい場合は単品のbundleにする
                    // if (SEPARATE_ASSET_SIZE > 0 && implicitParam.fileSize > SEPARATE_ASSET_SIZE)
                    // {
                    //     singleGroupParam.implicitParams.Add(implicitParam);
                    //     continue;
                    // }

                    // 非重複Assetは何もしない
                    if (implicitParam.bundles.Count == 1)
                        continue;

                    // 既存検索
                    var hit = sharedGroupParams.Count > 0; // 初回対応
                    foreach (var groupParam in sharedGroupParams)
                    {
                        // まず依存数（重複数）が違う
                        if (groupParam.bundles.Count != implicitParam.bundles.Count)
                        {
                            hit = false;
                            continue;
                        }

                        // 依存先（重複元）が同一のbundleか
                        hit = true;
                        foreach (var bundle in implicitParam.bundles)
                        {
                            if (!groupParam.bundles.Contains(bundle))
                            {
                                hit = false;
                                break;
                            }
                        }

                        if (hit)
                        {
                            groupParam.implicitParams.Add(implicitParam);
                            break;
                        }
                    }

                    // 新規Group
                    if (!hit)
                    {
                        var param = new SharedGroupParam(SHARED_GROUP_NAME + "{0}", implicitParam.bundles);
                        param.implicitParams.Add(implicitParam);
                        sharedGroupParams.Add(param);
                    }
                }

                var continued = sharedGroupParams.Count > 0;
                
                // 常駐グループ
                if (residentGroupParam.implicitParams.Count > 0)
                    sharedGroupParams.Add(residentGroupParam);

                // Shaderグループ
                if (groupingSettings.shaderGroup)
                    sharedGroupParams.Add(shaderGroupParam);

                // 単一Group振り分け
                var singleGroup = CreateSharedGroup(settings, SINGLE_GROUP_NAME, groupingSettings.hashName);
                var schema = singleGroup.GetSchema<BundledAssetGroupSchema>();
                schema.BundleMode = BundledAssetGroupSchema.BundlePackingMode.PackSeparately;
                CreateOrMoveEntry(settings, singleGroup, singleGroupParam);

                // Group振り分け
                foreach (var groupParam in sharedGroupParams)
                {
                    // 1個しかAssetがないGroupは単一グループにまとめる
                    var group = singleGroup;

                    if (groupParam.implicitParams.Count > 1)
                    {
                        var name = string.Format(groupParam.name, sharedGroupCount);
                        group = CreateSharedGroup(settings, name, groupingSettings.hashName);
                        sharedGroupCount++;
                    }

                    CreateOrMoveEntry(settings, group, groupParam);
                }

                // 空だったら不要
                if (singleGroup.entries.Count == 0)
                    settings.RemoveGroup(singleGroup);

                // alphanumericソート
                defaultGroupGuid = settings.DefaultGroup.Guid;
                settings.groups.Sort(CompareGroup);
                settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryModified, eventData: null,
                    postEvent: true, settingsModified: true);

                return continued;
            }

            /// <summary>
            /// SharedAsset用のGroupの作成
            /// </summary>
            static AddressableAssetGroup CreateSharedGroup(AddressableAssetSettings settings, string groupName,
                bool useHashName)
            {
                // Shared-SingleとShared-Shaderは単一
                var group = settings.FindGroup(groupName);
                if (group == null)
                {
                    var groupTemplate = settings.GetGroupTemplateObject(0) as AddressableAssetGroupTemplate;
                    if (groupTemplate == null)
                    {
                        Debug.LogError("Not found AddressableAssetGroupTemplate");
                        return null;
                    }
                    group = settings.CreateGroup(groupName, false, true, false,
                        groupTemplate.SchemaObjects);
                }

                var schema = group.GetSchema<BundledAssetGroupSchema>();
                if (schema == null)
                    schema = group.AddSchema<BundledAssetGroupSchema>();
                        
                // NOTE: 必ず無効にしないと反映されない
                schema.UseDefaultSchemaSettings = false;

                // NOTE: 依存Assetなのでcatalogに登録は省略（catalog.jsonの削減）
                schema.IncludeAddressInCatalog = false;
                schema.IncludeGUIDInCatalog = false;
                schema.IncludeLabelsInCatalog = false;

                schema.BundleMode = BundledAssetGroupSchema.BundlePackingMode.PackTogether;
                schema.AssetLoadMode = UnityEngine.ResourceManagement.ResourceProviders.AssetLoadMode
                    .AllPackedAssetsAndDependencies;
                schema.InternalBundleIdMode = BundledAssetGroupSchema.BundleInternalIdMode.GroupGuid;
                schema.InternalIdNamingMode = BundledAssetGroupSchema.AssetNamingMode.Dynamic;
                schema.UseAssetBundleCrc = schema.UseAssetBundleCache = false;
                if (useHashName)
                    schema.BundleNaming = BundledAssetGroupSchema.BundleNamingStyle.FileNameHash;
                else
                    schema.BundleNaming = BundledAssetGroupSchema.BundleNamingStyle.NoHash;

                return group;
            }

            /// <summary>
            /// 指定Groupへエントリ
            /// </summary>
            static void CreateOrMoveEntry(AddressableAssetSettings settings, AddressableAssetGroup group,
                SharedGroupParam groupParam)
            {
                foreach (var implicitParam in groupParam.implicitParams)
                {
                    var entry = settings.CreateOrMoveEntry(implicitParam.guid, group, false, false);
                    var addr = System.IO.Path.GetFileNameWithoutExtension(implicitParam.path);
                    entry.SetAddress(addr, false);
                }
            }

            /// <summary>
            /// 暗黙の依存Assetを抽出して情報をまとめる
            /// </summary>
            static (List<ImplicitParam>, List<SpriteAtlasParam>) CollectImplicitParams(
                Dictionary<string, string> bundleToAssetGroup,
                IBundleWriteData writeData, AddrExtendGroupingSettings groupingSettings)
            {
                var settings = AddressableAssetSettingsDefaultObject.Settings;
                var validImplicitGuids = new Dictionary<GUID, ImplicitParam>();
                var atlases = new List<SpriteAtlasParam>();

                foreach (var fileToBundle in writeData.FileToBundle)
                {
                    if (writeData.FileToObjects.TryGetValue(fileToBundle.Key, out var objects))
                    {
                        // NOTE: 参照が全てくるので同一ファイルから複数の参照がくる
                        foreach (var objectId in objects)
                        {
                            var guid = objectId.guid;

                            // EntryされてるExplicit Assetなら無視
                            if (writeData.AssetToFiles.ContainsKey(guid))
                                continue;

                            // Group検索
                            var bundle = fileToBundle.Value;
                            // NOTE: Built-in Shadersはグループが見つからない
                            if (!bundleToAssetGroup.TryGetValue(bundle, out var groupGuid))
                                continue;
                            var path = AssetDatabase.GUIDToAssetPath(guid);

                            // Resourcesがエントリされている場合は警告するが許容する
                            // NOTE: 多くのプロジェクトでTextMeshProが利用されるがTextMeshProがResources前提で設計されるので許容せざるを得ない
                            if (!IsPathValidForEntry(path))
                                continue;
                            if (path.Contains("/Resources/"))
                            {
                                var selectedGroup = settings.FindGroup(g => g.Guid == groupGuid);
                                Debug.LogWarning($"Resources is duplicated. - {path} / Group : {selectedGroup.name}");
                            }

                            // Lightmapはシーンに依存するので無視
                            if (path.Contains("Lightmap-"))
                                continue;
                            
                            // NOTE: PostProcessingVolumeやPlayableなどインスタンスがないアセットが存在
                            var instance = ObjectIdentifier.ToObject(objectId);
                            var type = instance != null ? instance.GetType() : null;

                            // NOTE: Materialはファイルサイズが小さいので重複を許容して過剰なbundleを避ける
                            if (groupingSettings.allowDuplicatedMaterial)
                            {
                                if (type == typeof(Material))
                                    continue;
                            }

                            var isSubAsset = instance != null && AssetDatabase.IsSubAsset(instance);
                            var isResident = groupGuid == groupingSettings.residentGroupGUID;

                            if (validImplicitGuids.TryGetValue(guid, out var param))
                            {
                                if (type != null && !param.usedType.Contains(type))
                                    param.usedType.Add(type);
                                if (!param.bundles.Contains(bundle))
                                    param.bundles.Add(bundle);
                                param.isSubAsset &= isSubAsset;
                                param.isResident |= isResident;
                            }
                            else
                            {
                                // // Textureは圧縮フォーマットでサイズが著しく変わるので対応する
                                // // NOTE: AssetBundleのLZ4圧縮後の結果は流石に内容物によって変わるのでビルド前チェックは無理
                                // var fullPath = "";
                                // if (path.Contains("Packages/"))
                                //     fullPath = System.IO.Path.GetFullPath(path);
                                // else
                                //     fullPath = System.IO.Path.Combine(Application.dataPath.Replace("/Assets", ""),
                                //         path);
                                // var fileSize = 0L;
                                // if (instance is Texture)
                                //     fileSize = this.GetStorageMemorySizeLong(instance as Texture);
                                // if (fileSize == 0L)
                                //     fileSize = new System.IO.FileInfo(fullPath).Length;

                                param = new ImplicitParam()
                                {
                                    guid = guid.ToString(),
                                    path = path,
                                    isSubAsset = isSubAsset,
                                    isResident = isResident,
                                    usedType = new List<System.Type>() { type },
                                    bundles = new List<string>() { bundle },
                                    //fileSize = fileSize,
                                };
                                validImplicitGuids.Add(guid, param);
                                
                                // SpriteAtlasは単品チェックでバラのテクスチャが引っかからないように集めておく
                                if (type == typeof(SpriteAtlas))
                                {
                                    var atlasParam = new SpriteAtlasParam()
                                    {
                                        isResident = isResident,
                                        instance = instance as SpriteAtlas,
                                    };
                                    atlases.Add(atlasParam);
                                }
                            }

                            // 確認用
                            //Debug.Log($"{implicitPath} / Entry : {explicitPath} / Group : {selectedGroup.name}");
                        }
                    }
                }

                var implicitParams = new List<ImplicitParam>(validImplicitGuids.Values);
                return (implicitParams, atlases);
            }
        }
        #endregion
    }
}
