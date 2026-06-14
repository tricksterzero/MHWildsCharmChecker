# MHWilds CharmChecker

モンスターハンターワイルズの護石（スキル構成・スロット情報）をスクリーンショットから読み取り、CSV化した上で重複・下位互換護石を検出するツール。

現在はClaude Vision + Node.jsによる読み取り・チェックを行っているが、今後はC# (WPF) + Windows.Media.Ocr + OpenCvSharpによるローカル動作のWindowsアプリへ移行予定。

## フォルダ構成

- `app/` — 今後作成するC# WPFアプリ本体
- `legacy/` — 旧アプローチ（Vision + Node.js）。スクショ読み取りプロンプトと、重複・上位互換チェッカー（`charm-duplicate-checker.js`）。C#移植時のロジック・仕様の参照元
- `resources/` — アプリが参照する正規化・検証用辞書（`skill-decoration-map.json`）
- `charm-lists/` — 護石読み取り結果のCSV（本ツールの出力物）。ローカルのみ
- `assets/` — OCR/CV検証用スクリーンショット。ローカルのみ
