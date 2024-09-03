# ADDR Auditor

supported Addressables 2.1 or later<br>

<i>[EN]</i><br>
This is a extension for Addressables if you create Standalone and Console Game. </br>
Its primary function is to resolve duplicate assets, but it also includes optimization without custom packaging. </br>
A description of the various items can be found at the tooltip in the Editor.</br>
<i>[JA]</i><br>
StandaloneやConsole Game向けにAddressablesを扱う際の包括的な拡張ツールです。</br>
主機能として重複アセットの解決を目的としていますがカスタムパッケージを行わない範囲での最適化も含みます。</br>
各種項目の説明はEditorにてマウスオーバーのtooltipにも記載しています。

![image](https://github.com/user-attachments/assets/115db6e1-8dfb-4e27-afe5-55e0f4ddbc5e)


## 1. Dependencies Graph
<i>[EN]</i><br>
It is possible to find unintended references by visualizing the dependencies between bundles/assets in Addressables.<br>
<i>[JA]</i><br>
Addressablesのバンドル間の依存関係を可視化することで意図しない参照を見つけることが可能です。

![image](https://github.com/user-attachments/assets/146ce754-c07e-4d70-a98d-a44add828a67)</br>
![image](https://github.com/user-attachments/assets/af42faaf-7739-49e1-8fdd-f9e6605f6001)

## 2. Analyze & Suggest any settings (not implemented)
<i>[EN]</i><br>
AddressablesAssetSettings and AddressablesGroupSettings are analyzed and better configulations are proposed and configured.</br>
<b>This is not yet implemented.</b><br>
<i>[JA]</i><br>
Addressables Asset SettingsおよびAddressables Groupの設定を解析し、最適なものを提案・設定します。</br>
<b>こちらはまだ未実装です。</b>


## 3. Automatic Shared-Group
<i>[EN]</i><br>
Duplicate asset detection can be checked byBuild Layout, though,<br>
Resolution requires creating another bundle by making explicit entries.<br>
This feature aims to solve automatically such difficult dependencies.<br>
<i>[JA]</i><br>
重複アセットの検出はBuild Layoutから確認することが可能ですが、</br>
解決には複数のバンドルから参照されるアセットの明示的にエントリを作って別のバンドルを生成する必要があります。</br>
本機能はその難解な依存関係の解決を自動で行うことが可能です。

![image](https://github.com/user-attachments/assets/7cb8db39-c40e-457a-ae5c-cee2fd470a94)

- <i>Create Shader Group (optional)</i>
  - <i>[EN]</i> group non-duplicated shaders into a unique group
    - this is an option to keep track of which shaders are being used (this is usually not needed)
  - <i>[JA]</i> 重複アセットでないシェーダーを独立したグループにひとまとめにします
    - 利用されているシェーダーを把握するためのオプションで通常不要です
- <i>Allow Duplicated Materials</i>
  - <i>[EN]</i> materials are very small file-size, so allowing duplicates is likely to result in fewer bundles and less overhead on load
  - <i>[JA]</i> マテリアルは非常に小さいので重複を許容するとバンドルの数が抑えられロード時のオーバーヘッドを抑えやすいです
- <i>Resident Group</i>
  - <i>[EN]</i> create a group of resident assets
    - exclude from duplicate resolution by grouping assets that are always in memory into the same group
  - <i>[JA]</i> 常駐アセットのグループを指定します
    - 常にメモリに展開されるアセットを同一グループにまとめることで重複解決から除外します

自動生成されるグループ : 
- <i>+Residents</i>
  - <i>[EN]</i> resident asset group
  - <i>[JA]</i> 指定した常駐アセットグループです
- <i>+Shared_XX</i>
  - <i>[EN]</i> groups of duplicate assets, generated for each asset with the same reference
  - <i>[JA]</i> 重複しているアセットをまとめたグループです、同一の参照をもつアセット毎に生成されます


## 4. Addressables Build
<br><i>[EN]</i><br>
You can build Addressables with specialized work-flow to build local bundles.
<br><i>[JA]</i><br>
ローカルのバンドルに特化したビルド設定を指定してAddressablesのビルドを行うことができます。

- <i>Bundle Name is Hash of Filename</i>
  - <i>[EN]</i> some obfuscation can be gotton by setting the bundle name to Hash
  - <i>[JA]</i> バンドル名をHashにすることによる多少の難読化が得られます
- <i>Use Optimized Provider (for local bundles)</i>
  - <i>[EN]</i> use a local optimized AssetBundleProvider for runtime
    - specifically, the deleted remote processing and reduced memory allocation apply
    - <b>*As of 2024/08/18, memory allocation by the delegate instance remains during unload</b>
  - <i>[JA]</i> ランタイム用のAssetBundleProviderにローカル専用に最適化したものを指定します
    - 具体的にはリモート処理の判定とメモリアロケートの削除が適用されます
    - <b>※2024/08/18現在、Unload時にdelegateインスタンスによるメモリアロケートが残っています</b>
- <i>Use Optimized Build (for local bundles)</i>
  - <i>[EN]</i> use a local optimized BuildPackedMode for Addressables build
    - specifically, delete unused data such as TypeTree and fbx Rig
  - <i>[JA]</i> Addressablesビルドの際にローカル専用に最適化したものを指定します
    - 具体的にはTypeTreeとfbxのRigといった未使用データを削除します
- <i>Clear Build Cache</i>
  - <i>[EN]</i> clear build caches before building
    - it is safer to clear when Addressables settings are changed
  - <i>[JA]</i> ビルド前にキャッシュをクリアします
    - 本拡張にかかわらずAddressablesの設定を変更した際はクリアした方が無難です
  

## 5. Optional
<br><i>[EN]</i><br>
This is an extra feature. It is usually not necessary to use.
<br><i>[JA]</i><br>
おまけの機能です。通常使用する必要はありません。

### 1. Create Address Defines
<i>[EN]</i><br>
Generate a class file that defines the Keys for Addressables. </br>
AssetReference is recommended, but may be easier for small to medium-sized projects.
<i>[JA]</i><br>
AddressablesのKeyを定義したクラスファイルを生成します。</br>
AssetReferenceの方が推奨されますが小～中規模のプロジェクトではこちらの方が楽かもしれません。

### 2. Dump Hash/Bunlde Name
<i>[EN]</i><br>
Output log to confirm hash of assetbundle name and file name. For confirmation.
<br><i>[JA]</i><br>
アセットバンドル名のハッシュとファイル名を確認するためのログを出力します。確認用です。

### 3. Remove Unused Material Properties
<i>[EN]</i><br>
Removes serialized properties in the material but not used. </br>
If the material has properties that were used by previously configured shaders,</br>
the serialization data is not deleted. For example, unused textures will be contained in the asset bundle. </br>
Conversely, if you have tricky implementations, such as switching shaders at runtime, it is not better to delete it.<br>
This will be integrated <b>Analyze & Suggest any settings</b>.
<br><i>[JA]</i><br>
マテリアルにシリアライズされているが使用されていないプロパティ情報を削除します。</br>
マテリアルに以前設定されていたシェーダーが使用していたプロパティがある場合、</br>
シリアライズデータは削除されないので、特にテクスチャ参照があると使用しないテクスチャがアセットバンドルに組み込まれます。</br>
逆にランタイムでマテリアルのシェーダーを切り替える、といったトリッキーな実装をしている場合は削除しない方が良いです。<br>
<b>Analyze & Suggest any settings</b> に統合予定です。


## Planned
- <t>Support to show dependencies all group</t>
  - <i>[EN]</i> Currently not supported due to expensive but will be offered as an option to see the whole dependencies
  - <i>[JA]</i> 高負荷のために除外していたが全体像を確認するためにオプションとして提供予定
- <t>Analyze & Suggest any settings</t>
  - <i>[EN]</i> The setting various settings as recommended is under consideration
  - <i>[JA]</i> 各種設定を推奨設定する機能を検討中
- <t>Use Short Load Path</t>
  - <i>[EN]</i> if "Load Path" is local by default, namespace is redundant, and the size of the catalog is increased when there are the large number of bundles
    - you can suppress catalog size by using your own shortened property as a wrapper
  - <i>[JA]</i> Load PathはデフォルトのLocalままだとnamespaceが冗長であり、生成されるバンドル数が多いとカタログサイズを増大させる
    - 独自の短縮したプロパティをラッパーとすることでカタログサイズを抑制することができる
