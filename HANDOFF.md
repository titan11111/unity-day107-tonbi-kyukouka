# HANDOFF day107
- Unity 6000.3.26f1 / Built-in RP / ランタイム生成 (TonbiWorld.Build)
- マイルストーン1完了。コンパイル検証済み(error CS 0件、シェーダエラー0件)。次: ユーザーの実機/Playテスト → マイルストーン2
- 構成: Assets/Scripts(*.cs), Assets/Resources/TonbiShaders(5 shaders), Assets/Scenes/Main.unity(空シーン、EditorBuildSettings登録済み)

## 2026-11-03 コンパイル修正・セットアップ記録
- バッチモード検証: `/Volumes/開発用SSD/開発データ/Unity/Editors/6000.3.26f1/Unity.app/Contents/MacOS/Unity -batchmode -nographics -quit -projectPath "$(pwd)" -logFile build.log`
  (Hub標準パス `/Applications/Unity/Hub/Editor/...` には存在せず、Editor本体は上記SSD配下にインストールされている)
- 修正: `Assets/Scripts/TonbiMusic.cs` の `HandleSfx()` 内、`SpawnBlip(f, f2, amp, tau, wave, delaySec)` 呼び出しで第4引数 `tau`(float)に `double` リテラル(0.22, 0.3, 0.4, 0.5, 0.07, 0.6, 0.9, 0.16, 0.2)を渡していたため `error CS1503` が57件発生。全箇所に `f` サフィックスを付与して解消(line 156-162)
- シーン: `Assets/Scenes/Main.unity` を空シーンで新規作成し、EditorBuildSettings.scenes に登録。PlayerSettingsを縦画面(Portrait)固定(allowedAutorotateToPortrait=1、他方向=0)に設定。作成用の一時Editorスクリプト(Assets/Editor/TitanSetupDay107.cs)は実行後に削除済み(ビルドに残存しない)
- 最終確認: 修正後に再度バッチモードコンパイル→ error CS 0件、シェーダエラー0件を確認。Unityエディタ(GUI)を起動済み
