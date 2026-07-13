# AutomationToolkit

ゲームやWeb上の単純な繰り返し作業を自動化する、Windows 用のマクロ録画・再生ツールです。キー入力やマウス操作を録画し、グローバルホットキーから再生できます。

## 特徴

- **録画**: キーボード・マウス (クリック / ホイール / 移動) をグローバルに記録。マウス移動は間引いて記録するためファイルが軽く、ドラッグも再現できます。
- **再生**: グローバルホットキーやGUIから再生。速度倍率とループ回数を指定可能。再生タイミングは高精度 (絶対時刻基準、~1ms) で再現します。
- **調整**: マクロは人間が読める JSON で保存されるため、手編集でスリープ時間・座標・クリック回数を調整できます (専用の編集GUIは今後追加予定)。
- **将来拡張**: 画像認識ステップ (「このアイコンを探してクリック」) や編集GUIを見込んだデータモデル設計です。

## プロジェクト構成

| プロジェクト | 役割 |
|---|---|
| `AutomationToolkit.Core` | UI非依存のエンジン (モデル・永続化・録画・再生・フック)。標準ライブラリのみに依存。 |
| `AutomationToolkit.App` | WPF GUI アプリ。 |
| `AutomationToolkit.Core.Tests` | xUnit 単体テスト。 |

## 動作環境・ビルド

- .NET 10 SDK (Windows)
- ビルド: `dotnet build`
- テスト: `dotnet test`
- GUI 起動: `dotnet run --project src/AutomationToolkit.App`

## 使い方

1. アプリを起動すると、マクロ一覧が表示されます (保存先: `%APPDATA%\AutomationToolkit\macros`)。
2. **● 録画** ボタン、または **Ctrl+Alt+R** で録画開始。もう一度押すと停止し、名前を付けて保存します。
3. 一覧でマクロを選び **▶ 再生**。再生中は **Ctrl+Alt+S** または **■ 再生停止** で中断できます。
4. 「ホットキー」欄をクリックすると、そのマクロに再生用ホットキーを割り当てられます。
5. 「速度」「ループ」はセルをダブルクリックで編集できます。

## マクロファイル形式

`schemaVersion` / `name` / `steps` を持つ JSON です。各ステップは `$type` で種類を、`delayBeforeMs` で「そのステップ実行前の待機時間」を表します。

```json
{
  "schemaVersion": 1,
  "name": "sample",
  "steps": [
    { "$type": "mouseMove", "delayBeforeMs": 120, "x": 960, "y": 540 },
    { "$type": "mouseDown", "delayBeforeMs": 35, "button": "Left", "x": 960, "y": 540 },
    { "$type": "mouseUp",   "delayBeforeMs": 90, "button": "Left", "x": 960, "y": 540 },
    { "$type": "keyDown",   "delayBeforeMs": 300, "virtualKey": 65, "scanCode": 30 },
    { "$type": "keyUp",     "delayBeforeMs": 60,  "virtualKey": 65, "scanCode": 30 }
  ]
}
```

ステップ種類: `keyDown` / `keyUp` / `mouseDown` / `mouseUp` / `mouseMove` / `mouseWheel`。
座標は仮想スクリーンの物理ピクセルです。スリープ調整は該当ステップの `delayBeforeMs` を変更するだけで済みます。

## 既知の制限・注意事項

- **管理者権限**: 対象アプリが管理者権限で動作している場合、入力が届きません。その場合は本ツールも管理者として実行してください。
- **アンチチート**: カーネル型アンチチート (Vanguard 等) を持つゲームは、合成入力を弾くことがあります。本ツールはスキャンコード送出で DirectInput 系の多くをカバーしますが、保護されたゲームには対応できません。
- **DPI / マルチモニタ**: PerMonitorV2 DPI 対応済みです。DPI が混在する複数モニタ環境で座標がずれる場合は、録画・再生を同じモニタ構成で行ってください。
- **キーボードレイアウト**: 記号キーのスキャンコードは JIS / US 配列で異なります。録画と再生は同じ配列で行うことを推奨します。
