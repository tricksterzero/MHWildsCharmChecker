# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## プロジェクト概要

モンスターハンターワイルズの護石（スキル構成・スロット情報）をスクリーンショットから読み取ってCSV化し、重複・下位互換護石を検出するツール。現在はClaude Vision + Node.jsで読み取り・チェックを行っているが、C# (WPF) + Windows.Media.Ocr + OpenCvSharpによるローカル動作のWindowsアプリへ移行中（`app/`が移行先の置き場所、現状は空）。

## フォルダ構成

- `app/` — 移行先のC# WPFアプリ本体（未着手）
- `legacy/` — 旧アプローチ一式。C#移植時のロジック・仕様の参照元
  - `charm-duplicate-checker.js` — 重複・上位互換チェッカー
  - `charm_reader_prompt.md` / `charm_reader_prompt_for_cowork.md` — スクショ読み取り手順（Vision向けプロンプト）
- `resources/skill-decoration-map.json` — 装飾品マスターデータ。スキル名正規化（117種）の正解候補として使う
- `charm-lists/` — 護石読み取り結果CSV（出力物。ローカルのみ、`.gitignore`対象）
- `assets/` — OCR/CV検証用スクリーンショット（ローカルのみ、`.gitignore`対象）

## コマンド

```
node legacy/charm-duplicate-checker.js <CSVパス>
```

CSVを読み込み、完全同一の重複と完全上位互換による処分候補を標準出力にレポートする（原本CSVは変更しない）。

## CSVフォーマット（12列、1行=護石1個）

```
スキル1名,Lv,スキル2名,Lv,スキル3名,Lv,防具スロット1,防具スロット2,防具スロット3,武器スロット1,武器スロット2,武器スロット3
```

- スキルが3つ未満の場合、空きは名前を空欄・Lvを`0`とする
- スロットは各位置のレベルを`0/1/2/3`で記録する（穴なし=0）
- スキル名は`resources/skill-decoration-map.json`の`decorations[].skills`キーに含まれる正規名のみが有効（正規化ルールは`legacy/charm_reader_prompt.md`を参照）

## 護石の優劣判定ロジック（`legacy/charm-duplicate-checker.js`）

- **完全同一**: スキル構成（名前+Lv）とスロット構成（防具・武器それぞれ降順ソート後）が両方一致
- **完全上位互換**: スキルは全項目でA≥B、スロットも防具・武器それぞれ降順ソート後の各位置でA≥B、かつ全項目で完全同一ではない
- スキル名の集合が異なる護石同士は比較不能（incomparable）として扱う

## C#移行の方針

- スタック: C#/.NET (WPF) + Windows.Media.Ocr（テキスト: スキル名・Lv）+ OpenCvSharp（スロットアイコンの判定）
- スロット判定: ソケット枠を`findContours`で検出し、相対オフセットでバッジ領域(種別)・三角領域(レベル)を切り出してテンプレートマッチング
- 基準解像度2560x1440に対する比率ベースで座標を扱う（解像度非依存対応は将来課題、現状は自環境での動作を優先）
- 当初は個人利用。機能が揃った段階で公開予定（このリポジトリのpush含む）
