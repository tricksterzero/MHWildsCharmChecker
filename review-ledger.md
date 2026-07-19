# コード精査台帳

実装モジュールを1本ずつ精査するための進行管理台帳（2026-07-12作成）。
[Ragmas 6v6動画解析ツールの台帳](../../Knowledge/Games/Ragmas/tools/6v6-video-analysis/review-ledger.md)の方式を踏襲。

**運用方針**: 2026-06-25に全体コードレビュー+セキュリティレビューを実施し12件修正済みだが、
「どのファイルを見て問題なしだったか」は記録が残っていない。本台帳は同じ反省に立ち、
**精査の実施自体を記録する**（問題なしでも日付を残す）。そのため、2026-06-25の修正コミット
（`2b63369`/`b090f4f`/`b62c0fb`等）で実際に差分が入ったファイルのみ「済」とし、
残りは記録が無いため未扱いとする（再精査しても害はない安全側の倒し方）。

**初回一巡完了（2026-07-12）**: 全25項目（高優先13・中優先8・低優先2・ドキュメント2）の精査を
1セッションで完了。発見は計4件=実バグ2件（`skill-groups.json`のスキル名表記不一致3件による
RARE推定サイレント失敗・スクショフォルダ設定の保存漏れ）+ドキュメント不整合2件（README.mdの
陳腐化・CLAUDE.mdのRARE推定特殊ケース記載漏れ）。観察点として3件を据え置き記録
（`skill-decoration-map.json`にあって`skill-groups.json`に無い8スキルの要調査・
差分読み込みのウォーターマーク方式が失敗ファイルを恒久除外しうる件・ダーク固定の
機構自体はOS追従のままという既知課題の再確認）。相互作用チェック列が今回も実バグ発見の
主な経路になった（単体では正しく見える箇所が、消費先のデータ・状態と食い違っていた）。
今後は「精査後に大きく変わったファイルの差分再精査」が主な運用になる。

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
| SlotIconAnalyzer.cs | 済(2026-07-19、Codex相談、実バグ1件修正+ドキュメント不整合1件修正+観察点3件) | SlotIconTypes(定数)・SlotValidation(判定結果の検証)・SkillReadingPipeline(護石名ベース種別判定の受け渡し)。**実バグ発見・修正**: `ClassifyLevel`(112行目)で列プロファイルが全て0(`peak==0`)の場合、`threshold`も0になり全列が閾値を満たして`n==1`扱いとなり`Unknown`ではなく`Lv1`と誤判定される穴があった。枠検出済み領域のため実データでは起きにくいが安全側で`peak<=0`を先にUnknownとするガードを追加(回帰テスト`ClassifyLevel_UniformCrop_ReturnsUnknown`追加)。**ドキュメント不整合発見・修正**: クラスXMLコメント「legacy/slot-icon-pipeline/pipeline.pyの移植」は不正確だった。git履歴を裏取りしたところ初回移植コミット`93f76dd`はPoCと完全一致(n==1→Unknown、Lv2判定閾値0.55)だったが、後続コミット`2db3c7f`(2026-06-15)で実データに基づきn==1→Lv1を追加・Lv2判定閾値を0.55→0.50に変更しており、legacy側(参照専用・ビルド対象外)は追随していなかった。コメントを実態(移植+実データチューニング)に合わせて修正。**観察点(据え置き)**: (1)`MergeOverlapping`(184行目)はX近接のみでマージしY座標を無視するため理論上は別行のノイズ候補との誤マージがありうる(後段`FilterYCluster`で大半救済、実データでは未発現)。(2)`MergeOverlapping`の連鎖マージは代表矩形の面積次第で推移的にならないケースがある。(3)`FilterYCluster`(206行目)の単連結クラスタリングは橋渡し候補があれば離れた行を誤結合しうる。3件とも現在の探索領域の狭さと実画像実績から実害小と判断、次回大幅改修時の要再検討事項として記録 |
| SlotIconTypes.cs | 済(2026-07-19、Codex相談、0件) | SlotIconAnalyzer(定数の消費)・SlotValidation(ArmorSlotMaxByPositionとの整合)。定数値自体はSlotIconAnalyzer精査時に実データ検証済みのものをそのまま踏襲しており齟齬なし。Codexとの相談でも定数・探索領域・アルゴリズム記述とCLAUDE.mdの一致を再確認、指摘なし |
| SlotValidation.cs | 済(2026-07-19、Codex相談、実バグ1件修正) | SlotIconAnalyzer(判定結果)・MainWindow.ClassifyFrames(武器/防具振り分け後に呼ばれる呼び出し元)。ArmorSlotMaxByPosition=[3,1,0](防具3個目は常に0)が実データと矛盾しないか、3フレーム検出される「栄世の護石」(20260614061441_1.jpg、防具2+武器1の特殊護石)で裏取り。ClassifyFramesがhasWeaponSlot時にframes[0]を武器スロットへ先に振り分けるため、SlotValidationへ渡る時点で防具は実質2枠のみとなり制約と整合することを確認(コード確認+実画像目視)。防具側の貪欲法アルゴリズム(降順ソート後、位置ごとの上限に収まれば採用しvalidIdx++)はCodexとの相談で数学的に反例なしと確認(回帰テスト`ArmorSlot1_Lv3Lv2Lv1_RejectsLv2_KeepsFollowingLv1`追加、`[3,2,1]→[3,1,0]`で棄却後に後続Lv1を拾う経路を固定)。**実バグ発見・修正**: 前回(2026-07-12)の観察点(武器スロットはOrderByDescending未実施)を深掘りしたところ、`Validate([0,0,0],[1,1,0])`が`[1,1,0]`をそのまま通す(「武器スロットは実質1個まで」という制約を未検証)ことが判明。現在の呼び出し元は常に`weaponSlots[0]`のみ埋めるため実害はなかったが、将来複数武器スロット護石が実装された場合・呼び出し元にバグが混入した場合の防御として`WeaponSlotCountMax=1`定数を追加し個数上限を強制(回帰テスト`WeaponSlot_MultipleLv1_OnlyFirstKept`追加)。武器側の非ソート自体は現行Lv1限定では実害なし、将来Lv2/Lv3が許可された場合は要再設計(据え置き) |
| SkillReadingPipeline.cs | 済(2026-07-19、Codex相談、実バグ1件修正+観察点3件) | ImageVariantFactory・LvParser・SkillNameNormalizer・SkillNameLoader・TextOcrReader・SlotIconAnalyzer(護石名の受け渡し)・CharmTypeLoader。**実バグ発見・修正(重要度高)**: `ExtractCharmName`(59-71行目、修正前)がアンカー座標を一切使わず、画像全体で最初に見つかった「〜の護石」を護石名として返していた。左右2パネル(装備中側・BOX側等)が同時に写る画面(「装備変更」「装備の確認・売却」等)で、`FindAnchor`は正しく右パネルを選びスキルも右パネルから正しく読み取るのに、護石名だけ左パネルのものを誤って返す不整合があった。実データ`case2 equip change/20260615054234_1.jpg`(左:秘歴の護石RARE7、右:栄世の護石RARE8)で検証したところ、`Skills=[見切りLv3,属性吸収Lv1]`(右パネル、正)に対し`CharmName=秘歴の護石`(左パネル、誤)を返すことを確認。CLAUDE.mdの「種別判定(武器/防具)」ロジックは護石名ベース(「栄世の護石」なら1つ目が武器スロット)のため、この誤り単体で武器スロット判定が丸ごと外れる実害があった。`ExtractCharmName`にアンカー座標を渡し、`IsCharmPanel`と同じ相対位置範囲(dx:-50〜250,dy:80〜280)内の候補のみを対象とするよう修正(回帰テスト`ReadWithMetadataAsync_DualPanel_PicksCharmNameFromAnchoredPanel`追加、同画像でCharmName=栄世の護石を検証)。**過去データへの影響に注意**: この修正以前に読み取った`charm-lists/`のCSVで、複数パネル画面から読み取った護石は護石名・RARE値・武器スロット種別が誤っている可能性がある。再読み取りを推奨(ユーザーへ2026-07-19報告済み)。**観察点(据え置き、Codex指摘)**: (1)`RunVariantsAndMerge`(161-216行目)は名前・Lvを別バリエーションから独立選択しインデックス順にペアリングするため、片方だけ中間行が欠落すると後続が全てズレて誤ペアになりうる(Y座標近傍対応への変更が望ましいが未実装、現状データでは37/37全問正解)。(2)「検出数最大」を品質基準にしているため誤検出込みのバリエーションが優先されうる、かつ3スキル上限のチェックが無い。(3)`FindAnchor`の3段フォールバックは「完全一致→部分一致→逆算」の順で全段を評価するため、左パネルで完全一致・右パネルで部分一致しかできない場合に左が選ばれ「常に右優先」という意図と矛盾しうる。3点とも次回大幅改修時の要再検討事項として記録 |
| ImageVariantFactory.cs | 済(2026-07-19、Codex相談、0件・観察点1件) | SkillReadingPipeline(5バリエーション生成・幅ガード)。LvXThreshold値(raw:300,trim60:240,otsu:300,trim60_otsu:240,gray:300)がCLAUDE.md記載と一致することを確認。`gray`変数を(3)Otsu二値化と(5)グレースケールのみバリエーションで共有する設計はCodexとの相談で問題なしと確認(Threshold/CvtColorは別Matへの書き込みのみで`gray`自体は変更されない)。`crop.Cols>60`の幅ガードで3バリエーションのみになるケースも、RunVariantsAndMerge側がバリエーション数を固定仮定していないため問題なし(残るLvXThreshold=300バリエーションではLv検出0件になり名前のみ扱いとなるが、誤データを生成するより安全な失敗)。**観察点(据え置き)**: `Create`メソッド(15-58行目)は複数のMatを順次生成し`List<Variant>`に積むが、途中でOpenCV処理が例外を投げた場合、生成済み・リスト追加前のMatがDisposeされずネイティブメモリリークする設計。呼び出し元の`try/finally`は`Create`が正常にリストを返した後にしか機能しないため防御にならない。実画像でOpenCV例外が起きる頻度は低く、呼び出し元(MainWindow)は画像単位でtry/catchして処理継続する設計のため実害は限定的だが、異常画像が多数混じった場合はネイティブメモリ累積のリスクあり。修正には`Create`全体をtry/catchで囲み例外時に確実にDisposeする必要がありコード複雑性が増すため、今回は見送り次回要再検討 |
| LvParser.cs | 済(2026-07-19、Codex相談、実バグ1件修正) | SkillReadingPipeline(184行目、`t.X0 >= v.LvXThreshold`で右側候補のみに適用・null返却時はlvsに追加せずスキップ)。CLAUDE.md記載の文字置換仕様(し→L, l→L, I→1, ー除去)と実装が一致。**実バグ発見・修正**: 前回(2026-07-12)の観察点(OverflowExceptionリスク)を再評価したところ、「画像単位で隔離され実害なし」ではなく「その画像の読み取り結果を丸ごと失う可用性問題」と再評価。さらにCodexとの相談で、`rest.Where(char.IsDigit)`(旧20行目)が文字列中の数字を位置に関係なく全て拾って連結するため、`LV1個2`→`12`・`LV1/2`→`12`のようにOCRが余計な文字を巻き込んだ場合に例外にならず**もっともらしい誤ったLv値をサイレントに生成する**より深刻な問題を発見(オーバーフローと異なり検出不能な誤データ)。加えて`I→1`変換が残り文字列全体に適用されるため`LVSKILL2`のような別ラベル混入時に存在しない数字を生成しうる点、`char.IsDigit`が全角数字を受理するが`int.Parse`は受理せずFormatExceptionになる不一致も判明。数字抽出を`rest.TakeWhile(char.IsAsciiDigit)`(先頭からの連続数字のみ)に変更し、`int.Parse`を`int.TryParse`に変更することで、離れた数字の連結・全角数字混入・オーバーフローの3点をまとめて解消(回帰テスト`LVSKILL2`/`LV2147483648`/`LV１`→null、`LV1個2`/`LV1/2`→1を追加)。値域検証(Lv0や非現実的大値の無効化)はスキルによりLv上限が異なりパーサー側で決め打ちするリスクがあるため見送り。`.Replace("l","L")`は直後の`ToUpperInvariant()`で同じ変換が行われるため実質冗長だが、動作に影響なし(修正不要と判断) |
| SkillNameNormalizer.cs | 済(2026-07-19、Codex相談、実バグ2件修正+設計強化1件) | SkillNameLoader(正解候補121種・HashSet化)・SkillReadingPipeline。**実バグ発見・修正**: (1)濁点フォールバック用`DakutenMap`がカタカナのガ・ザ・ダ・バ・パ行のみでひらがな未対応だった。実在する正規名「飛び込み」に対しOCRが「飛ひ込み」と誤読した場合に救済できない漏れがあったため、ひらがな版を追加(回帰テスト`DakutenFallback_Hiragana`追加)。(2)全角・半角英数字の揺れを吸収していなかった。実データに「ＫＯ術」「回避距離ＵＰ」「体力回復量ＵＰ」「防御力ＤＯＷＮ耐性」等の全角英数字混じりスキル名があり、OCRが半角(`KO術`等)で読み取ると一致しない漏れがあったため、比較前にOCRテキストの半角ASCII可視文字(!〜~)を全角化する`ToFullWidthAscii`を追加(回帰テスト`HalfWidthAlphabet_NormalizedToFullWidth`追加)。**設計強化**: Codexとの相談で、`Normalize`が`knownSkills`の「長さ降順ソート済み」という契約に完全依存しており(呼び出し元`SkillNameLoader.Load`がソート済みリストを渡すことが前提)、この契約がテストで直接検証されていない点を指摘された。実データに包含関係が6件存在(`攻撃`⊂`火属性攻撃強化`等5件、`防御`⊂`防御力ＤＯＷＮ耐性`)することを確認、現在の唯一の呼び出し元は正しくソートしているため実害はなかったが、将来別の呼び出し元が未ソートリストを渡した場合にサイレントに短い誤一致を返すリスクがあったため、`Normalize`内部で明示的に長さ降順ソートし直すよう変更し契約をメソッド自体で保証する設計に強化(回帰テスト`PartialMatch_UnsortedInput_LongerNameStillPreferred`追加、未ソート入力での長い名前優先を直接固定)。特殊・分解形式の濁点(ヴ等)は現在の121種に該当なしのため見送り(観察点) |
| SkillNameLoader.cs | 済(2026-07-19、Codex相談、設計強化1件) | SkillNameNormalizer(正解候補121種の供給元)・MainWindow(`LoadFromEmbeddedResource`でOCRパイプラインへ供給)。前回(2026-07-12)の実バグ発見・修正: 相互作用チェックで`skill-decoration-map.json`(正規名121種)と`RarityInference`が使う`skill-groups.json`(113種)を突き合わせたところ3件で表記が食い違っていた(防御力ＤＯＷＮ耐性/貫通弾・竜の矢強化/災禍転福 vs 半角DOWN/一矢/転覆)。`skill-name-checklist.md`/`deco_checklist.txt`で正規側が正しいと裏取りの上、`resources/skill-groups.json`の3件を正規名に修正済み。今回のCodex相談で実データ(`decorations`361件・スキル534件出現・`extra_skills`4件)を検査したが空文字/null/重複/前後空白は全て0件で現状データは正常と確認。**設計強化**: `ParseJson`(旧29-53行目)がJSONキーの空文字列・空白のみ・null(`extra_skills`側)を無検証で受理する設計だった。空のスキル名が登録されると下流の部分一致(`SkillNameNormalizer`)で`text.Contains("")`が常に真になり任意のOCR文字列を誤って空の正規名に変換しうる、`extra_skills`の`null`要素は`GetString()!`の`!`がコンパイラ警告を抑えるだけで実行時はNullReferenceExceptionになる、`extra_skills`プロパティ自体の欠落も無検証で117種のみ返してしまう、という3点のfail-fast不足をCodexに指摘され、`AddSkillName`ヘルパーで空文字/空白/null検出時に`InvalidOperationException`を投げるよう変更、`extra_skills`欠落時も例外化(回帰テスト3件追加: `Load_EmptyDecorationSkillName_Throws`/`Load_NullExtraSkill_Throws`/`Load_MissingExtraSkillsProperty_Throws`)。現状データは正常なので実害はないが、将来の手動編集ミス(今回の3件表記不一致のような)を早期検出する防御。**観察点(要調査・据え置き、前回から継続)**: decoration-mapにあってskill-groups.jsonに無いスキルが8件(飛び込み・緩衝・ジャンプ鉄人・昆虫標本の達人・クライマー・オトモへの采配・閃光強化・ハンター生活)。護石に実際つくスキルかどうかユーザー未確認のため、RarityInference.cs精査(次回以降)で扱う |
| SkillOcrTypes.cs | 済(2026-07-19、Codex相談、0件) | SkillReadingPipeline(OcrTextItem/SkillEntry/SkillReadResultの生成元)・MainWindow(438-441行目、`Name`/`Lv`どちらかnullなSkillEntryは`CharmSkill`変換前にフィルタして除外する設計を確認)。データ型定義のみでロジックなし。Codexとの相談でstruct/class選択・nullable設計を再確認、`OcrTextItem`/`SkillEntry`をreadonly record structにしている点(小型データの大量生成に適切)・`SkillEntry`のnullable設計(名前/Lv部分欠落表現)は妥当と確認。**観察点(据え置き、潜在的)**: `SkillReadResult`(record、`IReadOnlyList<SkillEntry>`を保持)の自動生成Equalsはリスト内容ではなく参照を比較するため、同じ要素を持つ別インスタンスは等価にならない。現状は戻り値DTOとしてのみ使われ等価比較・HashSet・Dictionaryキー用途が無いため実害なし。将来値オブジェクトとして比較する用途が生じた場合は`ImmutableArray<SkillEntry>`化等が必要 |
| TextOcrReader.cs | 済(2026-07-12、0件) | SkillReadingPipeline(RecognizeAsync/RecognizeBytesAsyncの両方を消費、フル画像OCR+バリエーション毎のMat直接OCR)。CLAUDE.md記載の仕様(BitmapDecoder.GetSoftwareBitmapAsyncでの直接デコード、Convert方式回避によるJPEGアルファ破損対策)と実装が一致。RecognizeBytesAsyncはOpenCVのBGR→BGRA変換後のバイト列を直接コピーする方式でJPEGデコードを経由しないため同アルファ問題の対象外。観察点(据え置き): `CreateEngine`が呼び出しごとに`OcrEngine.TryCreateFromLanguage`を再実行しキャッシュしない(1画像あたりフルOCR1回+バリエーション5回=計6回)。WinRT OCRエンジン生成は軽量なため実害は小さいと判断し優先度低 |
| DuplicateChecker.cs | 済(2026-06-25) | CharmModel(スキル・スロット比較)・DuplicateCheckWindow(消費者) |
| RarityInference.cs | 済(2026-06-25) | skill-groups.json/charm-combinations.json・CharmEditWindow・MainWindow(CSV一括推定) |
| CharmTypeLoader.cs | 済(2026-06-25) | charm-types.json・SkillReadingPipeline(武器スロット有無判定) |

## 中優先（データモデル・UI本体）

| ファイル | 状態 | 相互作用チェック相手 |
|---|---|---|
| CharmModel.cs | 済(2026-07-12、0件) | DuplicateChecker・CharmCsvConverter・RarityInference・MainWindow・CharmEditWindow(共通データモデル)。`Skills`/`ArmorSlots`/`WeaponSlots`は`init`のみで可変`List<T>`だが、消費側(`DuplicateChecker.SortDesc`)が`new List<int>(slots)`で防御的コピーしてからソートしておりエイリアシングによる原本破壊は無いことを確認。観察点(据え置き): `GameVersion.Ascendance`は現状どの生成経路(スクショ読取・CSV取込・手動入力)でも明示的に設定されず常定値`Wilds`のまま(JSON往復のみ対応)。将来のアセンダンス拡張(2027年予定)向けの先行スキャフォールドと判断、現時点では未使用でも問題なし |
| CharmCsvConverter.cs | 済(2026-06-25) | CharmModel・MainWindow(インポート/エクスポート) |
| MainWindow.xaml.cs | 済(2026-07-12、全920行精査完了、1件修正+1件観察点) | 全モジュールのオーケストレーション(SkillReadingPipeline・SlotIconAnalyzer・CharmCsvConverter・DuplicateChecker・RarityInference・CharmTypeLoader・ErrorLogger・GameSkillOrder・SettingsWindow・CharmEditWindow・DuplicateCheckWindow全てと接続する中枢)。SettingsWindow相互作用チェックで`_screenshotFolder`同期漏れを発見・修正済み(詳細は当該行参照)。AboutMenuの表示ライブラリバージョン(OpenCvSharp4 4.13.0.20260602/WPF-UI 4.3.0)はcsprojの実バージョンと一致確認済み。**観察点(ユーザー判断で見送り、2026-07-12)**: `StartReading_Click`(362-375行目)の差分読み込みは「既存護石(Source=Screenshot)の最新SourceTimestampより新しいファイルのみ対象」というウォーターマーク方式。(1)あるファイルの読み取りが失敗しても、同バッチ内でそれより後のタイムスタンプのファイルが1つでも成功すると、次回スキャンではウォーターマークがそこを追い越しており当該ファイルは永久に対象外になる。(2)`validSkills.Count == 0`(443行目、護石パネル自体は検出できたがスキル名/Lvが1件も読み取れなかったケース)は`failed`カウントにも`ErrorLogger`にも記録されず`continue`で無言スキップされるため、ユーザーは「対象外スクショだった」のか「護石だが読み取り失敗」なのか区別できない。実運用での発生頻度が不明なため今回は修正見送り、次回以降の実害有無を見て判断 |
| CharmEditWindow.xaml.cs | 済(2026-06-25) | SkillNameLoader・GameSkillOrder・RarityInference(レアリティ動的更新) |
| DuplicateCheckWindow.xaml.cs | 済(2026-07-12、0件) | DuplicateChecker(消費者、`Check()`が返す`Indices`/`TargetIndex`/`SuperiorIndices`は`charms`リストの0始まり位置)・MainWindow(`CharmItems`をそのまま渡す、DataGridの列ソートは表示のみでコレクション自体の順序を変えないため`charmItems[i]`との対応がずれないことを確認)。`ShowDialog()`によるモーダル表示のため計算後の並行変更リスクも無し |
| SettingsWindow.xaml.cs | 済(2026-07-12、1件・MainWindow側で修正) | MainWindow(設定永続化: ウィンドウサイズ・位置・スクショフォルダ)。SettingsWindow.xaml.cs自体は問題なし。**実バグ発見・修正**: MainWindowが`ScreenshotFolderPath.Text`(スクショ読み取りタブの実際の値)とは別に`_screenshotFolder`という影のフィールドを持ち、`SaveSettings()`は`_screenshotFolder`の方を永続化していた。ところがタブ自身の「参照...」ボタン(`BrowseScreenshotFolder_Click`)は`ScreenshotFolderPath.Text`だけを更新し`_screenshotFolder`は更新しないため、設定ダイアログ経由ではなくタブから直接フォルダを変更した場合、アプリ終了時にその変更が保存されず次回起動時に消えていた。`_screenshotFolder`フィールドを廃止し`ScreenshotFolderPath.Text`を単一の情報源に統一(読み込み/保存/初期ディレクトリ/設定ダイアログ受け渡しの4箇所を修正)。ビルド+全97件のテスト成功を確認。**制約**: この修正はWPFコードビハインドの状態遷移に関するもので、`CharmChecker.Tests`はCoreロジックのみが対象のためUIレベルの自動テストは無い。実機での動作確認(タブで参照→再起動→復元)は未実施 |
| ErrorLogger.cs | 済(2026-06-16新規実装、2026-06-25呼び出し側修正) | MainWindow各所（例外通知・error.log出力） |
| GameSkillOrder.cs | 済(2026-06-25) | skill-order.json・CharmEditWindow(ComboBox表示順)・SkillNameLoader(正規名との整合) |

## 低優先（ボイラープレート）

| ファイル | 状態 | 相互作用チェック相手 |
|---|---|---|
| App.xaml.cs | 済(2026-07-12、0件・既知課題を再確認) | MainWindow(`SystemThemeWatcher.Watch(this)`)。`ApplicationThemeManager.ApplySystemTheme()`+`SystemThemeWatcher`はOSのライト/ダーク設定に追従する実装のままである一方、MainWindow.xaml等のカスタムカラー(`PanelBackground`等)はダーク専用の固定`SolidColorBrush`(`#FF2D2D2D`等)。ライトモード環境ではwpfui標準コントロールだけライトへ切り替わりカスタムパネルはダークのまま残る、という混在崩れが理論上発生しうる。**新規発見ではなく、26日前のメモリ(project-wpfui-theme)で既に「ダーク固定が意図だが機構は残っており将来のライトテーマ対応タスク」として認識・先送り済みの既知課題であることをコード上で再確認**。CLAUDE.mdの「テーマ: ダーク固定」という記述はやや言い切りすぎで、実装は「ダーク前提だがOS追従の機構は無効化されていない」という実態の方が正確。対応は既にユーザー判断で見送り済みのため今回も修正せず据え置き |
| AssemblyInfo.cs | 済(2026-07-12、0件) | WPFテンプレート標準のThemeInfo属性のみ、ロジックなし |

## ドキュメント精査

| ドキュメント | 状態 | 発見 |
|---|---|---|
| README.md | 済(2026-07-12、1件) | 2026-06-14時点の初期版のまま（「今後C#移行予定」という将来形の記述、Node.js Vision前提）で、CLAUDE.mdに定めた正式な9項目構成（動作環境・使い方・既知の制約・CSV互換・ビルド方法・免責・ライセンス等）に未更新。C#実装は完了済みのため記述と実態が乖離 |
| CLAUDE.md | 済(2026-07-12、1件・修正済み) | 「RARE推定ロジック」節に、`RarityInference.cs`35-36行目の特殊ケース(武器スロットが1つでもあればRARE8固定)の記載が漏れていた(研鑽→RARE5固定の特殊ケースのみ記載)。追記して修正。他の節(スロットアイコン判定・OCRロジック・スキル読み取り・護石優劣判定・フォルダ構成の`resources/`一覧)は実ファイル・実装と突き合わせて相違なし |

## 対象外（本チェッカーの対象外・参照専用）

- `legacy/` — 旧Node.js + Claude Vision実装一式。C#移植のロジック参照元として保持するのみで現行動作パスではない
- `resources/*.json` — データファイル自体はコードではなくデータ照合の対象（[[project-charm-checker-license-checklist]]で別途照合済み）
