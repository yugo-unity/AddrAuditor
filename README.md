# ADDR Auditor

supported Addressables 2.1 or later<br>
This is the extension for Addressables if you create the standalone game for PC or any console platforms.

StandaloneやConsole Game向けにAddressablesを扱う際の包括的な拡張ツールです。</br>
主機能として重複アセットの解決を目的としていますがカスタムパッケージを行わない範囲での最適化も含みます。</br>
各種項目の説明はEditorにてマウスオーバーのtooltipにも記載しています。

![image](https://github.com/user-attachments/assets/115db6e1-8dfb-4e27-afe5-55e0f4ddbc5e)


## 1. Dependencies Graph
Addressablesのバンドル間の依存関係を可視化することで意図しない参照を見つけることが可能です。</br>
本機能はGraphViewを用いてバンドルや内包しているアセット間の依存関係を可視化します。

![image](https://github.com/user-attachments/assets/146ce754-c07e-4d70-a98d-a44add828a67)</br>
![image](https://github.com/user-attachments/assets/af42faaf-7739-49e1-8fdd-f9e6605f6001)

## 2. Analyze & Suggest any settings (not implemented)
Addressables Asset SettingsおよびAddressables Groupの設定を解析し、最適なものを提案・設定します。</br>
<b>こちらはまだ未実装です。</b>


## 3. Automatic Shared-Group
Addressablesは各バンドルの参照アセット（暗黙の依存アセット／Implicit Asset）の重複解決策がオフィシャルでは提供されていません。</br>
重複アセットの検出はBuild Layoutから確認することが可能ですが、</br>
解決には複数のバンドルから参照されるアセットの明示的にエントリを作って別のバンドルを生成する必要があります。</br>
本機能はその難解な依存関係の解決を自動で行うことが可能です。

![image](https://github.com/user-attachments/assets/7cb8db39-c40e-457a-ae5c-cee2fd470a94)

- <i>Create Shader Group (optional)</i>
  - 重複アセットでないシェーダーを独立したグループにひとまとめにします
  - 利用されているシェーダーを把握するためのオプションで通常不要です
- <i>Allow Duplicated Materials</i>
  - マテリアルは非常に小さいので重複を許容するとバンドルの数が抑えられロード時のオーバーヘッドを抑える結果になりやすいです
- <i>Resident Group</i>
  - 常駐アセットのグループを指定します。常にメモリに展開されるアセットを同一グループにまとめることで重複解決から除外します

自動生成されるグループ :
- <i>+Residents</i> : 指定した常駐アセットグループです
- <i>+Shared_XX</i> : 重複しているアセットをまとめたグループです、同一の参照をもつアセット毎に生成されます


## 4. Addressables Build
ローカルのバンドルに特化したビルド設定を指定してAddressablesのビルドを行うことができます。

- <i>Bundle Name is Hash of Filename</i>
  - バンドル名をHashにすることによる多少の難読化が得られます
- <i>Use Optimized Provider (for local bundles)</i>
  - ランタイム用のAssetBundleProviderにローカル専用に最適化したものを指定します
  - 具体的にはリモート処理の判定とメモリアロケートの削除が適用されます
  - <b>※2024/08/18現在、Unload時にdelegateインスタンスによるメモリアロケートが残っています</b>
- <i>Use Optimized Build (for local bundles)</i>
  - Addressablesビルドの際にローカル専用に最適化したものを指定します
  - 具体的にはTypeTreeとfbxのRigといった未使用データを削除します
- <i>Clear Build Cache</i>
  - ビルド前にキャッシュをクリアします
  - 本拡張にかかわらずAddressablesの設定を変更した際はクリアした方が無難です
  

## 5. Optional
おまけの機能です。通常使用する必要はありません。

### Create Address Defines
AddressablesのKeyを定義したクラスファイルを生成します。</br>
AssetReferenceの方が推奨されますが小～中規模のプロジェクトではこちらの方が楽かもしれません。

### Dump Hash/Bunlde Name
アセットバンドル名のハッシュとファイル名を確認するためのログを出力します。
確認用です。

### Remove Unused Material Properties
マテリアルにシリアライズされているが使用されていないプロパティ情報を削除します。</br>
マテリアルに以前設定されていたシェーダーが使用していたプロパティがある場合、</br>
シリアライズデータは削除されないので、特にテクスチャ参照があると使用しないテクスチャがアセットバンドルに組み込まれます。</br>
逆にランタイムでマテリアルのシェーダーを切り替える、といったトリッキーな実装をしている場合は削除しない方が良いです。

<b>Analyze & Suggest any settings</b> に統合予定です。


## Planned
- <t>Analyze & Suggest any settings</t>
  - 各種設定を推奨設定する機能を検討中です
- <t>Use Short Load Path</t>
  - Load PathはデフォルトのLocalままだとnamespaceが冗長であり、生成されるバンドル数が多いとカタログサイズを増大させます
  - 独自の短縮したプロパティをラッパーとすることでカタログサイズを抑制することができます
- <t>Readme.md and comments in English</t>
  - 本Readmeの英語対応を検討中です。またコード内コメントは随時英文に置き換え予定です
