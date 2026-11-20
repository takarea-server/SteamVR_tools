# Pico/Beyond モード切り替えツール 実装計画

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** PICO Connect の有効/無効(Pico モード / Beyond モード)を、ウィンドウとタスクトレイからワンアクションで切り替える Windows 常駐ツールを作る(設計ドキュメントの第1段階)。

**Architecture:** 切り替えロジックは UI から分離した Core ライブラリ(`SteamVRTools.Core`)に置き、WinForms の App は呼び出すだけにする(将来 UI を差し替えられるようにするため)。切り替えは `steamvr.vrsettings` の `driver_pico` セクションの `enable` を JSON として書き換える。書き換え前に必ずバックアップし、失敗したら復元する。プロセス操作とダイアログは interface/コールバックにして、Core を単体テストできるようにする。

**Tech Stack:** C# / .NET 10(`net10.0-windows`)、WinForms、System.Text.Json(JsonNode)、xUnit。

**Spec:** [docs/design.md](../../design.md)(特に第2章、第5章)

## Global Constraints

- 対象 OS は Windows のみ。
- 常駐するので軽量に保つ。外部の NuGet 依存は xUnit(テストのみ)以外に足さない。
- 将来の機能追加(通知、バッテリー、機器状態)を足しやすい構造にする。UI と切り替え処理は分離する。
- 設定ファイルを壊さない。書き換え前にバックアップを取り、失敗したら元に戻す。
- 確認ダイアログの選択肢は「再起動」「手動でやる」「キャンセル」の3つ。SteamVR が起動していないときは確認なしで書き換える。
- SteamVR のアプリIDは 250820。起動は `steam://rungameid/250820`。
- SteamVR の終了は `vrmonitor` のウィンドウを閉じて行う。起動中の判定は `vrmonitor` または `vrserver` のプロセスの有無。
- Pico モード = `driver_pico` セクションに `"enable": false` が無い状態。Beyond モード = `driver_pico` の `"enable"` が `false`。
- Steam のインストール先は、レジストリ `HKCU\Software\Valve\Steam` の `SteamPath`(`c:/program files (x86)/steam` のように小文字で `/` 区切りで返る)から自動検出し、失敗したらユーザー入力。
- 実在するフルパスやユーザー名をコード、テスト、ドキュメントに書かない(リポジトリは公開される)。
- 設計の第5章の項目5(Beyond 2e のために追加で必要な切り替え)は購入後に確認する。この計画の範囲外。

## Review Focus

1. `steamvr.vrsettings` に `driver_pico` が無い、または `steamvr` など他のセクションだけがある: 他のセクションを保ったまま追加/削除できる。
2. `steamvr.vrsettings` が壊れた JSON、空ファイル、存在しない: 書き換えず、分かるエラーを返す。ファイルは変更されない。
3. 書き込みが失敗した(読み取り専用、ファイルロック): バックアップから復元され、元の内容が残る。
4. SteamVR が時間内に終了しない: 設定は書き換えず、エラーを返す(強制終了はしない)。
5. すでに目的のモードになっている: ダイアログを出さず、何も書き換えない。

---

## ファイル構成

| ファイル                                                | 責務                                                                |
| ------------------------------------------------------- | ------------------------------------------------------------------- |
| `SteamVRTools.sln`                                      | ソリューション                                                      |
| `src/SteamVRTools.Core/Mode.cs`                         | `Mode` 列挙と切り替え結果の型                                       |
| `src/SteamVRTools.Core/VrSettingsFile.cs`               | `steamvr.vrsettings` の読み取りと `driver_pico` の書き換え          |
| `src/SteamVRTools.Core/BackupStore.cs`                  | バックアップの作成、世代管理、復元                                  |
| `src/SteamVRTools.Core/SteamLocator.cs`                 | Steam のインストール先の検出                                        |
| `src/SteamVRTools.Core/ISteamVrProcess.cs`              | SteamVR の起動判定・終了・起動の interface                          |
| `src/SteamVRTools.Core/SteamVrProcess.cs`               | 上記の実装(実プロセス)                                            |
| `src/SteamVRTools.Core/ModeSwitcher.cs`                 | 上記を組み合わせた切り替え処理                                      |
| `src/SteamVRTools.App/`                                 | WinForms: トレイ、ウィンドウ、確認ダイアログ、Steam パスの設定保存 |
| `tests/SteamVRTools.Core.Tests/`                        | xUnit。Core の単体テスト                                            |

---

### Task 1: ソリューションの土台と Mode / VrSettingsFile

**Files:**
- Create: `SteamVRTools.sln`, `src/SteamVRTools.Core/SteamVRTools.Core.csproj`(`net10.0-windows`), `tests/SteamVRTools.Core.Tests/SteamVRTools.Core.Tests.csproj`(xUnit、Core を参照)
- Create: `src/SteamVRTools.Core/Mode.cs`, `src/SteamVRTools.Core/VrSettingsFile.cs`
- Test: `tests/SteamVRTools.Core.Tests/VrSettingsFileTests.cs`
- Modify: `.gitignore`(`bin/`、`obj/` を追加)

**Interfaces:**
- Produces:
  - `enum Mode { Pico, Beyond }`
  - `class VrSettingsFile` — `VrSettingsFile(string path)`、`Mode ReadMode()`、`void WriteMode(Mode mode)`
  - 例外 `VrSettingsException : Exception`(ファイル無し、JSON 不正のときに投げる)

- [ ] **Step 1: ソリューションとプロジェクトを作る**。`dotnet new sln`、`classlib`、`xunit` で作り、`dotnet test` が空で通ることを確認する。

- [ ] **Step 2: 失敗するテストを書く**(一時ディレクトリにファイルを作って検証する)。
  - `ReadMode_NoDriverPicoSection_ReturnsPico`
  - `ReadMode_EnableFalse_ReturnsBeyond`
  - `ReadMode_MissingFile_Throws`(`VrSettingsException`)
  - `ReadMode_InvalidJson_Throws`、`ReadMode_EmptyFile_Throws`
  - `WriteMode_Beyond_AddsEnableFalse_KeepsOtherSections`(`{"steamvr":{"a":1}}` → `steamvr` が残り `driver_pico.enable == false`)
  - `WriteMode_Pico_RemovesEnable_RemovesEmptySection_KeepsOtherKeys`(`driver_pico` に `enable` 以外のキーがあれば、そのキーとセクションは残る)
  - `WriteMode_InvalidJson_Throws_FileUnchanged`

- [ ] **Step 3: 失敗を確認する**。Run: `dotnet test` / Expected: コンパイルエラー(型が未定義)。

- [ ] **Step 4: `VrSettingsFile` を実装する**。`System.Text.Json.Nodes.JsonNode` で読み書きし、インデントは元のファイルに合わせてタブにする。書き込みは一時ファイルに書いてから置き換える(中途半端な書き込みを避ける)。

- [ ] **Step 5: テストが通ることを確認する**。Run: `dotnet test` / Expected: 全件 PASS。

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "feat: add VrSettingsFile to read and write the PICO driver mode"
```

---

### Task 2: BackupStore

**Files:**
- Create: `src/SteamVRTools.Core/BackupStore.cs`
- Test: `tests/SteamVRTools.Core.Tests/BackupStoreTests.cs`

**Interfaces:**
- Produces: `class BackupStore` — `BackupStore(string backupDirectory, int generations = 5)`、`string Create(string sourceFile)`(バックアップのパスを返す)、`void Restore(string backupPath, string targetFile)`

- [ ] **Step 1: 失敗するテストを書く**
  - `Create_CopiesFileContent`
  - `Create_BeyondGenerations_DeletesOldest`(`generations: 2` で3回作ると、残るのは新しい2つ)
  - `Restore_OverwritesTargetWithBackup`
  - `Create_MissingSource_Throws`

- [ ] **Step 2: 失敗を確認する**。Run: `dotnet test --filter BackupStoreTests` / Expected: FAIL(型が未定義)。

- [ ] **Step 3: `BackupStore` を実装する**。ファイル名は `<元のファイル名>.<yyyyMMdd-HHmmss-fff>.bak` で、古い順に削除する。

- [ ] **Step 4: テストが通ることを確認する**。Run: `dotnet test --filter BackupStoreTests` / Expected: PASS。

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "feat: add BackupStore with generation limit"
```

---

### Task 3: SteamLocator

**Files:**
- Create: `src/SteamVRTools.Core/SteamLocator.cs`
- Test: `tests/SteamVRTools.Core.Tests/SteamLocatorTests.cs`

**Interfaces:**
- Produces: `static class SteamLocator`
  - `static string? Normalize(string? rawPath)` — `/` を `\` に直し、末尾の区切りを除き、`Path.GetFullPath` を通す。空や空白は `null`
  - `static string? DetectSteamPath()` — レジストリ `HKCU\Software\Valve\Steam` の `SteamPath` を `Normalize` して返す。無ければ `null`
  - `static string VrSettingsPath(string steamPath)` — `<steamPath>\config\steamvr.vrsettings`

- [ ] **Step 1: 失敗するテストを書く**(レジストリを使わない部分のみ)
  - `Normalize_ForwardSlashes_BecomesBackslashes`(`"c:/program files (x86)/steam"` → `@"c:\program files (x86)\steam"`)
  - `Normalize_TrailingSeparator_Removed`
  - `Normalize_NullOrWhitespace_ReturnsNull`
  - `VrSettingsPath_CombinesConfigSubfolder`

- [ ] **Step 2: 失敗を確認する**。Run: `dotnet test --filter SteamLocatorTests` / Expected: FAIL。

- [ ] **Step 3: `SteamLocator` を実装する**。`DetectSteamPath` は `Microsoft.Win32.Registry` を使い、キーが無い場合は例外ではなく `null` にする。

- [ ] **Step 4: テストが通ることを確認する**。Run: `dotnet test --filter SteamLocatorTests` / Expected: PASS。

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "feat: add SteamLocator to detect Steam install path"
```

---

### Task 4: SteamVR プロセス操作

**Files:**
- Create: `src/SteamVRTools.Core/ISteamVrProcess.cs`, `src/SteamVRTools.Core/SteamVrProcess.cs`
- Test: `tests/SteamVRTools.Core.Tests/SteamVrProcessTests.cs`

**Interfaces:**
- Produces:
  - `interface ISteamVrProcess` — `bool IsRunning()`、`Task<bool> CloseAsync(TimeSpan timeout)`(時間内に `vrmonitor` と `vrserver` が両方消えたら `true`)、`void Start()`
  - `class SteamVrProcess : ISteamVrProcess` — 実プロセスを操作する実装。`vrmonitor` のメインウィンドウに `CloseMainWindow()` を送り、`vrserver` が消えるまでポーリングする。時間切れでも強制終了(`Kill`)はしない。起動は `Process.Start` で `steam://rungameid/250820`(`UseShellExecute = true`)

- [ ] **Step 1: 失敗するテストを書く**。実プロセスは使わず、判定ロジックだけを純粋関数に切り出してテストする。
  - `SteamVrProcess.IsSteamVrProcessName("vrmonitor")` / `("vrserver")` が `true`、`("steam")` が `false`(大文字小文字は区別しない)

- [ ] **Step 2: 失敗を確認する**。Run: `dotnet test --filter SteamVrProcessTests` / Expected: FAIL。

- [ ] **Step 3: 実装する**。`static bool IsSteamVrProcessName(string name)` を public にして `IsRunning` から使う。

- [ ] **Step 4: テストが通ることを確認する**。Run: `dotnet test --filter SteamVrProcessTests` / Expected: PASS。

- [ ] **Step 5: 実機で手動確認する**(SteamVR が入っているPCで。結果の記録は不要、動けばよい)。
  - SteamVR 停止中に `IsRunning()` が `false`、起動後に `true`。
  - `CloseAsync(10秒)` で SteamVR が閉じ、`true` が返る。`Start()` で再び起動する。

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "feat: add SteamVrProcess for detecting, closing and starting SteamVR"
```

---

### Task 5: ModeSwitcher

**Files:**
- Create: `src/SteamVRTools.Core/ModeSwitcher.cs`(`SwitchResult` と `RunningChoice` もここに置く)
- Test: `tests/SteamVRTools.Core.Tests/ModeSwitcherTests.cs`(`ISteamVrProcess` のテスト用 fake を同じファイル内に作る)

**Interfaces:**
- Consumes: `VrSettingsFile`(Task 1)、`BackupStore`(Task 2)、`ISteamVrProcess`(Task 4)
- Produces:
  - `enum RunningChoice { Restart, Manual, Cancel }`
  - `enum SwitchOutcome { AlreadyInMode, Switched, SwitchedAndRestarted, SwitchedManualRestartNeeded, Cancelled, Failed }`
  - `record SwitchResult(SwitchOutcome Outcome, string? ErrorMessage)`
  - `class ModeSwitcher` — `ModeSwitcher(VrSettingsFile settings, BackupStore backups, ISteamVrProcess steamVr, TimeSpan closeTimeout)`、`Mode CurrentMode()`、`Task<SwitchResult> SwitchAsync(Mode target, Func<Task<RunningChoice>> askWhenRunning)`

動作の順序:
1. すでに目的のモード → `AlreadyInMode`(ダイアログも書き換えもしない)。
2. SteamVR 起動中 → `askWhenRunning` を呼ぶ。`Cancel` → `Cancelled`。`Manual` → 書き換えのみして `SwitchedManualRestartNeeded`。`Restart` → `CloseAsync`、`false` なら書き換えず `Failed`。
3. SteamVR 停止中 → 確認なしで書き換えて `Switched`。
4. 書き換えは `BackupStore.Create` → `WriteMode`。例外が出たら `Restore` して `Failed`(元の内容が残る)。
5. `Restart` を選んだ場合は書き換え後に `Start()`、結果は `SwitchedAndRestarted`。書き換えに失敗したら `Start()` は呼ばない。

- [ ] **Step 1: 失敗するテストを書く**
  - `Switch_AlreadyInTarget_NoDialogNoWrite`(`askWhenRunning` が呼ばれない、ファイルのバイト列が変わらない)
  - `Switch_NotRunning_WritesWithoutAsking_ReturnsSwitched`
  - `Switch_Running_Cancel_NoWrite`
  - `Switch_Running_Manual_WritesAndDoesNotClose`
  - `Switch_Running_Restart_ClosesWritesStarts_InThatOrder`(fake が呼び出し順を記録する)
  - `Switch_Running_Restart_CloseTimesOut_NoWriteNoStart_ReturnsFailed`
  - `Switch_WriteFails_RestoresBackup_ReturnsFailed`(設定ファイルを排他ロックして書き込みを失敗させ、内容が元のままであることを確認)
  - `Switch_InvalidJson_ReturnsFailedWithMessage_FileUnchanged`
  - `Switch_CreatesBackupBeforeWrite`

- [ ] **Step 2: 失敗を確認する**。Run: `dotnet test --filter ModeSwitcherTests` / Expected: FAIL。

- [ ] **Step 3: `ModeSwitcher` を実装する**。UI を一切参照しない。例外はすべて `SwitchResult(Failed, message)` に変換する。

- [ ] **Step 4: テストが通ることを確認する**。Run: `dotnet test` / Expected: 全件 PASS。

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "feat: add ModeSwitcher orchestrating backup, close, write and restart"
```

---

### Task 6: WinForms App(トレイ + ウィンドウ + ダイアログ)

**Files:**
- Create: `src/SteamVRTools.App/SteamVRTools.App.csproj`(`WinExe`、`net10.0-windows`、`UseWindowsForms`、Core を参照)、`Program.cs`、`MainForm.cs`、`TrayController.cs`、`AppSettings.cs`
- Modify: `SteamVRTools.sln`(App を追加)
- Test: `tests/SteamVRTools.Core.Tests/AppSettingsTests.cs`(`AppSettings` を Core に置く場合。置き場所は実装者が決めてよいが、テストできる側に置くこと)

**Interfaces:**
- Consumes: `ModeSwitcher`、`SteamLocator`、`BackupStore`、`SteamVrProcess`(Task 1〜5)
- Produces:
  - `class AppSettings` — `string? SteamPathOverride`、`static AppSettings Load(string filePath)`、`void Save(string filePath)`。保存先は `%APPDATA%\SteamVRTools\settings.json`、バックアップ先は同じ階層の `backups\`
  - UI: ウィンドウに現在のモード表示と「Pico モード」「Beyond モード」の2ボタン、トレイアイコンの右クリックメニューに同じ2項目と「終了」。現在のモードはウィンドウとトレイのツールチップの両方に表示する

- [ ] **Step 1: 失敗するテストを書く**
  - `AppSettings_SaveThenLoad_RoundTrips`
  - `AppSettings_Load_MissingFile_ReturnsDefault`
  - `AppSettings_Load_InvalidJson_ReturnsDefault`

- [ ] **Step 2: 失敗を確認する**。Run: `dotnet test --filter AppSettingsTests` / Expected: FAIL。

- [ ] **Step 3: `AppSettings` を実装する**。

- [ ] **Step 4: App を実装する**。起動時に `SteamPathOverride` → `SteamLocator.DetectSteamPath()` の順でパスを決める。どちらも無ければフォルダ選択ダイアログで入力させ、保存する。確認ダイアログは「再起動 / 手動でやる / キャンセル」の3ボタンで、`ModeSwitcher.SwitchAsync` の `askWhenRunning` として渡す。結果は `SwitchResult` に応じてバルーン通知かメッセージボックスで出す(`Failed` は `ErrorMessage` を表示)。多重起動は `Mutex` で防ぎ、2つ目は既存のウィンドウを前面に出さず静かに終了してよい。ウィンドウを閉じたらトレイに格納し、トレイメニューの「終了」でのみ終了する。切り替え処理は UI スレッドを塞がないよう `async` で呼ぶ。

- [ ] **Step 5: ビルドとテストを確認する**。Run: `dotnet build` と `dotnet test` / Expected: エラー無し、全件 PASS。

- [ ] **Step 6: 実機で手動確認する**(SteamVR が入っているPCで)。
  1. SteamVR 停止中に Beyond モードへ切り替え → `steamvr.vrsettings` に `driver_pico.enable = false` が入り、バックアップが作られる。Pico モードへ戻すと `driver_pico` が消える。
  2. SteamVR 起動中に切り替え → 3択のダイアログが出る。「再起動」で閉じて書き換えて再起動する。「手動でやる」で書き換えのみ。「キャンセル」で何も変わらない。
  3. すでに目的のモードのとき、何も起きない。
  4. 常駐中のメモリ使用量をタスクマネージャーで見て、極端に大きくない(目安: 数十 MB 以下)ことを確認する。

- [ ] **Step 7: Commit**

```bash
git add -A
git commit -m "feat: add WinForms app with tray and window for mode switching"
```

---

### Task 7: ドキュメントの更新

**Files:**
- Modify: `README.md`、`README/README.jp.md`(ビルド方法、使い方、Steam パスの自動検出、バックアップの保存場所)、`docs/design.md`(ステータスを「承認済み・第1段階実装済み」に更新。項目5は未確認のまま残す)

- [ ] **Step 1: README 2つを更新する**。実在のフルパスやユーザー名は書かない。
- [ ] **Step 2: design.md のステータスを更新する**。
- [ ] **Step 3: Commit**

```bash
git add -A
git commit -m "docs: update README and design status after first stage"
```

---

## 自己レビューの結果

- **設計の網羅**: 目的の切り替え(Task 1, 5)、ウィンドウとトレイ(Task 6)、3択ダイアログと停止中は確認なし(Task 5, 6)、バックアップと復元(Task 2, 5)、軽量さ(Task 6 の実機確認)、UI との分離(Task 5 が UI 非依存)。第2.3章の将来案は含めない。
- **型の整合**: `Mode`、`VrSettingsFile`、`BackupStore`、`ISteamVrProcess`、`ModeSwitcher` の名前と引数は Task 1〜5 の Interfaces で統一した。
- **Review Focus の対応**: 1 → Task 1、2 → Task 1 と 5、3 → Task 5、4 → Task 5、5 → Task 5。
- **未決の点**: `closeTimeout` の既定値は実装者が決めてよい(目安 15〜30 秒)。項目5は購入後に別途確認する。
