# SteamVR_tools

SteamVR の PICO Connect を有効/無効に切り替える、Windows 用の小さなタスクトレイ常駐ツールです。Pico と Bigscreen Beyond を使い分けるときに、設定を手で編集する手間をなくします。

English: [README.md](../README.md)

## 仕組み

- **Pico モード**: PICO Connect が有効な状態(`driver_pico` に `"enable": false` が無い)。
- **Beyond モード**: PICO Connect が無効な状態(`steamvr.vrsettings` の `driver_pico` に `"enable": false` がある)。
- SteamVR の起動中に切り替えると、確認ダイアログが出ます。**再起動**(SteamVR を終了 → 設定を書き換え → 再起動)、**手動でやる**(設定だけ書き換え)、**キャンセル**から選びます。
- 書き換える前に `steamvr.vrsettings` を毎回バックアップ(直近5世代)し、書き込みに失敗したら元に戻します。
- Steam のインストール先はレジストリから自動検出します。見つからないときだけ、フォルダを選ぶ画面が出ます(一度選べば保存されます)。

## ビルドと実行

Windows と .NET 10 SDK が必要です。

```
dotnet build
dotnet test
dotnet run --project src/SteamVRTools.App
```

設定とバックアップは `%APPDATA%\SteamVRTools\` に保存されます。

## 状況

第1段階(モード切り替え)は実装済みです。設計と未確認の事項(例: Beyond 2e のために PICO Connect 以外に切り替えが必要か)は [docs/design.md](../docs/design.md) を参照してください。
