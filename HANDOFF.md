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

## 2026-11-03 追加実装 + GitHub公開
- フォント: `TonbiFont.Get()` をOS実フォント名照合+try/catch+Unity標準フォントへのフォールバックに全面書き換え(「Unable to load font face」の連鎖エラー解消)
- 富士山: `TonbiWorld.BuildScenery()` の裾野を拡張(本物の比率に近づけ、山頂をやや平らに)。初版は空や霧の色に近く溶け込んでいたため、深い藍色の山体+暖色の岩肌+白い雪冠へコントラストを強化(z=900のフォグ減衰を見込んだ配色)
- 食べ物を盗まれた時の周囲の人のリアクション/コメント: `HumanAI.ReactToNearbyTheft()`。半径24m以内の人がコメント(食べ物名入りパターン含む)
- 性格6種(`Personality` enum: Angry/Kind/Cute/Noble/Humble/Dazai)をランダム付与。コメントが性格で変わる。Dazai枠は著名な引用のまま使わず、この場面用に新規で書いたオリジナル台詞
- ライバル鳥(`RivalBird.cs`、カラス): プレイ中15〜30秒間隔で出現、最寄りの食べ物に55%の確率で横取り(`GameManager.RivalSteal`)
- 操作系: `TonbiInput.cs` のDiveRect/FlapRectを固定ピクセルから「画面の短辺(S=min(width,height))基準」の相対サイズ・右下の親指の届く位置へ変更(縦/横どちらでも破綻しない設計)。画面向きは縦(Portrait)のまま
- GitHub公開: ソース→ https://github.com/titan11111/unity-day107-tonbi-kyukouka (main)、WebGLビルド→ gh-pages ブランチ → https://titan11111.github.io/unity-day107-tonbi-kyukouka/ (公開バイト列とローカルビルドのハッシュ一致を確認済み。実機スマホでの動作確認はまだ)
