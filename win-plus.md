# win-plus 保守メモ (開発者向け・非公開情報)

このファイルは common rules rule 6 の保守用ドキュメントである。
公開サイト・READMEには出さない内容をここに集約する。

## バージョン上げチェックリスト

1. `Package.appxmanifest` の `Version` (例: 1.0.0.0)
2. `Services/AppState.cs` の `AppVersion`
3. `Strings/*/Resources.resw` の `General_Version`
4. サイト `website/projects/win-plus/index.html` の版数・ダウンロードリンク
5. `website/content.js` のリリース情報
6. dist ファイル名・GitHub Release タグ (例: v1.0.0)

## ビルド規律

- packaged ビルドは必ずフレームワーク依存 (`SelfContained=false` が既定)。
  `--self-contained` 付き MSIX は起動時 fail-fast (CoreMessagingXP 0xc0000602) になる。
- `dotnet publish -p:SelfContained=true` は `bin/obj` を汚染するため、
  その後の packaged ビルド前には必ず `bin/obj` を削除してクリーンビルドする。
- x86/x64 の同版 MSIX は同居不可 (0x80073CF3)。入替時は旧archを先に削除。
- x86 動作には x86 版 .NET Desktop Runtime + WindowsAppRuntime が別途必要。

## 署名

- 配布物は開発用自己署名 (`CN=yuki-orita`, `devcert.pfx`)。
- `devcert.pfx` はリポジトリに入れない (ローカルのみ)。
- 製品配布時は CA 発行証明書 / Trusted Signing へ差し替え、
  `win-plus-devcert.cer` と README の信頼手順を更新する。

## 既知の未検証・注意

- 指本数測定: 標準 PTP 機での実タッチ未検証 (開発機に PTP Digitizer が無い)。
  Apple SPI 等の非PTPでは「測定不可」表示が正規動作。
- Win+L 送出: 未送出テスト (セッションをロックするため)。
- PrintScreen は OS 登録が発火しないためフック固定 (`AppState.AddTrigger`)。
- 次/前ページ プリセットは PageDown/PageUp 送出と定義。
- resw 追加時は `en` を正として7言語 parity を取ること
  (キー名の bare/scope 二重定義は MRT PRI175 エラーになる)。
- `MessageWindow` の GetMessage ループでは `DispatchMessage` が必須
  (WM_HOTKEY / WM_INPUT / WM_COMMAND が届かなくなる)。

## サイト更新手順

- `oriyu90/studio-rizi` の `website/projects/win-plus/` を編集し、
  `projects/manifest.json` + `website/content.js` を更新。
- `npm run build` (= generate:seo + validate + count-files) と `npm test` を通す。
- main へ push (Cloudflare Pages が自動デプロイ)。

## v1.1.0 作業メモ (2026-09-29)

- QR読み取り: `Services/QrCodeService.cs` 新設 (+ `ZXing.Net 0.16.11` 依存)。
  `ShotPreviewWindow` が撮影後に裏で復号→画像下に http/https リンクのみ一覧
  (最大10件・`TrySanitizeUrl` で scheme/長さ/制御文字を検証、タップ時のみ既定ブラウザ起動)。
  新規 resw キーは `Qr_Title` / `Qr_BlockedNote` の2件×7言語。
- レイアウト: 設定6ページの `ScrollViewer` は水平スクロール禁止、
  ボタン行は `VariableSizedWrapGrid` で折り返し、既定窓は 1000x720。
- 二重実行の修正: `App.xaml.cs` の `Actions.*Requested` 直接購読を削除
  (AppState の転送経由に一本化。撮影・メモが2回走っていた)。
- 開発機注意: `HKLM\SOFTWARE\dotnet\Setup\InstalledVersions\*\sharedfx` が欠落すると
  packaged/dev の framework-dependent 起動が 0x8007007B/1023 で失敗する。
  `winapp run` は独自にランタイム解決するため影響なし。
  ポータブル (self-contained) は影響なし。
