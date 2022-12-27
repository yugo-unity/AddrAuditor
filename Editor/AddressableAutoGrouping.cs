/***********************************************************************************************************
 * CreateSharedAssetsGroup.cs
 * Copyright (c) Yugo Fujioka - Unity Technologies Japan K.K.
 * 
 * Licensed under the Unity Companion License for Unity-dependent projects--see Unity Companion License.
 * https://unity.com/legal/licenses/unity-companion-license
 * Unless expressly provided otherwise, the Software under this license is made available strictly
 * on an "AS IS" BASIS WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED.
 * Please review the license for details on these and other terms and conditions.
***********************************************************************************************************/

using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Build.AnalyzeRules;
using UnityEditor.AddressableAssets.Build.DataBuilders;
using UnityEditor.AddressableAssets.Build.BuildPipelineTasks;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEditor.Build.Content;
using UnityEditor.Build.Pipeline;
using UnityEngine;
using UnityEngine.U2D;

namespace UTJ
{
    internal class AddressablesAutoGrouping : EditorWindow {

        #region DEFINE
        public const string SHARED_GROUP_NAME = "Shared-";
        public const string SHADER_GROUP_NAME = "Shared-Shader";
        public const string SINGLE_GROUP_NAME = "Shared-Single";

        readonly static string SETTINGS_PATH = "Assets/AddressableGroupingSettings.asset";
        #endregion


        #region MAIN LAYOUT
        [MenuItem("UTJ/ADDR Auto-Grouping Window")]
        private static void OpenWindow() {
            var window = GetWindow<AddressablesAutoGrouping>();
        }

        private void OnEnable() {
            this.titleContent = new GUIContent("ADDR Auto-Grouping");
            var rect = this.position;
            rect.size = new Vector2(400f, 400f);
            this.position = rect;

            // 設定ファイル
            var grouping = AssetDatabase.LoadAssetAtPath<ScriptableObject>(SETTINGS_PATH) as AddressableGroupingSettings;
            if (grouping == null) {
                var obj = ScriptableObject.CreateInstance("AddressableGroupingSettings");
                AssetDatabase.CreateAsset(obj, SETTINGS_PATH);
                grouping = obj as AddressableGroupingSettings;
            }

            var settings = AddressableAssetSettingsDefaultObject.Settings;

            var mainElement = this.rootVisualElement;

            AddrUtility.CreateSpace(mainElement);

            {
                AddrUtility.CreateHelpBox(mainElement, "自動生成されたグループを一括削除します\n開発時やテストに使用してください");

                // Remove Button
                var removeGroupButton = AddrUtility.CreateButton(mainElement, "Remove Shared Group");
                removeGroupButton.clicked += () => {
                    var deletedGroupList = new List<AddressableAssetGroup>();
                    foreach (var group in settings.groups) {
                        //if (group.ReadOnly && group.GetSchema<PlayerDataGroupSchema>() == null)
                        if (group.name.Contains(SHARED_GROUP_NAME) ||
                            group.name.Contains(SHADER_GROUP_NAME) ||
                            group.name.Contains(SINGLE_GROUP_NAME))
                            deletedGroupList.Add(group);
                    }
                    foreach (var group in deletedGroupList)
                        settings.RemoveGroup(group);
                };
            }

            AddrUtility.CreateSpace(mainElement);

            {
                AddrUtility.CreateHelpBox(mainElement, "重複アセットを解決するShared Assets Groupを作成します\nエントリ済のAssetは変更されません");

                var fileNameToggle = AddrUtility.CreateToggle(mainElement,
                    "Bundle Name is Hash",
                    "Bundleのファイル名をハッシュ値にします。開発中は無効とした方が便利です。",
                    grouping.hashName);
                var shaderGroupToggle = AddrUtility.CreateToggle(mainElement,
                    "Shader Group",
                    "Shader専用のグループを作ります。最終的にメモリに適したグルーピングを行ってください。",
                    grouping.shaderGroup);
                var thresholdField = AddrUtility.CreateInteger(mainElement,
                    "Threshold (KiB)",
                    "ファイルサイズが閾値を超える場合にSingleグループに割り振ります。0の場合は行いません。LZ4圧縮された後のサイズではないので検証目的で使用してください。",
                    grouping.singleThreshold);

                var createGroupButton = AddrUtility.CreateButton(mainElement, "Create Shared Assets Group");
                createGroupButton.clicked += () => {
                    // 実行したら設定ファイル保存
                    grouping.hashName = fileNameToggle.value;
                    grouping.shaderGroup = shaderGroupToggle.value;
                    grouping.singleThreshold = thresholdField.value;

                    var instance = new CreateSharedAssetsGroup();
                    // 重複アセット同士の重複アセットが存在するので再帰で行う、念のため上限10回
                    for (var i = 0; i < 10; ++i) {
                        if (instance.Execute(fileNameToggle.value, shaderGroupToggle.value, thresholdField.value))
                            continue;
                        break;
                    }
                };
            }

            AddrUtility.CreateSpace(mainElement);

            {
                AddrUtility.CreateHelpBox(mainElement, "依存アセットを全て個別bundleにします\n検証に使用してください");

                var implicitGroupButton = AddrUtility.CreateButton(mainElement, "Create Implicit Group (All single)");
                implicitGroupButton.clicked += () => {
                    var instance = new CreateSharedAssetsGroup();
                    instance.ExecuteSingle();
                };
            }
        }
        #endregion


        /// <summary>
        /// Addressablesの最適な重複解決の自動グルーピング
        /// 暗黙の依存Asset（ImplicitAssets）を検出して同じ依存関係を持つAssetとグループ（＝Assetbundle）をまとめる
        /// </summary>
        class CreateSharedAssetsGroup : BundleRuleBase {
            delegate bool IsPathCallback(string path);
            IsPathCallback IsPathValidForEntry = null;
            delegate long GetMemorySizeLongCallback(Texture tex);
            GetMemorySizeLongCallback GetStorageMemorySizeLong = null;
            ExtractDataTask ExtractData = null;

            public CreateSharedAssetsGroup() {
                // Utilityの取得
                var aagAssembly = typeof(AddressableAssetGroup).Assembly;
                var aauType = aagAssembly.GetType("UnityEditor.AddressableAssets.Settings.AddressableAssetUtility");
                var validMethod = aauType.GetMethod("IsPathValidForEntry", BindingFlags.Static | BindingFlags.NonPublic, null, new System.Type[] { typeof(string) }, null);
                this.IsPathValidForEntry = System.Delegate.CreateDelegate(typeof(IsPathCallback), validMethod) as IsPathCallback;

                var editorAssembly = typeof(TextureImporter).Assembly;
                var utilType = editorAssembly.GetType("UnityEditor.TextureUtil");
                var utilMethod = utilType.GetMethod("GetStorageMemorySizeLong", BindingFlags.Static | BindingFlags.Public, null, new System.Type[] { typeof(Texture) }, null);
                this.GetStorageMemorySizeLong = System.Delegate.CreateDelegate(typeof(GetMemorySizeLongCallback), utilMethod) as GetMemorySizeLongCallback;
            }

            /// <summary>
            /// SharedAssetグループの情報
            /// </summary>
            class SharedGroupParam {
                public string name = SHARED_GROUP_NAME + "{0}";
                public List<string> bundles;                // 依存先のbundle
                public List<ImplicitParam> implicitParams;  // 含まれる暗黙のAsset
            }

            /// <summary>
            /// 暗黙の依存Assetの収集情報
            /// </summary>
            private class ImplicitParam {
                public string guid;
                public string path;
                public bool isSubAsset;             // SubAssetかどうか
                public List<System.Type> usedType;  // 使用されているSubAssetの型（fbxと用）
                public List<string> bundles;        // 参照されているBundle
                public long fileSize;               // Assetのファイルサイズ
            }

            public void ExecuteSingle() {
                var settings = AddressableAssetSettingsDefaultObject.Settings;

                // Analyze共通処理
                ClearAnalysis();
                if (!BuildUtility.CheckModifiedScenesAndAskToSave()) {
                    Debug.LogError("Cannot run Analyze with unsaved scenes");
                    return;
                }
                CalculateInputDefinitions(settings);
                var context = GetBuildContext(settings);
                var exitCode = RefreshBuild(context);
                if (exitCode < ReturnCode.Success) {
                    Debug.LogError($"Analyze build failed. {exitCode}");
                    return;
                }
                // NOTE: 1.20以降はReflection不要
                //this.extractData = this.ExtractData;
                var extractDataField = this.GetType().GetField("m_ExtractData", BindingFlags.Instance | BindingFlags.NonPublic);
                this.ExtractData = (ExtractDataTask)extractDataField.GetValue(this);

                // 暗黙の依存Asset情報を抽出
                var implicitParams = new List<ImplicitParam>();
                this.GetImplicitAssetsParam(context, implicitParams, null);

                // Group振り分け
                var singleGroup = settings.groups.Find(group => { return (group.name.Contains(SINGLE_GROUP_NAME)); });
                if (singleGroup == null) {
                    singleGroup = CreateSharedGroup(settings, SINGLE_GROUP_NAME, false);
                    var schema = singleGroup.GetSchema<BundledAssetGroupSchema>();
                    schema.BundleMode = BundledAssetGroupSchema.BundlePackingMode.PackSeparately;
                }
                foreach (var implicitParam in implicitParams) {
                    var entry = settings.CreateOrMoveEntry(implicitParam.guid, singleGroup, readOnly: false, postEvent: false);
                    var addr = System.IO.Path.GetFileNameWithoutExtension(implicitParam.path);
                    entry.SetAddress(addr, postEvent: false);
                }
                // 空だったら不要
                if (singleGroup.entries.Count == 0)
                    settings.RemoveGroup(singleGroup);

                // alphanumericソート
                settings.groups.Sort(CompareGroup);

                // 反映
                settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryModified, eventData: null, postEvent: true, settingsModified: true);
            }

            /// <summary>
            /// 重複アセット解決の実行
            /// </summary>
            /// <param name="bundleNameIsHash">自動生成するグループのbundle名をHashにするか</param>
            /// <param name="collectShader">Shaderグループを作るか</param>
            /// <param name="thresholdSingleAsset">Singleグループ行きの閾値</param>
            /// <returns>再帰処理するか</returns>
            public bool Execute(bool bundleNameIsHash, bool collectShader, int thresholdSingleAsset) {
                var settings = AddressableAssetSettingsDefaultObject.Settings;

                // 単品のbundleにするAssetのファイルサイズの閾値
                var SEPARATE_ASSET_SIZE = (long)thresholdSingleAsset * 1024L;

                // Analyze共通処理
                ClearAnalysis();
                if (!BuildUtility.CheckModifiedScenesAndAskToSave()) {
                    Debug.LogError("Cannot run Analyze with unsaved scenes");
                    return false;
                }
                CalculateInputDefinitions(settings);
                var context = GetBuildContext(settings);
                var exitCode = RefreshBuild(context);
                if (exitCode < ReturnCode.Success) {
                    Debug.LogError($"Analyze build failed. {exitCode}");
                    return false;
                }
                // 1.20以降はReflection不要
                //this.extractData = this.ExtractData;
                var extractDataField = this.GetType().GetField("m_ExtractData", BindingFlags.Instance | BindingFlags.NonPublic);
                this.ExtractData = (ExtractDataTask)extractDataField.GetValue(this);

                // 暗黙の依存Asset情報を抽出
                var implicitParams = new List<ImplicitParam>();
                var atlases = new List<SpriteAtlas>();
                this.GetImplicitAssetsParam(context, implicitParams, atlases);

                // 既に配置されてるSharedAssetグループ数
                var sharedGroupCount = settings.groups.FindAll(group => { return (group.name.Contains(SHARED_GROUP_NAME)); }).Count;

                var sharedGroupParams = new List<SharedGroupParam>();
                var collectionGroupParams = new List<SharedGroupParam>();
                var shaderGroupParam = new SharedGroupParam() {
                    name = SHADER_GROUP_NAME,
                    implicitParams = new List<ImplicitParam>(),
                };
                var singleGroupParam = new SharedGroupParam() {
                    name = SINGLE_GROUP_NAME,
                    implicitParams = new List<ImplicitParam>(),
                };

                foreach (var implicitParam in implicitParams) {
                    if (collectShader) {
                        // Shaderグループにまとめる
                        var assetType = implicitParam.usedType[0];
                        if (assetType == typeof(Shader)) {
                            shaderGroupParam.implicitParams.Add(implicitParam);
                            continue;
                        }
                    }

                    // 指定サイズより大きい場合は単品のbundleにする
                    if (SEPARATE_ASSET_SIZE > 0 && implicitParam.fileSize > SEPARATE_ASSET_SIZE) {
                        var single = true;
                        if (implicitParam.isSubAsset && implicitParam.usedType[0] == typeof(Sprite)) {
                            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(implicitParam.path);
                            foreach (var atlas in atlases) {
                                if (atlas.CanBindTo(sprite)) {
                                    Debug.LogWarning($"Skip sprite in atlas : {implicitParam.path}");
                                    single = false;
                                    break;
                                }
                            }
                        }
                        if (single)
                            singleGroupParam.implicitParams.Add(implicitParam);
                        continue;
                    }

                    // 非重複Assetは何もしない
                    if (implicitParam.bundles.Count == 1)
                        continue;

                    // 既存検索
                    var hit = sharedGroupParams.Count > 0; // 初回対応
                    foreach (var groupParam in sharedGroupParams) {
                        // まず依存数（重複数）が違う
                        if (groupParam.bundles.Count != implicitParam.bundles.Count) {
                            hit = false;
                            continue;
                        }
                        // 依存先（重複元）が同一のbundleか
                        hit = true;
                        foreach (var bundle in implicitParam.bundles) {
                            if (!groupParam.bundles.Contains(bundle)) {
                                hit = false;
                                break;
                            }
                        }
                        if (hit) {
                            groupParam.implicitParams.Add(implicitParam);
                            break;
                        }
                    }
                    // 新規Group
                    if (!hit) {
                        sharedGroupParams.Add(
                            new SharedGroupParam() {
                                bundles = implicitParam.bundles,
                                implicitParams = new List<ImplicitParam>() { implicitParam },
                            });
                    }
                }

                var continued = sharedGroupParams.Count > 0;

                // Shaderグループ
                if (collectShader)
                    sharedGroupParams.Add(shaderGroupParam);

                // 単一Group振り分け
                var singleGroup = CreateSharedGroup(settings, SINGLE_GROUP_NAME, bundleNameIsHash);
                var schema = singleGroup.GetSchema<BundledAssetGroupSchema>();
                schema.BundleMode = BundledAssetGroupSchema.BundlePackingMode.PackSeparately;
                CreateOrMoveEntry(settings, singleGroup, singleGroupParam);

                // Group振り分け
                foreach (var groupParam in sharedGroupParams) {
                    // 1個しかAssetがないGroupは単一グループにまとめる
                    var group = singleGroup;

                    if (groupParam.implicitParams.Count > 1) {
                        var name = string.Format(groupParam.name, sharedGroupCount);
                        group = CreateSharedGroup(settings, name, bundleNameIsHash);
                        sharedGroupCount++;
                    }

                    CreateOrMoveEntry(settings, group, groupParam);
                }
                // 空だったら不要
                if (singleGroup.entries.Count == 0)
                    settings.RemoveGroup(singleGroup);

                // alphanumericソート
                settings.groups.Sort(CompareGroup);

                // 反映
                settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryModified, eventData: null, postEvent: true, settingsModified: true);

                return continued;
            }

            /// <summary>
            /// SharedAsset用のGroupの作成
            /// </summary>
            static AddressableAssetGroup CreateSharedGroup(AddressableAssetSettings settings, string groupName, bool useHashName) {
                // Shared-SingleとShared-Shaderは単一
                var group = settings.FindGroup(groupName);
                if (group == null) {
                    var groupTemplate = settings.GetGroupTemplateObject(0) as AddressableAssetGroupTemplate;
                    group = settings.CreateGroup(groupName, setAsDefaultGroup: false, readOnly: false, postEvent: false, groupTemplate.SchemaObjects);
                }
                var schema = group.GetSchema<BundledAssetGroupSchema>();
                // NOTE: 依存Assetなのでcatalogに登録は省略（catalog.jsonの削減）
                schema.IncludeAddressInCatalog = false;
                schema.IncludeGUIDInCatalog = false;
                schema.IncludeLabelsInCatalog = false;

                schema.BundleMode = BundledAssetGroupSchema.BundlePackingMode.PackTogether;
                schema.AssetLoadMode = UnityEngine.ResourceManagement.ResourceProviders.AssetLoadMode.AllPackedAssetsAndDependencies; // for LZ4
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
            static void CreateOrMoveEntry(AddressableAssetSettings settings, AddressableAssetGroup group, SharedGroupParam groupParam) {
                foreach (var implicitParam in groupParam.implicitParams) {
                    var entry = settings.CreateOrMoveEntry(implicitParam.guid, group, readOnly: false, postEvent: false);
                    var addr = System.IO.Path.GetFileNameWithoutExtension(implicitParam.path);
                    entry.SetAddress(addr, postEvent: false);
                }
            }

            /// <summary>
            /// 暗黙の依存Assetを抽出して情報をまとめる
            /// </summary>
            private void GetImplicitAssetsParam(AddressableAssetsBuildContext context, List<ImplicitParam> implicitParams, List<SpriteAtlas> atlases) {
                var validImplicitGuids = new Dictionary<GUID, ImplicitParam>();

                foreach (var fileToBundle in this.ExtractData.WriteData.FileToBundle) {
                    if (this.ExtractData.WriteData.FileToObjects.TryGetValue(fileToBundle.Key, out var objects)) {
                        // NOTE: 参照が全てくるので同一ファイルから複数の参照がくる
                        foreach (var objectId in objects) {
                            var guid = objectId.guid;
                            // PostProcessingVolumeやPlayableなどインスタンスがないアセットが存在
                            var instance = ObjectIdentifier.ToObject(objectId);
                            var type = instance != null ? instance.GetType() : null;

                            // SpriteAtlasは単品チェックでバラのテクスチャが引っかからないように集めておく
                            if (atlases != null && type == typeof(SpriteAtlas))
                                atlases.Add(instance as SpriteAtlas);

                            // EntryされていないImplicitなAssetか
                            if (this.ExtractData.WriteData.AssetToFiles.ContainsKey(guid))
                                continue;

                            // Group検索
                            var bundle = fileToBundle.Value;
                            // Built-in Shadersはグループが見つからない
                            if (!context.bundleToAssetGroup.TryGetValue(bundle, out var groupGUID))
                                continue;
                            var selectedGroup = context.Settings.FindGroup(findGroup => findGroup.Guid == groupGUID);
                            var path = AssetDatabase.GUIDToAssetPath(guid);
                            var isSubAsset = instance != null ? AssetDatabase.IsSubAsset(instance) : false;

                            // Resourcesの重複は警告するが許容する
                            // NOTE: 多くのプロジェクトでTextMeshProが利用されるがTextMeshProがResources前提で設計されるので許容せざるを得ない
                            if (!this.IsPathValidForEntry(path))
                                continue;
                            if (path.Contains("/Resources/"))
                                Debug.LogWarning($"Resources is duplicated. - {path} / Group : {selectedGroup.name}");

                            // Lightmapはシーンに依存するので無視
                            if (path.Contains("Lightmap-"))
                                continue;

                            if (validImplicitGuids.TryGetValue(guid, out var param)) {
                                if (type != null && !param.usedType.Contains(type))
                                    param.usedType.Add(type);
                                if (!param.bundles.Contains(bundle))
                                    param.bundles.Add(bundle);
                                param.isSubAsset &= isSubAsset;
                            } else {
                                var fullPath = Application.dataPath.Replace("/Assets", "");
                                if (path.Contains("Packages/"))
                                    fullPath = System.IO.Path.GetFullPath(path);
                                else
                                    fullPath = System.IO.Path.Combine(fullPath, path);

                                // Textureは圧縮フォーマットでサイズが著しく変わるので対応する
                                // NOTE: AssetBundleのLZ4圧縮後の結果は流石に内容物によって変わるのでビルド前チェックは無理
                                var fileSize = 0L;
                                if (instance is Texture)
                                    fileSize = this.GetStorageMemorySizeLong(instance as Texture);
                                if (fileSize == 0L)
                                    fileSize = new System.IO.FileInfo(fullPath).Length;

                                param = new ImplicitParam() {
                                    guid = guid.ToString(),
                                    path = path,
                                    isSubAsset = isSubAsset,
                                    usedType = new List<System.Type>() { type },
                                    bundles = new List<string>() { bundle },
                                    fileSize = fileSize,
                                };
                                validImplicitGuids.Add(guid, param);
                            }

                            // 確認用
                            //Debug.Log($"{implicitPath} / Entry : {explicitPath} / Group : {selectedGroup.name}");
                        }
                    }
                }

                implicitParams.AddRange(validImplicitGuids.Values);
            }

            static System.Text.RegularExpressions.Regex NUM_REGEX = new System.Text.RegularExpressions.Regex(@"[^0-9]");
            /// <summary>
            /// Addressables Groupのalphanumericソート
            /// </summary>
            private static int CompareGroup(AddressableAssetGroup a, AddressableAssetGroup b) {
                if (a.name == "Built In Data")
                    return -1;
                if (b.name == "Built In Data")
                    return 1;
                if (a.IsDefaultGroup())
                    return -1;
                if (b.IsDefaultGroup())
                    return 1;
                //if (a.ReadOnly && !b.ReadOnly)
                //    return 1;
                //if (!a.ReadOnly && b.ReadOnly)
                //    return -1;
                if (a.name.Contains(SHARED_GROUP_NAME) && !b.name.Contains(SHARED_GROUP_NAME))
                    return 1;
                if (!a.name.Contains(SHARED_GROUP_NAME) && b.name.Contains(SHARED_GROUP_NAME))
                    return -1;

                var ret = string.CompareOrdinal(a.name, b.name);
                // 桁数の違う数字を揃える
                var regA = NUM_REGEX.Replace(a.name, "");
                var regB = NUM_REGEX.Replace(b.name, "");
                if ((regA.Length > 0 && regB.Length > 0) && regA.Length != regB.Length) {
                    if (ret > 0 && regA.Length < regB.Length)
                        return -1;
                    else if (ret < 0 && regA.Length > regB.Length)
                        return 1;
                }

                return ret;
            }
        }
    }
}
