# AddressablesUtilities
Utilities for Addressables. (Auto-Grouping, Dependencies Graph, Custom Provider, etc...)

## Auto Grouping
Addressablesは各bundleの参照アセット（暗黙の依存アセット／Implicit Asset）の重複解決を自動では行わない為、複数のbundleから参照されるアセットは明示的にエントリする必要がある。
付属のAnalyzerから重複アセットの抽出を行う事はできるが、最適なグループ分けはされない為にメモリオーバーヘッドが発生する。

本ツールはメモリオーバーヘッドを最小限にするようImplicit Assetのグループ分けを行う。
![image](https://user-images.githubusercontent.com/57246289/197981267-f9144513-780b-427a-a4c1-3ae6a8a61da8.png)

【自動生成されるグループ】
- Shared-XX : 重複しているアセット
- Shared-Shader : 参照されているShader、Shader Groupのチェックがついていると生成される、ある程度の粒度で再振り分けしておくとよりベター
- Shared-Single : Pack Separately設定で個別bundleになるアセット、Thresholdの値にひっかかったものもこちらにエントリされる

## Dependencies Graph
Addressablesは生成されるbundle間の依存関係を確認する手段がない為、どのように構築されているのか（≒任意のアセットをロードする際にどのbundleがロードされるのか）知ることができない。

本ツールはGraphViewを用いてbundleや内包しているアセット間の依存関係を可視化する。
![image](https://user-images.githubusercontent.com/57246289/198171904-6632b2aa-e228-497b-97fb-0b02dde04efd.png)
