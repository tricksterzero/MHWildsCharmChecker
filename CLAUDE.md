# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## プロジェクト概要

モンスターハンターワイルズの護石（スキル構成・スロット情報）をスクリーンショットから読み取ってCSV化し、重複・下位互換護石を検出するWindowsデスクトップアプリ。C# (WPF) + Windows.Media.Ocr + OpenCvSharpでローカル動作する。Core機能・UI共に実装済み、ライセンス対応済み（MIT + THIRD-PARTY-NOTICES）、README作成済み。公開に向けた必須の残タスクは配布形式の検討（別途、未確認スキルの調査等の任意タスクあり、詳細は「RARE推定ロジック」節のTODO参照）。

## フォルダ構成

- `app/` — C# WPFアプリ本体（CharmChecker.App / CharmChecker.Core / CharmChecker.Tests）
- `legacy/` — 旧アプローチ一式（Node.js + Claude Vision）。C#移植時のロジック・仕様の参照元として保持
  - `charm-duplicate-checker.js` — 重複・上位互換チェッカー（C#移植済み）
  - `charm_reader_prompt.md` / `charm_reader_prompt_for_cowork.md` — スクショ読み取り手順（Vision向けプロンプト）
- `resources/` — アプリが参照するデータファイル群
  - `skill-decoration-map.json` — 装飾品マスターデータ + 装飾品なしスキル。スキル名正規化（121種）の正解候補として使う
  - `skill-order.json` — ゲーム内表示順のスキル名リスト。ComboBox表示順に使用
  - `skill-groups.json` — スキル→グループ番号のマッピング。RARE推定に使用
  - `charm-combinations.json` — グループ組み合わせ→RARE値のパターンテーブル。RARE推定に使用
  - `charm-types.json` — 護石名→武器スロット有無のテーブル。スロット種別判定に使用
  - `skill-name-checklist.md` — ゲーム内スキル一覧との突き合わせチェックリスト
  - `deco_checklist.txt` — ゲーム内装飾品一覧との突き合わせチェックリスト（361件照合済み）
- `charm-lists/` — 護石読み取り結果CSV（出力物。ローカルのみ、`.gitignore`対象）
- `assets/` — OCR/CV検証用スクリーンショット（ローカルのみ、`.gitignore`対象）
- `LICENSE` — MIT License
- `THIRD-PARTY-NOTICES` — 依存ライブラリのライセンス表示（OpenCvSharp4/OpenCV/WPF-UI）

## コマンド

### C#アプリ（メイン）

```
cd app
dotnet build
dotnet run --project CharmChecker.App
dotnet test --project CharmChecker.Tests
```

### legacy（参考用）

```
node legacy/charm-duplicate-checker.js <CSVパス>
```

## CSVフォーマット（12列、1行=護石1個）

```
スキル1名,Lv,スキル2名,Lv,スキル3名,Lv,防具スロット1,防具スロット2,防具スロット3,武器スロット1,武器スロット2,武器スロット3
```

- スキルが3つ未満の場合、空きは名前を空欄・Lvを`0`とする
- スロットは各位置のレベルを`0/1/2/3`で記録する（穴なし=0）
- スキル名は`resources/skill-decoration-map.json`の`decorations[].skills`キー + `extra_skills`配列に含まれる正規名（計121種）のみが有効（正規化ルールは`legacy/charm_reader_prompt.md`を参照）
- mhwilds.wiki-db.comのスキルシミュレータとのインポート・エクスポート互換を意図した形式

## スキル名の正規名と照合時の注意

- **正規名の単一ソース**: `resources/skill-decoration-map.json`の`decorations[].skills`キー + `extra_skills`（計121種）
- `skill-groups.json`・`skill-order.json`等のスキル名は正規名と完全一致している必要がある
- **正規名は全角表記に統一**: `ＫＯ術`（全角）・`攻撃力ＵＰ`（全角）等を使う。OCR入力側の半角ASCII（`KO術`・`攻撃力UP`等）は`SkillNameNormalizer.ToFullWidthAscii`で照合前に全角へ正規化するため問題なくマッチする（`app/CharmChecker.Core/Skill/SkillNameNormalizer.cs`）
- スキル名を含むリソースファイルを編集する際は、必ず`skill-decoration-map.json`の表記と突き合わせること

## 護石の優劣判定ロジック（`CharmChecker.Core/Model/DuplicateChecker.cs`）

- **完全同一**: スキル構成（名前+Lv）とスロット構成（防具・武器それぞれ降順ソート後）が両方一致
- **完全上位互換**: スキルは全項目でA>=B、スロットも防具・武器それぞれ降順ソート後の各位置でA>=B、かつ全項目で完全同一ではない
- スキル名の集合が異なる護石同士は比較不能（incomparable）として扱う

## RARE推定ロジック（`CharmChecker.Core/Model/RarityInference.cs`）

- `skill-groups.json`でスキル名→グループ番号に変換し、`charm-combinations.json`のパターンテーブルからRARE値を決定
- 特殊ケース: 武器スロットが1つでもあれば → RARE 8 固定（栄世の護石限定の武器スロットはRARE8確定のため）
- 特殊ケース: 研鑽スキル（希望の護石固有）→ RARE 5 固定
- 特殊ケース: 武器スロットなしでグループパターンがRARE 7・8の両方に一致する場合はRARE 8候補を除外し、RARE 7のみが残ればRARE 7とする（現行テーブルではRARE 8のスキルグループ構成がRARE 7と重複するが、RARE 8の護石は必ず武器スロットを伴うため。「7/8なら常に7」ではなく「8を除外した後に7だけが残れば」という条件）
- **TODO**: `skill-decoration-map.json`にあって`skill-groups.json`に無い8スキル（オトモへの采配・クライマー・ジャンプ鉄人・ハンター生活・昆虫標本の達人・緩衝・閃光強化・飛び込み）は、実際に護石に付与されうるスキルか未確認（ユーザー確認待ち）。該当スキルを含む護石は、武器スロットまたは研鑽スキルによる先行判定に該当しない限りRARE推定が`null`になる。正規名121種すべてがRARE推定可能なわけではない点に注意

## WPF UI構成

### ウィンドウ構成（全4ウィンドウ、FluentWindow + Mica backdrop）
- `MainWindow` — メインウィンドウ（3タブ: 護石一覧 / スクショ読み取り / CSVインポート・エクスポート）
- `DuplicateCheckWindow` — 重複チェック結果ダイアログ（護石一覧タブのボタンから起動）
- `CharmEditWindow` — 護石編集・手動入力ダイアログ
- `SettingsWindow` — 設定ダイアログ

### フォント
- ウィンドウ既定: BIZ UDGothic 16px
- DataGrid: 14px（明示設定）
- TitleBar: 16px（明示設定）

### wpfui TitleBar・DataGridのフォント非継承
**wpfui TitleBarはウィンドウのFontFamily/FontSizeを継承しない。** DataGridも同様。新しいウィンドウを追加する際は、TitleBarとDataGridに`FontFamily="BIZ UDGothic" FontSize="..."`を明示的に設定すること。設定を忘れるとシステムフォント（Yu Gothic UI等）にフォールバックし、見た目が不統一になる。

### テーマ
- ダーク固定。wpfui標準のDynamicResourceはダークモードで視認性が極端に低いため、カスタムカラーリソース（PanelBackground/CardBackground/SplitterColor/SecondaryText）で明示的に上書きしている
- ライトテーマ対応は将来タスク（カスタムカラーのDynamicResource化が必要）

## 技術構成・実装方針

- スタック: C#/.NET 10.0 (WPF) + Windows.Media.Ocr（テキスト: スキル名・Lv）+ OpenCvSharp4（スロットアイコンの判定）+ WPF-UI 4.3.0（Fluentテーマ）
- スロット判定: ソケット枠を`findContours`で検出し、列プロファイル解析でレベル判定。種別は護石名ベースで判定（バッジテンプレートマッチングは不安定なため廃止）
- 基準解像度2560x1440に対する比率ベースで座標を扱う（解像度非依存対応は将来課題、現状は自環境での動作を優先）
- 公開準備中。ライセンス対応済み・README作成済み。公開に向けた必須の残タスクは配布形式の検討

## スロットアイコン判定ロジック（`CharmChecker.Core/SlotIcon/`）

PythonでPoC済み、C#移植済み。

- **基準解像度**: `REF_W=2560, REF_H=1440`。実画像サイズとの比率(`scale_factors`)で全座標をスケーリング。
- **パネル領域**: BOXパネル(2護石比較画面の右側, y:280-420, x:2200-2500)とDetailパネル(単一護石詳細画面, y:310-410, x:1400-1650)の2領域で検出し、フレーム数が多い方を採用。
- **ソケット枠検出**: `Canny`→`findContours`→`boundingRect`。`FRAME_W_RANGE`/`FRAME_H_RANGE`/`FRAME_Y_MIN`（基準解像度でのpx範囲）でサイズ・位置フィルタ → `MergeOverlapping`(x近接統合, 閾値20px) → `FilterYCluster`(最頻yグループのみ残す, 閾値15px, 同数時は面積合計で選択)。
- **レベル判定(Lv1/2/3)**: 枠の下45%〜100%を(50,20)に正規化しOtsu二値化 → 列ごとの白画素数プロファイルを移動平均(win=3)で平滑化 → 全体最大値の0.75以上をピークとしてグルーピング → ピーク数(`n`)と隣接ピーク間の谷比率(`谷の最小値/全体最大値`)で判定:
  - `n==1` → Lv1
  - `n==2`かつ谷比率`<0.5` → Lv1
  - `n==2`かつ谷比率`>=0.5` → Lv2
  - `n>=3` → Lv3
- **種別判定(武器/防具)**: 護石名ベース。`SkillReadingPipeline.ReadWithMetadataAsync`で護石名を取得し、「栄世の護石」なら1つ目が武器スロット、それ以外は全て防具スロット。
- **バリデーション(`SlotValidation.cs`)**: 検出結果を業務ルールに基づいて検証・補正する。防具スロットは降順ソート後、1個目は最大Lv3・2個目は最大Lv1・3個目は常に0（=実質最大2個）、武器スロットは最大1個・Lv1のみを条件として適合する値のみ採用し、条件を満たさない検出値はゴミとして破棄する（丸め処理ではない）。

### 検証範囲・制約
- BOXパネル(2護石比較画面の右側): 11パネルでLv全問正解。Detailパネル(単一護石詳細画面): 2パネルで全問正解。装備中側(左パネル)は装飾品装着で色が変わるため判定対象外。
- Steamスクショ357枚での実画像テスト済み: 220枚でスロット検出、Unknown判定0件（護石画面のみ対象）。
- **武器Lv2/Lv3は現行ゲームに存在しない**ため未対応（将来アップデートで追加された場合は要再検証）。
- 「穴なし」スロットは対象外（ショップ購入護石のみに存在し、本チェッカーの対象護石には現れない）。
- 2560x1440以外の解像度・16:9以外のアスペクト比は未検証。

## テキストOCRロジック（`CharmChecker.Core/Ocr/TextOcrReader.cs`）

`Windows.Media.Ocr`を使ったテキスト認識。

- **OCRエンジン生成**: `OcrEngine.TryCreateFromLanguage(new Language("ja"))`。`null`の場合は例外（日本語OCR言語パック未導入）。
- **画像読み込み**: `BitmapDecoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied)`でデコード時に直接変換する。デコード後に`SoftwareBitmap.Convert`で変換する方式は、JPEGのアルファ値の扱いにより画像が壊れる可能性があるため避ける。
- **CJK文字の認識仕様**: 漢字・かな等のCJK文字は1文字ずつ別の`Word`として認識され、`OcrLine.Text`/`OcrResult.Text`は文字間に半角スペースを挟んで結合される（例: `"栄世の護石"` → `"栄 世 の 護 石"`）。スキル名等と比較する際はスペースを除去してから行う。
- **TFM要件**: `Windows.Media.Ocr`の利用には`CharmChecker.Core`/`CharmChecker.Tests`/`CharmChecker.App`すべてを`net10.0-windows10.0.22000.0`に統一する必要がある（プロジェクト間でTFMの具体度が揃わないとNU1201エラーになる）。

## スキル名・Lv読み取りロジック（`CharmChecker.Core/Skill/`）

PythonでPoC済み、C#移植済み。15画像・37スキル項目で全問正解。

### パイプライン概要
1. **フル画像OCR** → アンカー検出 → 護石パネル判定 → スキル領域クロップ → バリエーションOCR → 正規化・ペアリング

### アンカー検出（3段フォールバック、最右優先）
複数パネルがある場合は最も右(=BOX側)を採用:
1. 「装備詳細」完全一致 → word単位で「装」のx,y座標
2. 「備詳細」部分一致 → OCRが先頭文字を誤認するケース対応
3. 「装備スキル」位置から逆算 → `(x-20, y-310)` をアンカーとする

### 護石パネル判定
アンカー相対位置(dx:-50〜+250, dy:+80〜+280)に「護石」テキストがあれば護石と判定。武器・防具の装備詳細画面を棄却する。

### スキル領域クロップ
- `SKILL_AREA_REL = {x0:0, x1:470, y0:310, y1:700}` (アンカーからの相対px、2560x1440基準)

### 前処理バリエーション（5種）
各バリエーションで独立にOCRし、名前・Lvそれぞれ最多検出のバリエーションから採用:
- raw(原画, lv_x_thresh=300)
- trim60(左60pxカット, lv_x_thresh=240)
- otsu(グレースケール→Otsu二値化, lv_x_thresh=300)
- trim60_otsu(トリミング+二値化, lv_x_thresh=240)
- gray(グレースケールのみ, lv_x_thresh=300)

### スキル名正規化
- `skill-decoration-map.json`の`decorations[].skills`(117種) + `extra_skills`(4種)の計121種を正解候補とする
- 長い名前から優先的に部分一致（先頭のゴミ文字を無視）
- **濁点フォールバック**: 通常マッチ失敗時に濁点を除去して再マッチ（OCRの「ガ→カ」等の誤認対応）

### Lv解析
`parse_lv()`: 先頭を"LV"に正規化し残りから数字を抽出。文字置換: し→L, l→L, I→1, ー除去。

### ペアリング
- クロップ内テキストを`lv_x_threshold`で左右分離（左=スキル名候補、右=Lv候補）
- 各リストをy座標でソートし、インデックス順にペアリング

### 検証範囲・制約
- 2560x1440(16:9)の5画面パターン・15画像で全問正解(37/37)
- 非護石画像の棄却テスト: 30枚で偽陽性ゼロ
- 1280x720設定(スクショは2560x1440): 正常動作確認済み
- **21:9は未対応**(パネル幅が異なりLv検出不可。将来課題)

## README運用方針

README.mdは以下の方針で作成済み（2026-07-19）。今後README.mdを更新する際もこの方針に従う。

- **言語**: 日本語。冒頭に英語で「日本語版MHWilds向けツール」の一文を添える
- **対象読者**: ユーザー向け。技術詳細はCLAUDE.mdに任せる
- **スクリーンショット**: 初版では省略。後から追加するのは任意の改善事項（配布形式検討とは独立、必須の残タスクではない）
- **構成**（実装時に9項目案から一部調整。概要は独立見出しにせず冒頭の説明文に統合、配布状況の節を追加）:
  1. 英語の一文（日本語版向けである旨）+ 概要（見出しなしの説明文）
  2. 動作環境・前提条件（利用者向け/開発者向けに分離。データ保存場所も明記）
  3. 現在の配布状況（配布バイナリ未整備・ソースビルドが必要な旨。「インストール」ではなく「使い方」とする）
  4. 使い方
  5. 既知の制約
  6. CSV形式互換の明記（mhwilds.wiki-db.comスキルシミュレータとの相互運用を意図。完全互換は保証しない）
  7. ビルド方法
  8. 免責・帰属表示（カプコン知的財産への帰属、非公式ツールである旨）
  9. ライセンス（MIT、THIRD-PARTY-NOTICESへの参照）

## コミット規約

- Conventional Commits の prefix（`feat`/`fix`/`docs`/`chore`等）＋簡潔な1行件名
- コミットメッセージは日本語
- 説明本文の段落は付けない
