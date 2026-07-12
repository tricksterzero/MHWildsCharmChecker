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
| LvParser.cs | 済(2026-07-12、0件) | SkillReadingPipeline(184行目、`t.X0 >= v.LvXThreshold`で右側候補のみに適用・null返却時はlvsに追加せずスキップ)。CLAUDE.md記載の文字置換仕様(し→L, l→L, I→1, ー除去)と実装が一致。観察点(据え置き): `int.Parse(digits)`はdigits長に上限が無くOCR誤読で10桁超の数字列が来ればOverflowExceptionを送出しうるが、呼び出し元のMainWindow側スクショ処理ループ(433-464行目)がtry/catchで1枚ずつ隔離済み(失敗カウントに計上され後続処理は継続)のため実害なし。`.Replace("l","L")`は直後の`ToUpperInvariant()`で同じ変換が行われるため実質冗長だが、動作に影響なし(修正不要と判断) |
| SkillNameNormalizer.cs | 済(2026-06-25) | SkillNameLoader(正解候補121種・HashSet化)・SkillReadingPipeline |
| SkillNameLoader.cs | 済(2026-07-12、1件・データ修正) | SkillNameNormalizer(正解候補121種の供給元)・MainWindow(`LoadFromEmbeddedResource`でOCRパイプラインへ供給)。**実バグ発見・修正**: 相互作用チェックで`skill-decoration-map.json`(正規名121種)と`RarityInference`が使う`skill-groups.json`(113種)を突き合わせたところ3件で表記が食い違っていた(防御力ＤＯＷＮ耐性/貫通弾・竜の矢強化/災禍転福 vs 半角DOWN/一矢/転覆)。`RarityInference.Infer()`は完全一致検索で失敗時null(RARE推定サイレント失敗、`RarityInference.cs:47-50`)のため、OCRでこれらのスキルを読み取ると正規化は成功するのにRARE推定だけ理由不明で失敗する実害があった。`skill-name-checklist.md`/`deco_checklist.txt`で正規側が正しいと裏取りの上、`resources/skill-groups.json`の3件を正規名に修正(過去の全角半角統一パス`1b4897c`で取りこぼされていたもの)。回帰テスト3件追加(`RarityInferenceTests.CanonicalSkillName_ResolvesInSkillGroups`)。**観察点(要調査・据え置き)**: decoration-mapにあってskill-groups.jsonに無いスキルが8件(飛び込み・緩衝・ジャンプ鉄人・昆虫標本の達人・クライマー・オトモへの采配・閃光強化・ハンター生活)。護石に実際つくスキルかどうかユーザー未確認のため、次回別途調査 |
| SkillOcrTypes.cs | 済(2026-07-12、0件) | SkillReadingPipeline(OcrTextItem/SkillEntry/SkillReadResultの生成元)・MainWindow(438-441行目、`Name`/`Lv`どちらかnullなSkillEntryは`CharmSkill`変換前にフィルタして除外する設計を確認)。データ型定義のみでロジックなし |
| TextOcrReader.cs | 済(2026-07-12、0件) | SkillReadingPipeline(RecognizeAsync/RecognizeBytesAsyncの両方を消費、フル画像OCR+バリエーション毎のMat直接OCR)。CLAUDE.md記載の仕様(BitmapDecoder.GetSoftwareBitmapAsyncでの直接デコード、Convert方式回避によるJPEGアルファ破損対策)と実装が一致。RecognizeBytesAsyncはOpenCVのBGR→BGRA変換後のバイト列を直接コピーする方式でJPEGデコードを経由しないため同アルファ問題の対象外。観察点(据え置き): `CreateEngine`が呼び出しごとに`OcrEngine.TryCreateFromLanguage`を再実行しキャッシュしない(1画像あたりフルOCR1回+バリエーション5回=計6回)。WinRT OCRエンジン生成は軽量なため実害は小さいと判断し優先度低 |
| DuplicateChecker.cs | 済(2026-06-25) | CharmModel(スキル・スロット比較)・DuplicateCheckWindow(消費者) |
| RarityInference.cs | 済(2026-06-25) | skill-groups.json/charm-combinations.json・CharmEditWindow・MainWindow(CSV一括推定) |
| CharmTypeLoader.cs | 済(2026-06-25) | charm-types.json・SkillReadingPipeline(武器スロット有無判定) |

## 中優先（データモデル・UI本体）

| ファイル | 状態 | 相互作用チェック相手 |
|---|---|---|
| CharmModel.cs | 済(2026-07-12、0件) | DuplicateChecker・CharmCsvConverter・RarityInference・MainWindow・CharmEditWindow(共通データモデル)。`Skills`/`ArmorSlots`/`WeaponSlots`は`init`のみで可変`List<T>`だが、消費側(`DuplicateChecker.SortDesc`)が`new List<int>(slots)`で防御的コピーしてからソートしておりエイリアシングによる原本破壊は無いことを確認。観察点(据え置き): `GameVersion.Ascendance`は現状どの生成経路(スクショ読取・CSV取込・手動入力)でも明示的に設定されず常定値`Wilds`のまま(JSON往復のみ対応)。将来のアセンダンス拡張(2027年予定)向けの先行スキャフォールドと判断、現時点では未使用でも問題なし |
| CharmCsvConverter.cs | 済(2026-06-25) | CharmModel・MainWindow(インポート/エクスポート) |
| MainWindow.xaml.cs | 済(2026-06-25、一部)+変更 | 全モジュールのオーケストレーション。920行中、06-25修正は一部箇所のみ（Task.Run化・存在チェック等）で全体は未精査。SkillReadingPipeline・SlotIconAnalyzer・CharmCsvConverter・DuplicateChecker・RarityInference・ErrorLogger・GameSkillOrder |
| CharmEditWindow.xaml.cs | 済(2026-06-25) | SkillNameLoader・GameSkillOrder・RarityInference(レアリティ動的更新) |
| DuplicateCheckWindow.xaml.cs | 済(2026-07-12、0件) | DuplicateChecker(消費者、`Check()`が返す`Indices`/`TargetIndex`/`SuperiorIndices`は`charms`リストの0始まり位置)・MainWindow(`CharmItems`をそのまま渡す、DataGridの列ソートは表示のみでコレクション自体の順序を変えないため`charmItems[i]`との対応がずれないことを確認)。`ShowDialog()`によるモーダル表示のため計算後の並行変更リスクも無し |
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
