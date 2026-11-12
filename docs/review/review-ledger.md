# コード精査台帳

実装モジュールを1本ずつ精査するための進行管理台帳（2026-07-12作成）。
[Ragmas 6v6動画解析ツールの台帳](../../../Ragmas/6v6-video-analysis/review-ledger.md)の方式を踏襲。
2026-08-03、[コード精査台帳テンプレート](../../../../Knowledge/Tech/ClaudeCode/code-review-ledger-template.md)に基づき、
台帳本体（本ファイル）と詳細アーカイブ（[review-log.md](review-log.md)）の2ファイル構成に再構成した
（テーブルの1セルが最大8000字超まで肥大化し読みづらくなっていたため）。

**本台帳と[review-log.md](review-log.md)の役割分担**: 台帳には各ファイルの「最新状態」（状態列＋相互作用チェック相手・
未解決の既知課題を1行）だけを残す。精査のたびの詳細（発見内容・修正過程・実測値・Codexとの相談内容等）は
review-log.mdの該当ファイル見出しへ記載する。精査を終えたら、台帳の状態列を更新すると同時に
review-log.mdへ詳細を追記する。

**運用方針**: 2026-06-25に全体コードレビュー+セキュリティレビューを実施し12件修正済みだが、
「どのファイルを見て問題なしだったか」は記録が残っていない。本台帳は同じ反省に立ち、
**精査の実施自体を記録する**（問題なしでも日付を残す）。そのため、2026-06-25の修正コミット
（`2b63369`/`b090f4f`/`b62c0fb`等）で実際に差分が入ったファイルのみ「済」とし、
残りは記録が無いため未扱いとする（再精査しても害はない安全側の倒し方）。

**初回一巡完了（2026-07-12）**: 全25項目（高優先13・中優先8・低優先2・ドキュメント2）の精査を
1セッションで完了。発見は計4件=実バグ2件（`skill-groups.json`のスキル名表記不一致3件による
RARE推定サイレント失敗・スクショフォルダ設定の保存漏れ）+ドキュメント不整合2件（README.mdの
陳腐化・CLAUDE.mdのRARE推定特殊ケース記載漏れ）。相互作用チェック列が今回も実バグ発見の
主な経路になった（単体では正しく見える箇所が、消費先のデータ・状態と食い違っていた）。

**第2巡完了（2026-07-19、Codex相談あり）**: 初回一巡はCodexの助言を挟まずに実施したため、
今回は全25項目をCodex(`mcp__codex__codex`、独立した別系統モデル)と1本ずつ相談しながら再精査。
実バグ8件を発見・修正（`ExtractCharmName`の護石名誤帰属・RARE未推定の連鎖2箇所・スロット値
バリデーション上限の誤り(0〜4→0〜3、README執筆中に発覚)・スキルLv不整合の未検証・重複スキル名
未検証・テーマのOS追従による現行仕様との矛盾・ウォーターマーク方式の再スキャン手段欠如）+
fail-fast検証の追加5件+ドキュメント記載漏れ・陳腐化6件（README.md全面改稿・CLAUDE.md更新）。
Codexとの相談を通じた総括（Codex自身の言葉）: 「個々のアルゴリズムより『モジュール間の契約』に
実害のある問題が集中していた。OCR/CV本体は実画像でよく検証され精度も高い一方、護石名とパネルの
帰属・RARE推定の呼び忘れ・差分読み込みの再試行など、オーケストレーション層で不具合が生じやすい
構造だった。データファイルは実質的にコードと同じビジネスロジックであり、fail-fast検証や
ファイル間整合性テストの効果が高かった」。全140テスト成功、コアロジックの作り直しは不要、
連携漏れとデータ境界の補強が中心の巡目だった。詳細は[review-log.md](review-log.md)の各見出しを参照。

**第3巡完了（2026-07-21、Codex相談あり）**: 2026-07-21中に既存機能へ加えた変更5件を同日中に
Codex相談ありで再精査。実バグ計8件発見・修正（うち重要度高2件、SlotIconAnalyzer.cs融合フレーム
回復のY方向重なりチェック漏れ・重複除去漏れ、SkillReadingPipeline.csのDPペアリング構造的欠陥、
SkillNameNormalizer.csのカナ誤認フォールバック優先順位不整合、MainWindow.xaml.csの確率表示丸め、
App.xaml.csのMutex所有権誤解放）。詳細は[review-log.md](review-log.md)の各見出しを参照。

**#12/#21の宿題解消（2026-07-20）**: `skill-decoration-map.json`にあって`skill-groups.json`に無い
8スキル問題は、ユーザーが実機の「条件ソート→装備スキル」一覧との突き合わせで解決済み
（7種は装飾品専用スキルと確認、対応不要。残る1種「オトモへの采配」は`extra_skills`から削除、
正規名121種→120種）。詳細は[review-log.md](review-log.md#skillnameloadercs)を参照。

**未反映の後続変更（2026-08-03時点の既知課題、2026-08-05解消）**: CLAUDE.mdには2026-07-24〜2026-07-27の間に
実装された機能（ウルトラワイド21:9対応・真の21:9モニタ対応・空スロットテンプレート照合による
装飾品検知の全面再設計・融合フレーム回復のフォールバック拡張等）が記載されているが、本台帳・
review-log.mdの精査履歴（Codex相談）はいずれもこれらの変更より前の時点で止まっていた
（SlotIconAnalyzer.cs・SkillReadingPipeline.cs・MainWindow.xaml.csの各行に「追記」として
未反映であることを明記済み）。

**第4巡完了（2026-08-05、Codex相談あり）**: 上記未反映分3ファイルをまとめて1つの意思決定として
Codexへ相談し敵対的検証を実施。自分の机上レビューで洗い出した5点の懸念に加え、Codex独自の
指摘（高2件・中3件）を得て、`charm-combinations.json`の一次データ確認等で確かめた上で実バグ
候補4件を確認、うち3件を修正: (1)スロット検出0件が「穴なし護石」として正常保存される実装漏れ
（新規`SlotDetectionFailedException`追加）、(2)護石名OCR失敗時に栄世の護石の武器スロットが
防具スロットとして誤保存される実装漏れ（新規発見）、(3)21:9(黒帯付き・真のネイティブ両方)で
「装備スキル」逆算アンカーの固定オフセットが座標スケールで補正されていない座標系バグ
（新規発見、テストカバレッジ未確認のまま修正）。**(2)(3)は内蔵advisorとの最終レビューで初版の
修正が不完全（(2)は`charm-types.json`未登録の正当な護石=希望の護石等まで誤って除外する過剰対応、
(3)は黒帯付き21:9のケースが未対応）と判明し、各1回設計を訂正**（詳細はreview-log.mdの各節）。
**(4)融合フレーム回復の呼び出し条件変更は、advisor指摘を受けた実データ検証(全78枚×BOX/DETAIL
156領域のblast-radius比較)で、既存の正しい護石読み取り結果を壊す回帰（BOX領域の「RARE」ラベル
偽陽性を誤って「回復」しBOX優先ロジックにより正しいDETAIL側結果を上書き）が判明したため撤回**
（コード変更なし、詳細はreview-log.mdのSlotIconAnalyzer.cs節）。全221テスト成功、既存機能への
回帰なし。詳細は各ファイルのreview-log.md該当節（2026-08-05追記）を参照。

**第5巡完了（2026-11-12、Codex相談あり）**: 2026-11-07〜08の読み取りの修正・入口の検査の作り直しと、2026-11-12の整理で
「未」「+変更（未精査）」になっていた39行を、変更の理由ごとに3つのグループに分けて精査した（グループごとに
Codexへ1〜2回相談）。グループA（護石データの入口の検査）で不具合3件（起動中の例外でウィンドウの無いプロセスが
多重起動防止のMutexを持ったまま残る・編集画面で何も変えずにOKを押すとRAREが消える場合がある・手動入力で名前とLvの
片方だけの欄が黙って捨てられる）、グループB（スクショの読み取り）で不具合2件（護石名の本体を読み違えると栄世の護石の
武器スロットが防具スロットとして保存されうる・全画像の再スキャンで取り込み済みの護石が二重に入る）を修正。
グループC（2026-11-12の整理）は0件。あわせて、コメント・文書とコードの食い違いを直した。詳細はreview-log.mdの
「2026-11-12 グループA/B/C」の各節を参照。

## 精査手順（1本あたり）

1. 実装+対応テストを読む
2. コード内コメント・CLAUDE.mdの「〜と同じ方針」「〜で判定」等の主張を実コードと突き合わせる
3. 「相互作用チェック相手」列のモジュールとの整合を確認する（単体では正しく見える部分適用漏れを狙う）
4. 疑わしい箇所は実データ（`assets/`のスクリーンショット・実CSV・`charm-lists/`）で確認する
5. 修正は回帰テスト付きで行い、本台帳の状態更新＋review-log.mdへの詳細追記までを1単位としてコミットする
6. 深い判断・セカンドオピニオンが必要な指摘は外部レビュー（Codex等）に相談してもよい

## 状態の凡例

- **未** — 未精査
- **済(日付)** — 精査済み。発見があれば括弧内に件数
- **済(日付)+変更** — 精査後にコードが変わっており、差分の再精査が必要

## 高優先（コアロジック・複雑な画像処理・ビジネスルール）

| ファイル | 状態 | 相互作用チェック相手 |
|---|---|---|
| SlotIconAnalyzer.cs | 済(2026-11-12、Codex相談(グループB・スロット側)、0件) | SlotIconTypes(定数)・SlotValidation(判定結果の検証)・SkillReadingPipeline(護石名ベース種別判定の受け渡し)・ScreenshotCharmReader.ReadSlots(2026-11-08にMainWindowから移動。BOX優先ロジックとの相互作用に要注意、撤回の経緯を参照)。既知課題: 融合フレーム回復の観察点2件+MergeOverlapping/FilterYCluster観察点3件+RangesOverlapのY非考慮1件+「filtered完全に空」ケースへの対応は安全に実現できず据え置き(いずれも実害小、詳細はreview-log.md)+観察点1件(2026-11-12、公開メソッドScaleFactorsは同じ値(コンテンツの高さ/1440)を横・縦の2つとして返す。倍率1つにするとDetectFramesと呼び出し元の書き換えが要り、得られるのは読み違いの防止だけのため据え置き)。詳細: [review-log.md](review-log.md#sloticonanalyzercs) |
| SlotIconTypes.cs | 済(2026-11-12、Codex相談(グループB・スロット側)、0件) | SlotIconAnalyzer(定数消費)・SlotValidation(ArmorSlotMaxByPositionとの整合)。既知課題なし。詳細: [review-log.md](review-log.md#sloticontypescs) |
| SlotValidation.cs | 済(2026-11-12、Codex相談(グループA)、コメントの修正1件) | SlotIconAnalyzer(判定結果)・ScreenshotCharmReader.AssignSlots(武器/防具振り分け後の呼び出し元)・CharmValidator(手動入力・CSV・JSONの検査。nullならError)。既知課題: 武器スロット非ソートは将来Lv2/Lv3許可時に要再設計(据え置き)。詳細: [review-log.md](review-log.md#slotvalidationcs) |
| ScreenshotCharmReader.cs | 済(2026-11-12、新規、Codex相談(グループB)、不具合1件修正(護石名がcharm-types.jsonに当てはまらない場合に武器スロットなしとして読み進めていた)。全978枚で結果の件数が変わらないことを確認) | ScreenshotImage(画像の読み込み)・SkillReadingPipeline・SlotIconAnalyzer・SlotPanelConsistency・SlotValidation・CharmTypeLoader・CharmCombinationValidator・MainWindow(一括読み取り)・ScreenshotReadingTally。既知課題: AssignSlotsがnullを返す経路はユニットテストがあるが、それを例外にしてInconsistentWithCombinationsとして扱う部分は通していない。スロットの無い希望の護石は、枠を検出できない(または別の模様を枠として拾う)ため正しく読めず除外される(2026-11-12、手元の1枚は装飾品ありとして除外。誤った値で保存されることはない)。ユーザー判断で対応しない（2026-11-12。配布でしか入手できない特殊な護石のため。手動入力で登録できる）。詳細: [review-log.md](review-log.md#screenshotcharmreadercs) |
| SkillReadingPipeline.cs | 済(2026-11-12、Codex相談(グループB・スキル側)、0件) | ImageVariantFactory・LvParser・SkillNameNormalizer・SkillNameLoader・TextOcrReader・SlotIconAnalyzer(護石名の受け渡し)・CharmTypeLoader。既知課題: 過去データ(2026-07-19以前読み取りのCSV)の護石名誤帰属可能性(ユーザー報告済み)、観察点3件(据え置き)。**「装備スキル」逆算アンカーのocrScale補正は修正済みだが、この経路を実際に通る実データでの検証は未確認**。観察点1件(2026-11-12、行のテキストの空白除去と単語の左端・上端の取り出しがExtractCharmName・FindLabelCandidates・IsCharmPanelとParseOcrLinesで重複。ParseOcrLinesにまとめると単語が0個の行が例外から読み飛ばしに変わりうる(行のテキストと単語の関係は未確認)うえ、FindLabelCandidatesは単語ごとの左端が要るため置き換えられず、据え置き)。詳細: [review-log.md](review-log.md#skillreadingpipelinecs) |
| ImageVariantFactory.cs | 済(2026-11-12、Codex相談(グループB・スキル側)、0件) | SkillReadingPipeline(10バリエーション生成(元の大きさ5種+1.5倍に拡大した5種)・幅ガード)。既知課題: Createメソッドの例外時ネイティブメモリリーク(観察点、据え置き)。詳細: [review-log.md](review-log.md#imagevariantfactorycs) |
| LvParser.cs | 済(2026-07-19、Codex相談、実バグ1件修正) | SkillReadingPipeline(RunVariantsAndMerge、右側候補のみ適用)。既知課題なし。詳細: [review-log.md](review-log.md#lvparsercs) |
| SkillNameNormalizer.cs | 済(2026-07-21、Codex相談、実バグ1件修正) | SkillNameLoader(正解候補120種)・SkillReadingPipeline。既知課題: 特殊・分解形式の濁点は現行データに該当なしのため見送り(観察点)。詳細: [review-log.md](review-log.md#skillnamenormalizercs) |
| SkillNameLoader.cs | 済(2026-11-12、Codex相談(グループC)、0件) | SkillNameNormalizer(正解候補供給元)・MainWindow(LoadFromEmbeddedResource)。既知課題なし(8スキル宿題は2026-07-20解消済み)。詳細: [review-log.md](review-log.md#skillnameloadercs) |
| SkillOcrTypes.cs | 済(2026-11-12、Codex相談(グループB・スキル側)、0件) | SkillReadingPipeline(型定義生成元)・ScreenshotCharmReader(ProcessAsyncでのSkillEntryの絞り込み・AnchorXの受け渡し。2026-11-08にMainWindowから移動)。既知課題: SkillReadResultのrecord Equalsがリスト内容ではなく参照比較(潜在的、観察点)。詳細: [review-log.md](review-log.md#skillocrtypescs) |
| TextOcrReader.cs | 済(2026-07-19、Codex相談、実バグ1件修正)+追記(2026-09-30) | SkillReadingPipeline(RecognizeAsync/RecognizeBytesAsync)・ScreenshotCharmReader.ProcessAllAsync(スレッドプールからの並列呼び出し。2026-11-08にMainWindow.ProcessScreenshotsAsyncから移動)。既知課題なし(CreateEngineのキャッシュなしは、生成約0.09msの実測と並列読み取りでエンジンを共有しない設計により意図的な仕様と確定、2026-09-30)。詳細: [review-log.md](review-log.md#textocrreadercs) |
| DuplicateChecker.cs | 済(2026-11-12、Codex相談(グループA)、0件)+変更(2026-11-12、グループBの修正でExcludeAlreadyImportedScreenshotsを追加。テストと利用者のデータでの試算で確認、この関数自体はCodexに相談していない) | CharmModel(スキル・スロット比較)・DuplicateCheckWindow(消費者)・MainWindow(取り込みの差分追加のExcludeIdentical)・CharmEditWindow(AreIdentical)。既知課題: 観察点2件(IdentityKeyの末尾0省略・区切り文字衝突、いずれも現行契約下では実害小)。詳細: [review-log.md](review-log.md#duplicatecheckercs) |
| RarityInference.cs | 済(2026-11-12、Codex相談(グループA)、0件) | CharmCombinationTable(構成表の読み込み・照合)・CharmEditWindow・MainWindow(CSV一括推定)。既知課題: 観察点1件(RARE7/8の特別な扱いは、RARE7と対にならないRARE8のパターンが加わると、武器スロットなしでも8を返しうる。据え置き)。2026-07-19のもう1件(読み込んだ構成表を書き換えられる)は、構成表の読み込みごとCharmCombinationTable.csへ移った。詳細: [review-log.md](review-log.md#rarityinferencecs) |
| CharmCombinationTable.cs | 済(2026-11-12、新規、Codex相談(グループC)、0件) | skill-groups.json/charm-combinations.json・RarityInference・CharmCombinationValidator・CharmTheoreticalValueChecker・CharmProbabilityEstimator・CharmValidator・MainWindow・CharmEditWindow。既知課題: 観察点2件(据え置き)。(1)読み込んだ構成表(配列・int[])を呼び出し元が書き換えられる(2026-07-19にRarityInference.csで記録した観察点が移ってきた。今の呼び出し元はどれも読むだけ)。(2)構成確率・理論値判定は、当てはまる組み合わせの最初の1つだけを使う。同じRAREの中で複数に当てはまらないことはテスト(OrderIndependentMatching_NoSameRarityAmbiguityAcrossRealData)で固定している。RAREをまたいでスキル構成が違う組み合わせに当てはまらないことは2026-11-12に総当たりで確かめたが、テストは無い(構成表が変わると判定が変わりうる)。2026-11-12より前の履歴は[review-log.md](review-log.md#rarityinferencecs)のRarityInference.cs節。詳細: [review-log.md](review-log.md#charmcombinationtablecs) |
| CharmProbabilityEstimator.cs | 済(2026-11-12、Codex相談(グループC)、0件) | CharmCombinationTable(共有ロジック、2026-11-12にRarityInferenceから移動)・MainWindow(詳細パネル表示)。既知課題なし。詳細: [review-log.md](review-log.md#charmprobabilityestimatorcs) |
| CharmTheoreticalValueChecker.cs | 済(2026-11-12、Codex相談(グループC)、0件) | CharmCombinationTable(共有ロジック、2026-11-12にRarityInferenceから移動)・CharmProbabilityEstimator(スロット一致判定ロジック共有)・MainWindow(詳細パネル表示)。既知課題なし。詳細: [review-log.md](review-log.md#charmtheoreticalvaluecheckercs) |
| CharmTypeLoader.cs | 済(2026-11-12、Codex相談(グループB)、データ1件追加(charm-types.jsonに希望の護石)) | charm-types.json・SkillReadingPipeline(武器スロット有無判定)・ScreenshotCharmReader(ProcessAsyncでのRarity設定・hasWeaponSlot受け渡し。2026-11-08にMainWindowのReadScreenshotから移動)・RarityInference(RAREの補完。InferRarityBatchは2026-11-08の65fee44でコードから無くなった)。既知課題: Lookupの末尾一致が「入力順の最初一致」で将来部分文字列関係の護石名追加時に誤マッチしうる(観察点、据え置き)。**2026-08-05: `Lookup`のnull戻り値をMainWindow側が`hasWeaponSlot=false`にフォールバックしていたため栄世の護石で誤混同する実バグが判明・MainWindow側で修正済み(本ファイル自体の変更なし)**。詳細: [review-log.md](review-log.md#charmtypeloadercs) |
| LetterboxNormalizer.cs | 済(2026-11-12、新規、Codex相談(グループB・スロット側)、0件) | SkillReadingPipeline(スキル欄の切り出し・「装備スキル」からの逆算)・SlotIconAnalyzer(探索領域・枠の大きさ・ScaleFactors)・SlotPanelConsistency(倍率)。既知課題: 黒帯の判定は行の平均の明るさが8以下かどうかのため、黒帯の無い16:9の画像でも上端・下端の数行が暗いと黒帯とみなされ、縮小率がわずかに1より小さくなって探索領域・枠の大きさが数pxずれうる(手元の画像で起きているかは未確認)。1枚の読み取りで4回呼ばれる(1回約0.7ms)。詳細: [review-log.md](review-log.md#letterboxnormalizercs) |
| CharmCombinationValidator.cs | 済(2026-11-12、新規、Codex相談(グループB)、0件・観察点1件) | CharmCombinationTable(構成表の読み込み・照合)・ScreenshotCharmReader(ProcessAsyncでの除外)。既知課題: 研鑽を含む護石は他のスキル・スロットにかかわらず通す(コメントとテストに書かれた意図した動き)ため、研鑽を含む護石でスキルのLvを読み違えても除外されない(観察点。研鑽は希望の護石固有で、希望の護石はスロットが無く枠の検出で除外されるため、ここまで届くことはまず無い)。詳細: [review-log.md](review-log.md#charmcombinationvalidatorcs) |
| SlotPanelConsistency.cs | 済(2026-11-12、新規、Codex相談(グループB・スロット側)、0件) | SlotIconTypes(SlotFrameMinDxFromAnchor・SlotFrameMaxDxFromAnchor)・ScreenshotCharmReader(ReadSlotsFromImage)・SkillReadingPipeline(アンカーの位置)。既知課題なし。詳細: [review-log.md](review-log.md#slotpanelconsistencycs) |

## 中優先（データモデル・UI本体）

| ファイル | 状態 | 相互作用チェック相手 |
|---|---|---|
| CharmModel.cs | 済(2026-11-12、Codex相談(グループA)、0件) | DuplicateChecker・CharmCsvConverter・RarityInference・MainWindow・CharmEditWindow(共通データモデル)。既知課題: 観察点1件(CharmSourceの既定値0がScreenshotのため、Sourceを設定し忘れるとスクショ由来として扱われる。今の生成経路はすべて設定している)+GameVersion.Ascendance未使用(将来拡張スキャフォールド)。2026-07-19の観察点のうち、リストの中身を書き換えられる件とスロットを3個に固定する型の件は、2026-11-08の不変条件の導入で解消。詳細: [review-log.md](review-log.md#charmmodelcs) |
| CharmCsvConverter.cs | 済(2026-11-12、Codex相談(グループA)、0件) | CharmModel・MainWindow(インポート/エクスポート)。既知課題: 未知スキル名は取り込みを止めず、警告として件数を示して取り込む(2026-11-08から。CharmImportScreening.ScreenCsv。それまでは検証せずに受け入れており、拒否しないのはユーザー判断)・ParseTextの行番号ずれ(低優先度)。詳細: [review-log.md](review-log.md#charmcsvconvertercs) |
| CharmValidator.cs | 済(2026-11-12、新規、Codex相談(グループA)、コメントの修正1件) | CharmCombinationTable(skill-groups)・SlotValidation(スロットの形)・CharmImportScreening(CSV・JSONの取り込み。MainWindowから呼ばれる)・CharmEditWindow(手動入力)。スクショの読み取りはこれを呼ばない(構成表との照合とスロットの検査で除外する)。既知課題なし。詳細: [review-log.md](review-log.md#charmvalidatorcs) |
| CharmImportScreening.cs | 済(2026-11-12、新規、Codex相談(グループA)、0件) | CharmValidator(検査)・RarityInference(RAREの推定)・CharmJsonSerializer(CharmJsonParseResult)・MainWindow(CSV・JSONの取り込み)。既知課題: CSVのErrorの行番号は空行を数えない(CharmCsvConverter.ParseTextと同じ数え方)。詳細: [review-log.md](review-log.md#charmimportscreeningcs) |
| CharmJsonSerializer.cs | 済(2026-11-12、新規、Codex相談(グループA)、0件) | CharmModel(不変条件)・CharmImportScreening(CharmJsonParseResult)・MainWindow(起動時の読み込み・保存・データ管理の書き出しと取り込み)。既知課題: source・versionをEnum.TryParseで読むため、"99"のような数字の文字列が定義に無い値のまま取り込まれる(Charm側も検査しない。画面の入手元は「不明」と表示され、保存すると"99"のまま書き戻される。アプリ自身が書いたJSONでは起きない)。小文字の"screenshot"等は読めずCSVインポート扱いになり、スクショの差分読み込みの基準日時から外れる。1項目の型の誤り("lv": "3"等)はその項目だけでなくファイル全体がJSONとして読めない扱いになる(起動時は保存を止める、CLAUDE.mdの設計どおり)。詳細: [review-log.md](review-log.md#charmjsonserializercs) |
| ScreenshotFileSelector.cs | 済(2026-11-12、新規、Codex相談(グループA)、0件) | CharmModel(Source・SourceTimestamp)・ScreenshotCharmReader(SourceTimestampにファイルの更新日時を入れる)・MainWindow(読み取りの開始)。既知課題: 読み取りに失敗したファイルより新しいファイルが1つでも取り込まれると、失敗したファイルは次から対象外になる(コメントに書かれた既知の動きで、全件読み直しで対処できる)。詳細: [review-log.md](review-log.md#screenshotfileselectorcs) |
| MainWindow.xaml.cs | 済(2026-11-12、Codex相談(グループA・B・C)、グループAで起動時の読み込み・保存・取り込み・編集・初期化(0件・観察点1件)、グループBでスクショ読み取り(不具合1件修正)、グループCで2026-11-12の整理(設定・テーマ・詳細パネル・確率の表示・データ管理、0件)。画面の操作で確かめたのは手動入力・編集の流れだけ) | 全モジュールのオーケストレーション中枢(SkillReadingPipeline・SlotIconAnalyzer・CharmCsvConverter・DuplicateChecker・RarityInference・CharmTypeLoader・ErrorLogger・GameSkillOrder・SettingsWindow・CharmEditWindow・DuplicateCheckWindow)。**並列読み取り(2026-11-08からScreenshotCharmReader.ProcessAllAsync)ではCore側の共有状態・ErrorLogger(非スレッドセーフ)との相互作用に要注意**。既知課題: 観察点3件(据え置き、IsExactDuplicateのコード重複(2026-11-12に解消を確認。取り込みの差分追加はDuplicateChecker.ExcludeIdenticalを使い、MainWindowに独自の判定は無い)・武器スロット表示不整合・Dispose漏れリスク)+並列読み取りの観察点3件(ワーカー外例外時に部分結果が集計されない・中断直後に各ワーカーが最大1枚多く開始しうる・ログ一括書き込み時のUI停止とログ時刻の変化)+UI改修の観察点1件(ハイコントラスト時は独自色がライト固定で詳細パネルの文字色が未指定のため読みにくい可能性、詳細はreview-log.md。タイトルバー統合で消失したAlt+Dアクセスキーは2026-09-30にAlt+D/S/Aのショートカットとして復元済み)。DecorationEquippedException.csの旧方式参照コメントは2026-09-30に修正済み。+観察点1件(2026-11-12、SaveCharmsはFile.WriteAllTextで直接書くため、書き込みの途中で電源が切れるとcharms.jsonが途中までになりうる。次の起動では保存を止めるがデータは失われる。起きる確率が低いため据え置き)。詳細: [review-log.md](review-log.md#mainwindowxamlcs) |
| CharmEditWindow.xaml.cs | 済(2026-11-12、Codex相談(グループA)、不具合2件修正(何も変えずにOKを押すとRAREが消える場合がある・名前とLvの片方だけの欄が黙って捨てられる)、一時フォルダのアプリを画面操作して確認) | SkillNameLoader・GameSkillOrder・RarityInference(レアリティ動的更新)・DuplicateChecker(AreIdentical)・CharmValidator(OK時の検査)。既知課題: TitleBar表示不整合(観察点、据え置き)。BuildCharmのサイレントスキップは2026-11-12にOK時の拒否として解消。詳細: [review-log.md](review-log.md#charmeditwindowxamlcs) |
| CharmDisplayText.cs | 済(2026-11-12、新規、Codex相談(グループC)、0件) | MainWindow(CharmListItem・読み取り結果の一覧・詳細パネル)・DuplicateCheckWindow(CharmListItemの表示文字列を使う)・CharmModel(ArmorSlotsDescending)。既知課題なし。詳細: [review-log.md](review-log.md#charmdisplaytextcs) |
| ScreenshotReadingTally.cs | 済(2026-11-12、新規、Codex相談(グループB)、NoCharmNameのerror.logの文を変更) | ScreenshotCharmReader(ScreenshotOutcome・ScreenshotOutcomeKind)・MainWindow(error.logへの書き込みと表示)。既知課題なし。詳細: [review-log.md](review-log.md#screenshotreadingtallycs) |
| CharmListItem.cs | 済(2026-11-12、新規、Codex相談(グループC)、0件) | MainWindow(護石一覧)・DuplicateCheckWindow(表示文字列)・CharmDisplayText。既知課題なし。詳細: [review-log.md](review-log.md#charmlistitemcs) |
| AppSettings.cs | 済(2026-11-12、新規、Codex相談(グループC)、0件) | MainWindow(settings.jsonの読み書き)・ThemeService(theme)。既知課題なし。詳細: [review-log.md](review-log.md#appsettingscs) |
| DuplicateCheckWindow.xaml.cs | 済(2026-11-12、Codex相談(グループC)、0件) | DuplicateChecker(消費者)・MainWindow(CharmItemsを渡す)。既知課題: 完全同一グループのRarity表示不整合(観察点、据え置き、実害は大幅軽減済み)。詳細: [review-log.md](review-log.md#duplicatecheckwindowxamlcs) |
| SettingsWindow.xaml.cs | 済(2026-07-19、Codex相談、0件)+変更(2026-09-30、テーマ選択欄追加、同日Codex相談で再精査済み、0件) | MainWindow(設定永続化: ウィンドウサイズ・位置・スクショフォルダ・テーマ)・ThemeService。既知課題: BrowseFolder_ClickのDirectory.Exists未検証(観察点、実害極小)。詳細: [review-log.md](review-log.md#settingswindowxamlcs) |
| ThemeService.cs | 済(2026-09-30、新規、Codex相談、0件・観察点1件) | App.xaml(独自色定義Palette/*.xamlの登録)・MainWindow(起動時と設定変更時のApply)・SettingsWindow(AppThemeMode)・WPF-UIのApplicationThemeManager/SystemThemeWatcher(4.3.0のソースで挙動確認済み)。既知課題: ハイコントラスト時は独自色をライトで代用(未検証の観察点)、実行中のWindowsテーマ変更への追従は実機未検証。詳細: [review-log.md](review-log.md#themeservicecs) |
| AboutWindow.xaml.cs | 済(2026-09-30、新規、Codex相談、0件) | MainWindow(AboutMenu_ClickでShowDialog)・README(免責・帰属表示の要約)・csproj(使用ライブラリのバージョンは直書き)。既知課題: 使用ライブラリのバージョン表記はパッケージ更新時に手動で合わせる必要あり。詳細: [review-log.md](review-log.md#aboutwindowxamlcs) |
| ErrorLogger.cs | 済(2026-07-19、Codex相談、実バグ1件修正+観察点2件) | MainWindow各所・CharmEditWindow・DuplicateCheckWindow(呼び出し元)。既知課題: LogFilePathの配置先依存(配布形式は確定済みのため前提再評価が必要)・ローテーションなし。詳細: [review-log.md](review-log.md#errorloggercs) |
| GameSkillOrder.cs | 済(2026-11-12、Codex相談(グループC)、0件) | skill-order.json・CharmEditWindow(ComboBox表示順)・SkillNameLoader(正規名との整合)。既知課題: Orderプロパティの可変List公開(観察点、実害なし。今の宣言は`Lazy<IReadOnlyList<string>>`で、中身はParseJsonが返すList<string>のため、キャストで書き換えられる(2026-11-12にコードで確認)。唯一の呼び出し元(CharmEditWindow)はToListでコピーして使う)。詳細: [review-log.md](review-log.md#gameskillordercs) |

## 低優先（ボイラープレート）

| ファイル | 状態 | 相互作用チェック相手 |
|---|---|---|
| App.xaml.cs | 済(2026-11-12、Codex相談(グループA)、不具合1件修正(起動中の例外で、ウィンドウの無いプロセスが多重起動防止のMutexを持ったまま残る)、最小のWPFアプリで修正前後を確認) | ThemeService(テーマ適用はMainWindowのコンストラクタ)。**2026-09-30に設計意図が「ダーク固定」から「選択式・既定はOS追従」に変わった。2026-07-19に「OS追従はダーク固定と矛盾」として直した経緯を根拠に戻さないこと**。既知課題: Mutex名にGlobal\が無くログオンセッション単位(観察点、ポータブルアプリの想定利用範囲では許容)。詳細: [review-log.md](review-log.md#appxamlcs) |
| AssemblyInfo.cs | 済(2026-07-19、Codex相談、0件) | CharmChecker.App/AssemblyInfo.cs・CharmChecker.Core/AssemblyInfo.cs(InternalsVisibleTo)。既知課題なし。詳細: [review-log.md](review-log.md#assemblyinfocs) |
| EmbeddedResources.cs | 済(2026-11-12、新規、Codex相談(グループC)、0件) | CharmChecker.Core.csproj(EmbeddedResourceの登録)・CharmCombinationTable・CharmTypeLoader・SkillNameLoader・GameSkillOrder。既知課題なし。詳細: [review-log.md](review-log.md#embeddedresourcescs) |
| ScreenshotImage.cs | 済(2026-11-12、新規、Codex相談(グループB・スロット側)、0件) | SkillReadingPipeline(ReadWithMetadataAsync・ReadWithMetadataFromImageAsync)・ScreenshotCharmReader(ProcessAsync・ReadSlots・ReadSlotsFromImage)。既知課題なし(日本語を含むパスも、ANSIコードページ932のWindowsでImReadで読めることを確認。cp932で表せない文字は未確認)。詳細: [review-log.md](review-log.md#screenshotimagecs) |
| DecorationEquippedException.cs | 済(2026-11-12、Codex相談(グループB・スロット側)、0件) | ScreenshotCharmReader(ToLevelNumberが投げ、ProcessAsyncがDecorationEquippedとして扱う)。既知課題なし。詳細: [review-log.md](review-log.md#decorationequippedexceptioncs) |
| InvalidSlotConfigurationException.cs | 済(2026-11-12、Codex相談(グループA)、コメントの修正1件) | SlotValidation(TryValidate)・ScreenshotCharmReader(ReadSlotsFromImageが投げ、ProcessAsyncがInconsistentWithCombinationsとして扱う)。既知課題なし。詳細: [review-log.md](review-log.md#invalidslotconfigurationexceptioncs) |
| SlotDetectionFailedException.cs | 済(2026-11-12、Codex相談(グループB・スロット側)、0件) | ScreenshotCharmReader(ReadSlotsFromImageが投げ、ProcessAsyncがSlotDetectionFailedとして扱う)。既知課題なし。詳細: [review-log.md](review-log.md#slotdetectionfailedexceptioncs) |
| SlotPanelMismatchException.cs | 済(2026-11-12、Codex相談(グループB・スロット側)、コメントの修正1件) | SlotPanelConsistency・ScreenshotCharmReader(ReadSlotsFromImageが投げ、ProcessAsyncがSlotPanelMismatchとして扱う)。既知課題なし。詳細: [review-log.md](review-log.md#slotpanelmismatchexceptioncs) |

## ドキュメント精査

| ドキュメント | 状態 | 発見 |
|---|---|---|
| README.md | 済(2026-11-12、Codex相談(グループA)、取り込みの検査・データ保護・編集できない護石・差分読み込みの記述を動作と照合、1件追記)+変更(2026-11-12、グループBの修正に合わせて、再スキャンで取り込み済みの護石を二重に追加しないことを1文追記) | 15行目に、不正な項目があるのにバックアップを作れなかった場合も保存しないことを追記。既知課題: 文書全体の構成をCLAUDE.mdの「README運用方針」(9項目)と突き合わせることは未実施(今のREADMEには「フィードバック・不具合報告」節もある)。詳細: [review-log.md](review-log.md#readmemd) |
| CLAUDE.md | 済(2026-07-19、Codex相談、記載漏れ4件+陳腐化2件を修正) | 記載漏れ2件・陳腐化2件を修正。既知課題なし(その後大量に追記された内容は都度セッションでユーザー確認済みのため対象外)。詳細: [review-log.md](review-log.md#claudemd) |

## 対象外（本チェッカーの対象外・参照専用）

- `legacy/` — 旧Node.js + Claude Vision実装一式。C#移植のロジック参照元として保持するのみで現行動作パスではない
- `resources/*.json` — データファイル自体はコードではなくデータ照合の対象（別途照合済みと記録されている。参照先として書かれていたメモリ`project-charm-checker-license-checklist`は、2026-11-12時点で存在しない）
- `app/CharmChecker.Core/SlotIcon/EmptySlotTemplates.cs` — 空スロットの見本の画素の値（自動生成のデータ）。生成元は`app/CharmChecker.Tests/EmptySlotTemplateGenerator.cs`（見本の出典・座標はそのファイルのSources配列）。2026-11-12にこの節へ追加
