# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## プロジェクト概要

モンスターハンターワイルズの護石（スキル構成・スロット情報）をスクリーンショットから読み取ってCSV化し、重複・下位互換護石を検出するWindowsデスクトップアプリ。C# (WPF) + Windows.Media.Ocr + OpenCvSharpでローカル動作する。Core機能・UI共に実装済み、ライセンス対応済み（MIT + THIRD-PARTY-NOTICES）、README作成済み。配布形式は自己完結（self-contained）のWindows x64向けポータブルzip・GitHub Releases配布（2026-07-21、v1.0.0リリース済み）。リポジトリは公開設定済み（Public、2026-07-21）。公開作業は完了。

## フォルダ構成

- `app/` — C# WPFアプリ本体（CharmChecker.App / CharmChecker.Core / CharmChecker.Tests）
- `legacy/` — 旧アプローチ一式（Node.js + Claude Vision）。C#移植時のロジック・仕様の参照元として保持
  - `charm-duplicate-checker.js` — 重複・上位互換チェッカー（C#移植済み）
  - `charm_reader_prompt.md` / `charm_reader_prompt_for_cowork.md` — スクショ読み取り手順（Vision向けプロンプト）
- `resources/` — アプリが参照するデータファイル群
  - `skill-decoration-map.json` — 装飾品マスターデータ + 装飾品なしスキル。スキル名正規化（120種）の正解候補として使う
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
- スキル名は`resources/skill-decoration-map.json`の`decorations[].skills`キー + `extra_skills`配列に含まれる正規名（計120種）のみが有効（正規化ルールは`legacy/charm_reader_prompt.md`を参照）
- mhwilds.wiki-db.comのスキルシミュレータとのインポート・エクスポート互換を意図した形式

## スキル名の正規名と照合時の注意

- **正規名の単一ソース**: `resources/skill-decoration-map.json`の`decorations[].skills`キー + `extra_skills`（計120種）
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
- 解決済み（2026-07-20）: `skill-decoration-map.json`にあって`skill-groups.json`に無かった8スキルのうち7種（クライマー・ジャンプ鉄人・ハンター生活・昆虫標本の達人・緩衝・閃光強化・飛び込み）は、実機の護石選択画面「条件ソート→装備スキル」一覧に出現せず装飾品専用スキルと確認。護石CSVにこれらの名前が入ることはなく、`skill-groups.json`に未収録でも実害なし。残る1種（オトモへの采配）は防具スキルではあるが護石・装飾品どちらの一覧にも出現せず、防具自体にのみ付くスキルと判明したため`extra_skills`から削除し正規名を121種→120種に修正済み（詳細: `resources/skill-name-checklist.md`）

## 護石構成確率の推定ロジック（`CharmChecker.Core/Model/CharmProbabilityEstimator.cs`）

護石一覧タブの詳細パネルに「構成確率（参考値）」として表示する、そのレアリティ内でのスキル・スロット構成の推定確率（2026-07-21実装）。

- 計算式: `構成確率 = 組み合わせ確率 × スロット確率 × 各スキルの選択確率の積`
  - 組み合わせ確率 = 1 / (そのレアリティで`charm-combinations.json`に存在する組み合わせパターン数)
  - スロット確率 = 1 / (その組み合わせで有効なスロットパターン数)。ただしRARE8のみ`charm-combinations.json`の`weight`フィールド（0.5/0.33/0.17）を使用
  - 各スキルの選択確率 = 1 / (そのスキルが属するグループに`skill-groups.json`で登録されている(スキル名,Lv)エントリ数)
- `RarityInference`が持つグループ照合ロジック（`ResolvePossibleGroups`/`MatchesGroupPattern`、いずれも`internal`化して共有）を再利用してcharmの組み合わせを`charm-combinations.json`から特定する
- 出典・確度: `charm-combinations.json`のデータ自体は既存のDtlnorデータマイン（コミュニティ有志による解析、RARE推定にも使用）が土台。RARE8のスロット重み（0.5/0.33/0.17）も同データマイン由来で、game8.jpの実測記事（レアリティ自体の抽選確率は確認できたが、スロットパターン内訳は未掲載と確認済み、2026-07-21調査）による裏付けはない
- **組み合わせ確率・RARE5〜7のスロット確率・各スキルの選択確率は、いずれも「等確率で選ばれる」という未検証の仮定に基づく推定値**であり、実際のゲーム内抽選確率を保証するものではない（ユーザー了承の上で実装、2026-07-21）
- UI表示（`MainWindow.xaml.cs`の`FormatProbability`）: パーセント表示・有効数字3桁（値の範囲が0.00004%〜0.02%程度と広いため、固定小数点桁数では小さい値が0%に飽和してしまうことを避ける設計）

## 理論値護石の判定ロジック（`CharmChecker.Core/Model/CharmTheoreticalValueChecker.cs`）

護石一覧タブの詳細パネルに「理論値判定」として表示する、次の2条件を両方満たすかの判定（2026-07-21実装）。

- **スキル条件**: 各スキルのLvが、そのスキル自身の最大Lv（`skill-groups.json`で同名スキルが取りうる全レベルの最大値。**所属グループの最大Lvではない**）と一致していること
  - 誤りやすい設計: 「そのスキルが属するグループの最大Lv」を基準にすると誤判定になる。例: 超会心はLv1にしか存在しないが、所属するgroup4にはLv2のスキルも混在するため、グループ最大基準だと超会心Lv1が理論値から誤って除外される。同様の食い違いが59件確認された（貫通弾・竜の矢強化、耳栓、毒耐性等）ため、スキル名ベースの自己最大Lvで判定する
- **スロット条件**: 護石の防具・武器スロット構成が、**同じスキルグループ構成（`skillGroups`）を持つ組み合わせの間でのみ**比較して、他のどのスロットパターンにも支配されない（＝全項目で上回られない）こと
  - 誤りやすい設計: スロットの優劣を**レアリティ横断で無条件に**比較すると誤判定になる。防具スロットと武器スロットは種類が異なり単純な優劣がつかないため（例: 防具Lv2×1とRARE8の武器構成はどちらが上位とも言えない）、無関係な組み合わせ同士のスロットを比較してはならない
  - `charm-combinations.json`を調べると、同一`skillGroups`が複数レアリティにまたがるのはRARE7・RARE8の組み合わせのみ（8パターン全てが完全一致）。これはRARE8がRARE7と同じスキル構成に武器スロットを追加しただけの関係にあるためで、xlsx出典の注記にも明記されている（"R7 and R8 have the same possible skill configs"）
  - 具体例: RARE7とRARE8で共有される`skillGroups=[3,7]`の場合、スロット比較対象はRARE7の`[1,0][1,1][2,0]`とRARE8の`[W1,0][W1,1][W1,1,1]`の6パターンのみ。この中でパレート最適なのは`[2,0]`（防具Lv2×1、RARE7）と`[W1,1,1]`（防具Lv1×2+武器Lv1、RARE8）の2つ。RARE7の`[1,0]``[1,1]`はRARE8側に防具据え置き・武器スロット純増で上回られるため支配されるが、`[2,0]`はRARE8のどの構成にも防具・武器の両面で上回られないため、RARE7の護石でも理論値になりうる
  - RARE5・6は他レアリティと`skillGroups`を共有しないため、各々の組み合わせ内の4パターン（RARE5: `[1,1][2,0][2,1][3,0]`）・4パターン（RARE6: `[1,0][1,1][2,0][2,1]`）の中でのみ比較する
- 両条件を満たさないスキルが1つでもあればスキル条件で不成立、スロットが支配されていればスロット条件で不成立

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
- 公開済み（2026-07-21）。ライセンス対応済み・README作成済み・自己完結ポータブルzipをGitHub Releasesでv1.0.0として配布中、リポジトリはPublic

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
- `skill-decoration-map.json`の`decorations[].skills`(117種) + `extra_skills`(3種)の計120種を正解候補とする
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

README.mdは以下の方針で作成済み（2026-07-19作成、2026-07-20に利用者向け優先の構成へ改訂）。今後README.mdを更新する際もこの方針に従う。

- **言語**: 日本語。冒頭に英語で「日本語版MHWilds向けツール」の一文を添える
- **対象読者**: ユーザー向け。技術詳細はCLAUDE.mdに任せる
- **利用者向け情報を先、開発者向け情報は末尾にまとめる**（2026-07-20方針変更）: 動作環境・前提条件は利用者向けのみを本文中段に置き、ビルド関連（開発者向け動作環境・ビルド方法）は末尾の「開発者向け情報」節に集約する
- **スクリーンショット**: 初版では省略。後から追加するのは任意の改善事項（配布形式検討とは独立、必須の残タスクではない）
- **構成**（2026-07-20改訂版）:
  1. 英語の一文（日本語版向けである旨）+ 概要（見出しなしの説明文）
  2. 動作環境・前提条件（利用者向けのみ。データ保存場所も明記）
  3. 現在の配布状況（配布形式は決定済み・自己完結ポータブルzip・GitHub Releases配布予定である旨、リリース未作成の間はソースビルドが必要な旨）
  4. 使い方
  5. 既知の制約
  6. CSV形式互換の明記（mhwilds.wiki-db.comスキルシミュレータとの相互運用を意図。完全互換は保証しない）
  7. 免責・帰属表示（カプコン知的財産への帰属、非公式ツールである旨）
  8. ライセンス（MIT、THIRD-PARTY-NOTICESへの参照）
  9. 開発者向け情報（ビルド環境・ビルド方法。末尾に集約）

## コミット規約

- Conventional Commits の prefix（`feat`/`fix`/`docs`/`chore`等）＋簡潔な1行件名
- コミットメッセージは日本語
- 説明本文の段落は付けない
