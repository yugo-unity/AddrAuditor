using UnityEngine;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using System.IO;
using System.Collections.Generic;

/// <summary>
/// Addressablesをロードする際のKey定義を事前に行う
/// メリット ... typoの防止、GUIDに統一することでのcatalogサイズの削減、意図しない同一address名の発見
/// デメリット ... エントリを追加する度に行う必要がある
/// 
/// NOTE:
/// 本スクリプトではaddressのユニークを保証されていないとエラーになる
/// Addressablesでは同一アドレスを許容しているがリモートのbundle切り替えの為なのでローカルでは許容しない
/// リモートアセットについてはKey値をハードコーディングするのは良くない
/// よって本スクリプトはローカルのみを考慮し、型とアドレスが共に同一のエントリがある場合はコンパイルエラーとなる、で正しい
/// </summary>
namespace UTJ
{
	internal partial class AddrAuditor : EditorWindow
	{
		// 出力先ディレクトリ
		private static readonly string makeClassDirectoryPathWithAssets = "Assets/";

		// ファイル名
		private static readonly string buildInfomationClassFileName = "AddressableKeys.cs";

		private struct EntryPair
		{
			public string address;
			public string guid;
		}

		/// <summary>
		/// AddressablesのKey定義ファイルを出力する
		/// </summary>
		public static void CreateAddressDefines()
		{
			var date = System.DateTime.Now.ToString("yyyy/MM/dd/HH:mm");

			var groups = AddressableAssetSettingsDefaultObject.Settings.groups;

			var typeAddr = new Dictionary<System.Type, List<EntryPair>>();

			var code = $"// Created by AddressableKeyDefine.cs {date}\n";
			foreach (var g in groups)
			{
				var schema = g.GetSchema<BundledAssetGroupSchema>();

				// Built-in Group
				if (schema == null)
					continue;

				if (schema.IncludeAddressInCatalog || schema.IncludeGUIDInCatalog)
				{
					// GUIDを使うのでAddress不要
					schema.IncludeGUIDInCatalog = true;
					schema.IncludeAddressInCatalog = false;

					void CollectEntries(System.Type type, string addr, string guid)
					{
						var newEntry = new EntryPair { address = addr, guid = guid };
						if (typeAddr.TryGetValue(type, out var list))
						{
							list.Add(newEntry);
						}
						else
						{
							list = new List<EntryPair> { newEntry };
							typeAddr.Add(type, list);
						}
					}

					void CollectFolderEntries(string rootPath, string folderPath, HashSet<string> subEntries)
					{
						var guids = AssetDatabase.FindAssets(string.Empty, new[] { folderPath });
						foreach (var guid in guids)
						{
							var assetPath = AssetDatabase.GUIDToAssetPath(guid);

							if (AssetDatabase.IsValidFolder(assetPath))
							{
								CollectFolderEntries(rootPath, assetPath, subEntries);
								continue;
							}

							// ディレクトリで再起処理済
							if (subEntries.Contains(guid))
								continue;

							var addr = assetPath.Replace(rootPath, string.Empty);
							addr = addr.Replace("/", "_");
							addr = Path.ChangeExtension(addr, string.Empty);

							var main = AssetDatabase.LoadMainAssetAtPath(assetPath);
							var type = main.GetType();
							// 派生クラスが多いのでまとめる
							if (main is ScriptableObject)
								type = typeof(ScriptableObject);
							else if (main is Texture)
								type = typeof(Texture);

							subEntries.Add(guid); // List.Containでもいいが最大数が読めないので
							CollectEntries(type, addr, guid);
						}
					}

					var subEntries = new HashSet<string>();
					foreach (var e in g.entries)
					{
						// ディレクトリのエントリはPrefixをつける
						// NOTE: なくても、まぁ
						if (e.IsFolder)
						{
							var rootPath = Path.GetDirectoryName(e.AssetPath).Replace("\\", "/") + "/";
							CollectFolderEntries(rootPath, e.AssetPath, subEntries);
						}
						else
						{
							var main = e.MainAsset;
							var type = e.MainAssetType;
							// 派生クラスが多いのでまとめる
							if (main is ScriptableObject)
								type = typeof(ScriptableObject);
							else if (main is Texture)
								type = typeof(Texture);
							CollectEntries(type, e.address, e.guid);
						}
					}
				}
			}

			foreach (var pair in typeAddr)
			{
				var temp = pair.Key.ToString().Split('.');
				var type = temp[temp.Length - 1];

				// 定義名の省略
				// 長さを気にしないならしなくても...
				// 小規模プロジェクトなら型別で分けなくてもいい
				type = type.Replace("GameObject", "GO");
				type = type.Replace("Texture", "Tex");
				type = type.Replace("ScriptableObject", "SO");
				type = type.Replace("SpriteAtlas", "Atlas");
				type = type.Replace("SceneAsset", "Scene");
				type = type.Replace("TextAsset", "Text"); // bytesもこちら
				type = type.Replace("ShaderVariantCollection", "SVC");
				type = type.Replace("AudioClip", "Audio");
				type = type.Replace("AnimationClip", "Anim");
				type = type.Replace("AnimationController", "AnimCtrl");
				type = type.Replace("AnimatorOverrideController", "AnimOver");
				type = type.Replace("Material", "Mat");
				type = type.Replace("PhysicMaterial", "PhyMat");

				code += $"\npublic static class ADDR_{type.ToUpper()}" + " {\n";
				foreach (var e in pair.Value)
				{
					var addr = Path.GetFileNameWithoutExtension(e.address);

					// @不可
					addr = addr.Replace("@", "");
					// スペース不可
					addr = addr.Replace(" ", "_");
					// ハイフン不可
					addr = addr.Replace("-", "_");
					// ()不可
					addr = addr.Replace("(", "_");
					addr = addr.Replace(")", "_");
					// 数字開始不可
					if (addr[0] >= '0' && addr[0] <= '9')
						addr = $"_{addr}";

					code += $"\tpublic const string {addr.ToUpper()} = \"{e.guid}\";\n";
				}

				code += "}\n";
			}

			var filePath = makeClassDirectoryPathWithAssets + buildInfomationClassFileName;
			File.WriteAllText(filePath, code, System.Text.Encoding.UTF8);
			AssetDatabase.Refresh();
		}
	}
}