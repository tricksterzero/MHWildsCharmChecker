# コード精査台帳

実装モジュールを1本ずつ精査するための進行管理台帳（2026-07-12作成）。
[Ragmas 6v6動画解析ツールの台帳](../../Knowledge/Games/Ragmas/tools/6v6-video-analysis/review-ledger.md)の方式を踏襲。

**運用方針**: 2026-06-25に全体コードレビュー+セキュリティレビューを実施し12件修正済みだが、
「どのファイルを見て問題なしだったか」は記録が残っていない。本台帳は同じ反省に立ち、
**精査の実施自体を記録する**（問題なしでも日付を残す）。そのため、2026-06-25の修正コミット
（`2b63369`/`b090f4f`/`b62c0fb`等）で実際に差分が入ったファイルのみ「済」とし、
残りは記録が無いため未扱いとする（再精査しても害はない安全側の倒し方）。

## 精査手順（1本あたり）

1. 実装+対応テストを読む
2. コード内コメント・CLAUDE.mdの「〜と同じ方針」「〜で判定」等の主張を実コードと突き合わせる
3. 「相互作用チェック相手」列のモジュールとの整合を確認する（単体では正しく見える部分適用漏れを狙う）
4. 疑わしい箇所は実データ（`assets/`のスクリーンショット・実CSV・`charm-lists/`）で裏取りする
5. 修正は回帰テスト付きで行い、本台帳の状態更新までを1単位としてコミットする

## 状態の凡例

- **未** — 未精査
- **済(日付)** — 精査済み。発見があれば括弧内に件数
- **済(日付)+変更** — 精査後にコードが変わっており、差分の再精査が必要

## 高優先（コアロジック・複雑な画像処理・ビジネスルール）

| ファイル | 状態 | 相互作用チェック相手 |
|---|---|---|
| SlotIconAnalyzer.cs | 済(2026-06-25、マジックナンバー定数化+コードレビュー修正1件) | SlotIconTypes(定数)・SlotValidation(判定結果の検証)・SkillReadingPipeline(護石名ベース種別判定の受け渡し) |
| SlotIconTypes.cs | 済(2026-07-12、0件) | SlotIconAnalyzer(定数の消費)・SlotValidation(ArmorSlotMaxByPositionとの整合)。定数値自体はSlotIconAnalyzer精査時に実データ検証済み(2026-06-15メモリ参照)のものをそのまま踏襲しており齟齬なし |
| SlotValidation.cs | 済(2026-07-12、0件) | SlotIconAnalyzer(判定結果)・MainWindow.ClassifyFrames(武器/防具振り分け後に呼ばれる呼び出し元)。ArmorSlotMaxByPosition=[3,1,0](防具3個目は常に0)が実データと矛盾しないか、3フレーム検出される「栄世の護石」(20260614061441_1.jpg、防具2+武器1の特殊護石)で裏取り。ClassifyFramesがhasWeaponSlot時にframes[0]を武器スロットへ先に振り分けるため、SlotValidationへ渡る時点で防具は実質2枠のみとなり制約と整合することを確認(コード確認+実画像目視)。観察点(据え置き): 武器スロットのバリデーションは防具と異なりOrderByDescending未実施だが、現行ゲームでは武器スロットはClassifyFrames側の設計上常にweaponSlots[0]のみが埋まる(複数武器スロットを持つ護石が存在しない)ため実害なし。将来複数武器スロット護石が実装された場合は要再検討 |
| SkillReadingPipeline.cs | 済(2026-06-25) | ImageVariantFactory・LvParser・SkillNameNormalizer・SkillNameLoader・TextOcrReader・SlotIconAnalyzer(護石名の受け渡し)・CharmTypeLoader |
| ImageVariantFactory.cs | 済(2026-06-25) | SkillReadingPipeline(5バリエーション生成・幅ガード) |
| LvParser.cs | 未 | SkillReadingPipeline(Lv解析) |
| SkillNameNormalizer.cs | 済(2026-06-25) | SkillNameLoader(正解候補121種・HashSet化)・SkillReadingPipeline |
| SkillNameLoader.cs | 未 | SkillNameNormalizer・CharmEditWindow(ComboBox)・GameSkillOrder(表示順との整合) |
| SkillOcrTypes.cs | 未 | SkillReadingPipeline |
| TextOcrReader.cs | 未 | SkillReadingPipeline(OCRエンジン生成・デコード方式) |
| DuplicateChecker.cs | 済(2026-06-25) | CharmModel(スキル・スロット比較)・DuplicateCheckWindow(消費者) |
| RarityInference.cs | 済(2026-06-25) | skill-groups.json/charm-combinations.json・CharmEditWindow・MainWindow(CSV一括推定) |
| CharmTypeLoader.cs | 済(2026-06-25) | charm-types.json・SkillReadingPipeline(武器スロット有無判定) |

## 中優先（データモデル・UI本体）

| ファイル | 状態 | 相互作用チェック相手 |
|---|---|---|
| CharmModel.cs | 未 | DuplicateChecker・CharmCsvConverter・RarityInference・MainWindow・CharmEditWindow（共通データモデル） |
| CharmCsvConverter.cs | 済(2026-06-25) | CharmModel・MainWindow(インポート/エクスポート) |
| MainWindow.xaml.cs | 済(2026-06-25、一部)+変更 | 全モジュールのオーケストレーション。920行中、06-25修正は一部箇所のみ（Task.Run化・存在チェック等）で全体は未精査。SkillReadingPipeline・SlotIconAnalyzer・CharmCsvConverter・DuplicateChecker・RarityInference・ErrorLogger・GameSkillOrder |
| CharmEditWindow.xaml.cs | 済(2026-06-25) | SkillNameLoader・GameSkillOrder・RarityInference(レアリティ動的更新) |
| DuplicateCheckWindow.xaml.cs | 未 | DuplicateChecker(消費者) |
| SettingsWindow.xaml.cs | 未 | MainWindow(設定永続化: ウィンドウサイズ・位置・スクショフォルダ) |
| ErrorLogger.cs | 済(2026-06-16新規実装、2026-06-25呼び出し側修正) | MainWindow各所（例外通知・error.log出力） |
| GameSkillOrder.cs | 済(2026-06-25) | skill-order.json・CharmEditWindow(ComboBox表示順)・SkillNameLoader(正規名との整合) |

## 低優先（ボイラープレート）

| ファイル | 状態 | 相互作用チェック相手 |
|---|---|---|
| App.xaml.cs | 未 | - |
| AssemblyInfo.cs | 未 | - |

## ドキュメント精査

| ドキュメント | 状態 | 発見 |
|---|---|---|
| README.md | 済(2026-07-12、1件) | 2026-06-14時点の初期版のまま（「今後C#移行予定」という将来形の記述、Node.js Vision前提）で、CLAUDE.mdに定めた正式な9項目構成（動作環境・使い方・既知の制約・CSV互換・ビルド方法・免責・ライセンス等）に未更新。C#実装は完了済みのため記述と実態が乖離 |
| CLAUDE.md | 未 | - |

## 対象外（本チェッカーの対象外・参照専用）

- `legacy/` — 旧Node.js + Claude Vision実装一式。C#移植のロジック参照元として保持するのみで現行動作パスではない
- `resources/*.json` — データファイル自体はコードではなくデータ照合の対象（[[project-charm-checker-license-checklist]]で別途照合済み）
