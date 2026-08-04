# コード精査台帳

実装モジュールを1本ずつ精査するための進行管理台帳（2026-07-12作成）。
[Ragmas 6v6動画解析ツールの台帳](../../Knowledge/Games/Ragmas/tools/6v6-video-analysis/review-ledger.md)の方式を踏襲。
2026-08-03、[コード精査台帳テンプレート](../../Knowledge/Tech/ClaudeCode/code-review-ledger-template.md)に基づき、
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
指摘（高2件・中3件）を得て、`charm-combinations.json`の一次データ確認等で裏取りした上で実バグ
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

## 精査手順（1本あたり）

1. 実装+対応テストを読む
2. コード内コメント・CLAUDE.mdの「〜と同じ方針」「〜で判定」等の主張を実コードと突き合わせる
3. 「相互作用チェック相手」列のモジュールとの整合を確認する（単体では正しく見える部分適用漏れを狙う）
4. 疑わしい箇所は実データ（`assets/`のスクリーンショット・実CSV・`charm-lists/`）で裏取りする
5. 修正は回帰テスト付きで行い、本台帳の状態更新＋review-log.mdへの詳細追記までを1単位としてコミットする
6. 深い判断・セカンドオピニオンが必要な指摘は外部レビュー（Codex等）に相談してもよい

## 状態の凡例

- **未** — 未精査
- **済(日付)** — 精査済み。発見があれば括弧内に件数
- **済(日付)+変更** — 精査後にコードが変わっており、差分の再精査が必要

## 高優先（コアロジック・複雑な画像処理・ビジネスルール）

| ファイル | 状態 | 相互作用チェック相手 |
|---|---|---|
| SlotIconAnalyzer.cs | 済(2026-08-05、Codex相談、実バグ0件・修正案1件は実データ検証で回帰確認し撤回) | SlotIconTypes(定数)・SlotValidation(判定結果の検証)・SkillReadingPipeline(護石名ベース種別判定の受け渡し)・MainWindow.ReadSlots(BOX優先ロジックとの相互作用に要注意、撤回の経緯を参照)。既知課題: 融合フレーム回復の観察点2件+MergeOverlapping/FilterYCluster観察点3件+RangesOverlapのY非考慮1件+「filtered完全に空」ケースへの対応は安全に実現できず据え置き(いずれも実害小、詳細はreview-log.md)。詳細: [review-log.md](review-log.md#sloticonanalyzercs) |
| SlotIconTypes.cs | 済(2026-07-19、Codex相談、0件) | SlotIconAnalyzer(定数消費)・SlotValidation(ArmorSlotMaxByPositionとの整合)。既知課題なし。詳細: [review-log.md](review-log.md#sloticontypescs) |
| SlotValidation.cs | 済(2026-07-19、Codex相談、実バグ1件修正) | SlotIconAnalyzer(判定結果)・MainWindow.ClassifyFrames(武器/防具振り分け後の呼び出し元)。既知課題: 武器スロット非ソートは将来Lv2/Lv3許可時に要再設計(据え置き)。詳細: [review-log.md](review-log.md#slotvalidationcs) |
| SkillReadingPipeline.cs | 済(2026-08-05、Codex相談、実バグ1件修正) | ImageVariantFactory・LvParser・SkillNameNormalizer・SkillNameLoader・TextOcrReader・SlotIconAnalyzer(護石名の受け渡し)・CharmTypeLoader。既知課題: 過去データ(2026-07-19以前読み取りのCSV)の護石名誤帰属可能性(ユーザー報告済み)、観察点3件(据え置き)。**「装備スキル」逆算アンカーのocrScale補正は修正済みだが、この経路を実際に通る実データでの検証は未確認**。詳細: [review-log.md](review-log.md#skillreadingpipelinecs) |
| ImageVariantFactory.cs | 済(2026-07-19、Codex相談、0件・観察点1件) | SkillReadingPipeline(5バリエーション生成・幅ガード)。既知課題: Createメソッドの例外時ネイティブメモリリーク(観察点、据え置き)。詳細: [review-log.md](review-log.md#imagevariantfactorycs) |
| LvParser.cs | 済(2026-07-19、Codex相談、実バグ1件修正) | SkillReadingPipeline(184行目、右側候補のみ適用)。既知課題なし。詳細: [review-log.md](review-log.md#lvparsercs) |
| SkillNameNormalizer.cs | 済(2026-07-21、Codex相談、実バグ1件修正) | SkillNameLoader(正解候補120種)・SkillReadingPipeline。既知課題: 特殊・分解形式の濁点は現行データに該当なしのため見送り(観察点)。詳細: [review-log.md](review-log.md#skillnamenormalizercs) |
| SkillNameLoader.cs | 済(2026-07-19、Codex相談、設計強化1件) | SkillNameNormalizer(正解候補供給元)・MainWindow(LoadFromEmbeddedResource)。既知課題なし(8スキル宿題は2026-07-20解消済み)。詳細: [review-log.md](review-log.md#skillnameloadercs) |
| SkillOcrTypes.cs | 済(2026-07-19、Codex相談、0件) | SkillReadingPipeline(型定義生成元)・MainWindow(SkillEntryフィルタ)。既知課題: SkillReadResultのrecord Equalsがリスト内容ではなく参照比較(潜在的、観察点)。詳細: [review-log.md](review-log.md#skillocrtypescs) |
| TextOcrReader.cs | 済(2026-07-19、Codex相談、実バグ1件修正) | SkillReadingPipeline(RecognizeAsync/RecognizeBytesAsync)。既知課題: CreateEngineが呼び出しごとに再生成しキャッシュしない(観察点、性能上緊急度低)。詳細: [review-log.md](review-log.md#textocrreadercs) |
| DuplicateChecker.cs | 済(2026-07-19、Codex相談、0件・観察点3件) | CharmModel(スキル・スロット比較)・DuplicateCheckWindow(消費者)。既知課題: 観察点3件(IdentityKeyの末尾0省略・区切り文字衝突・重複スキル名構成、いずれも現行契約下では実害小)。詳細: [review-log.md](review-log.md#duplicatecheckercs) |
| RarityInference.cs | 済(2026-07-19)+変更(2026-07-21、Codex相談で再精査済み) | skill-groups.json/charm-combinations.json・CharmEditWindow・MainWindow(CSV一括推定)・CharmProbabilityEstimator・CharmTheoreticalValueChecker(共有ロジック提供元)。既知課題: 観察点2件(据え置き、将来データ変更時要再検討)。詳細: [review-log.md](review-log.md#rarityinferencecs) |
| CharmProbabilityEstimator.cs | 済(2026-07-21、Codex相談、実バグなし・共有ロジック集約1件) | RarityInference(共有ロジック)・MainWindow(詳細パネル表示)。既知課題なし。詳細: [review-log.md](review-log.md#charmprobabilityestimatorcs) |
| CharmTheoreticalValueChecker.cs | 済(2026-07-21、Codex相談、実バグ1件修正+テスト強化2件) | RarityInference(共有ロジック)・CharmProbabilityEstimator(スロット一致判定ロジック共有)・MainWindow(詳細パネル表示)。既知課題なし。詳細: [review-log.md](review-log.md#charmtheoreticalvaluecheckercs) |
| CharmTypeLoader.cs | 済(2026-07-19、Codex相談、実バグ1件修正(MainWindow側)+設計強化1件+観察点2件)+追記(2026-08-05) | charm-types.json・SkillReadingPipeline(武器スロット有無判定)・MainWindow(ReadScreenshotでのRarity設定・hasWeaponSlot受け渡し)・RarityInference(InferRarityBatchでの補完)。既知課題: Lookupの末尾一致が「入力順の最初一致」で将来部分文字列関係の護石名追加時に誤マッチしうる(観察点、据え置き)。**2026-08-05: `Lookup`のnull戻り値をMainWindow側が`hasWeaponSlot=false`にフォールバックしていたため栄世の護石で誤混同する実バグが判明・MainWindow側で修正済み(本ファイル自体の変更なし)**。詳細: [review-log.md](review-log.md#charmtypeloadercs) |

## 中優先（データモデル・UI本体）

| ファイル | 状態 | 相互作用チェック相手 |
|---|---|---|
| CharmModel.cs | 済(2026-07-19、Codex相談、0件・観察点3件) | DuplicateChecker・CharmCsvConverter・RarityInference・MainWindow・CharmEditWindow(共通データモデル)。既知課題: 観察点3件(据え置き、b級)+GameVersion.Ascendance未使用(将来拡張スキャフォールド)。詳細: [review-log.md](review-log.md#charmmodelcs) |
| CharmCsvConverter.cs | 済(2026-07-19、Codex相談、実バグ2件修正+観察点2件) | CharmModel・MainWindow(インポート/エクスポート)。既知課題: 未知スキル名の無検証受理(ユーザー判断で見送り)・ParseTextの行番号ずれ(低優先度)。詳細: [review-log.md](review-log.md#charmcsvconvertercs) |
| MainWindow.xaml.cs | 済(2026-08-05、Codex相談、実バグ2件修正) | 全モジュールのオーケストレーション中枢(SkillReadingPipeline・SlotIconAnalyzer・CharmCsvConverter・DuplicateChecker・RarityInference・CharmTypeLoader・ErrorLogger・GameSkillOrder・SettingsWindow・CharmEditWindow・DuplicateCheckWindow)。既知課題: 観察点3件(据え置き、IsExactDuplicateのコード重複・武器スロット表示不整合・Dispose漏れリスク)。**DecorationEquippedException.csのXMLコメントが削除済みの旧方式(ClassifyLevel)を参照したまま(次回要修正)**。詳細: [review-log.md](review-log.md#mainwindowxamlcs) |
| CharmEditWindow.xaml.cs | 済(2026-07-19、Codex相談、実バグ2件修正、実機動作確認済み) | SkillNameLoader・GameSkillOrder・RarityInference(レアリティ動的更新)。既知課題: BuildCharmのサイレントスキップ・TitleBar表示不整合(観察点、据え置き)。詳細: [review-log.md](review-log.md#charmeditwindowxamlcs) |
| DuplicateCheckWindow.xaml.cs | 済(2026-07-19、Codex相談、実バグ1件修正+観察点1件) | DuplicateChecker(消費者)・MainWindow(CharmItemsを渡す)。既知課題: 完全同一グループのRarity表示不整合(観察点、据え置き、実害は大幅軽減済み)。詳細: [review-log.md](review-log.md#duplicatecheckwindowxamlcs) |
| SettingsWindow.xaml.cs | 済(2026-07-19、Codex相談、0件) | MainWindow(設定永続化: ウィンドウサイズ・位置・スクショフォルダ)。既知課題: BrowseFolder_ClickのDirectory.Exists未検証(観察点、実害極小)。詳細: [review-log.md](review-log.md#settingswindowxamlcs) |
| ErrorLogger.cs | 済(2026-07-19、Codex相談、実バグ1件修正+観察点2件) | MainWindow各所・CharmEditWindow・DuplicateCheckWindow(呼び出し元)。既知課題: LogFilePathの配置先依存(配布形式は確定済みのため前提再評価が必要)・ローテーションなし。詳細: [review-log.md](review-log.md#errorloggercs) |
| GameSkillOrder.cs | 済(2026-07-19、Codex相談、fail-fast化1件+回帰テスト追加) | skill-order.json・CharmEditWindow(ComboBox表示順)・SkillNameLoader(正規名との整合)。既知課題: Orderプロパティの可変List公開(観察点、実害なし)。詳細: [review-log.md](review-log.md#gameskillordercs) |

## 低優先（ボイラープレート）

| ファイル | 状態 | 相互作用チェック相手 |
|---|---|---|
| App.xaml.cs | 済(2026-07-21、Codex相談、実バグ1件修正・実機検証済み) | MainWindow(SystemThemeWatcher.Watch(this))。既知課題: Mutex名にGlobal\が無くログオンセッション単位(観察点、ポータブルアプリの想定利用範囲では許容)。詳細: [review-log.md](review-log.md#appxamlcs) |
| AssemblyInfo.cs | 済(2026-07-19、Codex相談、0件) | CharmChecker.App/AssemblyInfo.cs・CharmChecker.Core/AssemblyInfo.cs(InternalsVisibleTo)。既知課題なし。詳細: [review-log.md](review-log.md#assemblyinfocs) |

## ドキュメント精査

| ドキュメント | 状態 | 発見 |
|---|---|---|
| README.md | 済(2026-07-19、Codex相談、全文書き直し+副産物で実バグ1件発見・修正)+変更(2026-07-20未反映) | CLAUDE.mdの9項目構成で全文書き直し。既知課題: 2026-07-20の利用者向け優先構成改訂は未反映、次回精査時にCLAUDE.mdの最新構成と突き合わせが必要。詳細: [review-log.md](review-log.md#readmemd) |
| CLAUDE.md | 済(2026-07-19、Codex相談、記載漏れ4件+陳腐化2件を修正) | 記載漏れ2件・陳腐化2件を修正。既知課題なし(その後大量に追記された内容は都度セッションでユーザー確認済みのため対象外)。詳細: [review-log.md](review-log.md#claudemd) |

## 対象外（本チェッカーの対象外・参照専用）

- `legacy/` — 旧Node.js + Claude Vision実装一式。C#移植のロジック参照元として保持するのみで現行動作パスではない
- `resources/*.json` — データファイル自体はコードではなくデータ照合の対象（[[project-charm-checker-license-checklist]]で別途照合済み）
