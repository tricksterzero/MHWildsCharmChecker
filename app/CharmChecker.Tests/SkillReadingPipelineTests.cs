using CharmChecker.Core.Skill;

namespace CharmChecker.Tests;

public class SkillReadingPipelineTests
{
    private static string FindResourcesDir()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "resources");
            if (Directory.Exists(candidate))
                return candidate;
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException("resources フォルダが見つかりません。");
    }

    private static string FindAssetsDir()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "assets");
            if (Directory.Exists(candidate))
                return candidate;
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException("assets フォルダが見つかりません(ローカル検証用画像が未配置の可能性)。");
    }

    public static IEnumerable<object[]> TestCases =>
    [
        // case1: クエスト報酬鑑定結果
        ["case1 quest result", "20260605112617_1.jpg", new[] { ("弾導強化", 3), ("巧撃", 1) }],
        ["case1 quest result", "20260612192419_1.jpg", new[] { ("砲術", 2), ("抜刀術【技】", 1), ("広域化", 4) }],
        // case2: 装備変更
        ["case2 equip change", "20260615054234_1.jpg", new[] { ("見切り", 3), ("属性吸収", 1) }],
        ["case2 equip change", "20260615054257_1.jpg", new[] { ("見切り", 1), ("渾身", 2) }],
        ["case2 equip change", "20260615054304_1.jpg", new[] { ("見切り", 3), ("スタミナ急速回復", 1), ("火耐性", 1) }],
        // case3: 装備の確認・売却
        ["case3 equip check", "20260615054415_1.jpg", new[] { ("攻撃", 2), ("挑戦者", 1) }],
        ["case3 equip check", "20260615054420_1.jpg", new[] { ("攻めの守勢", 1), ("渾身", 1), ("逆襲", 1) }],
        ["case3 equip check", "20260615054427_1.jpg", new[] { ("ガード性能", 3), ("裂傷耐性", 3) }],
        // case4: マカ錬金 素材選択
        ["case4 craft select", "20260615054539_1.jpg", new[] { ("剛刃研磨", 2), ("ファーストショット", 1), ("笛吹き名人", 1) }],
        ["case4 craft select", "20260615054543_1.jpg", new[] { ("ファーストショット", 2), ("見切り", 1), ("剛刃研磨", 1) }],
        ["case4 craft select", "20260615054556_1.jpg", new[] { ("見切り", 1), ("集中", 1), ("防御", 4) }],
        ["case4 craft select", "20260615054603_1.jpg", new[] { ("氷属性攻撃強化", 1), ("力の解放", 1) }],
        // case5: マカ錬金 鑑定結果
        ["case5 craft result", "20260615054652_1.jpg", new[] { ("攻撃", 1), ("龍属性攻撃強化", 1), ("巧撃", 1) }],
        ["case5 craft result", "20260615054656_1.jpg", new[] { ("スタミナ奪取", 3), ("巧撃", 1) }],
        ["case5 craft result", "20260615054704_1.jpg", new[] { ("攻撃", 1), ("雷耐性", 3) }],
        // 「火事場力」の「力」がカタカナ「カ」に誤認識され2つ目のスキルが読み取れなかった回帰テスト
        ["case5 craft result", "20260704160048_1.jpg", new[] { ("笛吹き名人", 2), ("火事場力", 3) }],
        // case6: 鑑定BOX。行間隔が行内の名前-Lv間オフセットの2倍未満のため、Y最近傍の
        // 貪欲マッチングだと2つ目の名前が1つ目のLvに交差マッチしてLvが入れ替わっていた回帰テスト
        // （正: 攻めの守勢Lv3,挑戦者Lv1 / 修正前の誤: 攻めの守勢Lv1,挑戦者Lv3）
        ["case6 appraisal box", "20250906064316_1.jpg", new[] { ("攻めの守勢", 3), ("挑戦者", 1) }],
        // option 21_9: ウルトラワイド(21:9)設定でのキャプチャ。上下に黒帯が入り、ゲーム内UIは
        // 右上コーナー基準・コンテンツ高さ/1440の比率で縮小配置される。CropSkillAreaのscale補正
        // (LetterboxNormalizer.DetectContentBounds)で対応(2026-07-24)。
        ["option 21_9", "20260615094726_1.jpg", new[] { ("貫通弾・竜の矢強化", 1), ("耳栓", 1), ("早食い", 1) }],
        ["option 21_9", "20260615094741_1.jpg", new[] { ("水属性攻撃強化", 1), ("広域化", 4) }],
        ["option 21_9", "20260615094750_1.jpg", new[] { ("フォースショット", 1), ("爆破やられ耐性", 3) }],
        // option 21_9 native: 真の21:9モニタ(3440x1440ネイティブ、黒帯なし)実機キャプチャ。
        // フルスクリーンOCRを生画像のまま行うと「装備詳細」等の複数文字テキストが1行として
        // 認識されないケースが多発する問題があり、基準幅(2560)超過時はOCR専用に基準幅相当へ
        // 縮小したコピーを使うことで解消(2026-07-26、ReadWithMetadataAsync)。
        ["option 21_9 native", "20260726181601_1.jpg", new[] { ("攻撃", 3), ("属性変換", 1) }],
        // 「匠」(1文字名、孤立名回復ロジック対象)が真の21:9でも回復できることの確認
        ["option 21_9 native", "20260726182924_1.jpg", new[] { ("匠", 3), ("広域化", 4) }],
        ["option 21_9 native", "20260726181828_1.jpg", new[] { ("攻撃", 1), ("広域化", 3), ("納刀術", 2) }],
        ["option 21_9 native", "20260726183121_1.jpg", new[] { ("攻撃", 2), ("破壊王", 1), ("回避距離ＵＰ", 1) }],
        // option 21_9 native quest result: 真の21:9での狩猟後護石入手(鑑定BOX)画面。
        // 「装備変更」の2パネル比較とは異なる単一パネルレイアウトだが、右端の「装備詳細」
        // パネル位置は右上基準で共通のため、既存のDetailPanelRegionがそのまま機能する。
        ["option 21_9 native quest result", "202A601.JPG", new[] { ("貫通弾・竜の矢強化", 1), ("雷耐性", 2), ("環境利用の知識", 1) }],
        ["option 21_9 native quest result", "20BC111.JPG", new[] { ("見切り", 3), ("満足感", 3) }],
        // option 21_9 native craft result: 真の21:9でのマカ錬金(お守りの錬金術)鑑定結果画面。
        ["option 21_9 native craft result", "202FCE1.JPG", new[] { ("ＫＯ術", 2), ("心眼", 1), ("弱点特効", 1) }],
        ["option 21_9 native craft result", "208B0D1.JPG", new[] { ("笛吹き名人", 2), ("水属性攻撃強化", 1), ("巧撃", 1) }],
        // フルスクリーンOCR(幅2560)ではアンカー未検出だった画像(2026-07-26発見)。原因は
        // OCR対象領域の幅に依存する閾値的な現象(中央パネルの内容をマスクしても結果は不変で、
        // 幅760pxで失敗・710pxで成功する境界を確認)。FindAnchorWithFallbackAsyncの
        // 右端フォールバック(1200/900/700px)で解消したことの回帰テスト。
        ["option 21_9 native craft result", "205B741.JPG", new[] { ("爆破属性強化", 1), ("防御力ＤＯＷＮ耐性", 3) }],
    ];

    [Fact]
    public void PairByNearestY_LeadingNameMissing_DoesNotShiftRemainingLevels()
    {
        // Codexとの相談で発見された実バグの回帰テスト。名前-Lv間の行内オフセット(実データで
        // 31~48px、平均約37px)が行間隔(約80px)の半分を超える画像で、先頭の名前が検出漏れした
        // 場合、生のY座標差をコストにすると「1行分ずれた誤ペアリング」の方が正しいペアリングより
        // 総コストが低くなり、GapCostをどう調整しても直せない誤りが起きていた
        // (詳細はAssumedRowOffsetのXMLコメント参照)。
        // 名前Y=[120,200](2件目・3件目相当)、Lv Y=[88,168,248](3件分、1件目=88が実際の1件目の
        // Lv)という、先頭スキルの名前だけが検出漏れした状況を再現する。
        var names = new List<(double Y, string SkillName)> { (120, "回避性能"), (200, "満足感") };
        var lvs = new List<(double Y, int Lv)> { (88, 3), (168, 2), (248, 1) };

        var result = SkillReadingPipeline.PairByNearestY(names, lvs);
        result.Sort((a, b) => a.Y.CompareTo(b.Y));

        Assert.Equal(3, result.Count);
        Assert.Null(result[0].Entry.Name);
        Assert.Equal(3, result[0].Entry.Lv);
        Assert.Equal("回避性能", result[1].Entry.Name);
        Assert.Equal(2, result[1].Entry.Lv);
        Assert.Equal("満足感", result[2].Entry.Name);
        Assert.Equal(1, result[2].Entry.Lv);
    }

    [Fact]
    public async Task ReadAsync_SingleCharacterSkillName_IsRecoveredWithoutShiftingOtherSkillsLevels()
    {
        // OCRエンジンが「匠」（1文字のスキル名）を単独では一切検出できないケース
        // （Windows.Media.Ocrは同一行に他のテキストが無い孤立した1文字を行として検出しない仕様）。
        // 名前とLvを単純なインデックス順でペアリングすると、1つ目のスキル名が
        // 丸ごと欠落した影響で後続の全スキルのLvがずれてしまう問題があったため、
        // Y座標最近傍ペアリングで欠落自体は解消済み。さらに、名前が無いLv行(孤児行)については
        // 既に判明している別の行のテキストをコンパニオンとして横に連結し再OCRすることで、
        // 「匠」単独では検出されない文字を回復させる（正: 匠Lv3, 回避性能Lv2, 満足感Lv1）。
        var assetsDir = FindAssetsDir();
        var resourcesDir = FindResourcesDir();
        var jsonPath = Path.Combine(resourcesDir, "skill-decoration-map.json");
        var imagePath = Path.Combine(assetsDir, "20260614061441_1.jpg");

        if (!File.Exists(imagePath))
        {
            return;
        }

        var knownSkills = SkillNameLoader.Load(jsonPath);
        var result = await SkillReadingPipeline.ReadAsync(imagePath, knownSkills);

        Assert.NotNull(result);
        Assert.Equal(3, result.Count);
        Assert.Equal("匠", result[0].Name);
        Assert.Equal(3, result[0].Lv);
        Assert.Equal("回避性能", result[1].Name);
        Assert.Equal(2, result[1].Lv);
        Assert.Equal("満足感", result[2].Name);
        Assert.Equal(1, result[2].Lv);
    }

    [Fact]
    public async Task ReadWithMetadataAsync_DualPanel_PicksCharmNameFromAnchoredPanel()
    {
        // 左パネル(秘歴の護石)・右パネル(栄世の護石)が同時に写る装備変更画面。
        // FindAnchorは右パネルを選ぶため、護石名も右パネル(栄世の護石)を返すべき。
        var assetsDir = FindAssetsDir();
        var resourcesDir = FindResourcesDir();
        var jsonPath = Path.Combine(resourcesDir, "skill-decoration-map.json");
        var imagePath = Path.Combine(assetsDir, "case2 equip change", "20260615054234_1.jpg");

        if (!File.Exists(imagePath))
        {
            return;
        }

        var knownSkills = SkillNameLoader.Load(jsonPath);
        var result = await SkillReadingPipeline.ReadWithMetadataAsync(imagePath, knownSkills);

        Assert.NotNull(result);
        Assert.Equal("栄世の護石", result.CharmName);
    }

    [Fact]
    public async Task ReadWithMetadataAsync_21x9DualPanel_PicksCharmNameFromAnchoredPanel()
    {
        // 21:9(黒帯付き2560x1440)の装備詳細2パネル画面。左パネル=秘歴の護石、右パネル=未解の護石。
        // FindAnchorは生画像のフルOCRで右パネルを選ぶ(検出は拡大しない設計、詳細はCropSkillArea
        // のXMLコメント参照)ため、護石名も右パネル(未解の護石)を返すべき回帰テスト。
        var assetsDir = FindAssetsDir();
        var resourcesDir = FindResourcesDir();
        var jsonPath = Path.Combine(resourcesDir, "skill-decoration-map.json");
        var imagePath = Path.Combine(assetsDir, "option 21_9", "20260615094750_1.jpg");

        if (!File.Exists(imagePath))
        {
            return;
        }

        var knownSkills = SkillNameLoader.Load(jsonPath);
        var result = await SkillReadingPipeline.ReadWithMetadataAsync(imagePath, knownSkills);

        Assert.NotNull(result);
        Assert.Equal("未解の護石", result.CharmName);
    }

    [Fact]
    public void ChooseAnchor_SkillLabelOfSamePanel_KeepsDetailLabelPosition()
    {
        // 同じパネルの逆算候補(実測でX差-36〜+42)は位置が粗いため採用せず、「装備詳細」の位置を使う
        var result = SkillReadingPipeline.ChooseAnchor([(2082, 132)], [(2086 + 113, 134)], 1.0);
        Assert.Equal((2082d, 132d), result);
    }

    [Fact]
    public void ChooseAnchor_SkillLabelExactlyAtSeparateDistance_PicksSkillLabel()
    {
        // 境界値: ちょうど200px右(scale=1)なら別パネルとみなす
        var result = SkillReadingPipeline.ChooseAnchor([(1602, 132)], [(1606, 135), (1802, 134)], 1.0);
        Assert.Equal((1802d, 134d), result);
    }

    [Fact]
    public void ChooseAnchor_SkillLabelJustBelowSeparateDistance_KeepsDetailLabel()
    {
        var result = SkillReadingPipeline.ChooseAnchor([(1602, 132)], [(1801, 134)], 1.0);
        Assert.Equal((1602d, 132d), result);
    }

    [Fact]
    public void ChooseAnchor_SeparateDistanceScalesWithUiScale()
    {
        // 黒帯付き21:9等でUIが縮小される場合は距離も縮小する(scale=0.5なら100px)
        var result = SkillReadingPipeline.ChooseAnchor([(1000, 100)], [(1100, 100)], 0.5);
        Assert.Equal((1100d, 100d), result);
    }

    [Fact]
    public void ChooseAnchor_NoDetailLabel_FallsBackToRightmostSkillLabel()
    {
        var result = SkillReadingPipeline.ChooseAnchor([], [(1606, 135), (2086, 134)], 1.0);
        Assert.Equal((2086d, 134d), result);
    }

    [Fact]
    public void ChooseAnchor_NoCandidates_ReturnsNull()
    {
        Assert.Null(SkillReadingPipeline.ChooseAnchor([], [], 1.0));
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(-1.0)]
    [InlineData(double.NaN)]
    public void ChooseAnchor_NonPositiveScale_Throws(double scale)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => SkillReadingPipeline.ChooseAnchor([(0, 0)], [], scale));
    }

    [Fact]
    public async Task ReadWithMetadataAsync_AllBlackImage_ReturnsNull()
    {
        // 真っ黒な画像ではLetterboxNormalizerの倍率が0になる。アンカー候補が無いので、倍率の検査
        // (ChooseAnchorの事前条件)で例外にせずnullを返すべき回帰テスト(2026-11-07、Steamのスクショで発見)
        var resourcesDir = FindResourcesDir();
        var knownSkills = SkillNameLoader.Load(Path.Combine(resourcesDir, "skill-decoration-map.json"));
        var tempPath = Path.Combine(Path.GetTempPath(), $"charmchecker_black_{Guid.NewGuid():N}.png");
        using (var black = new OpenCvSharp.Mat(new OpenCvSharp.Size(2560, 1440), OpenCvSharp.MatType.CV_8UC3, OpenCvSharp.Scalar.All(0)))
        {
            OpenCvSharp.Cv2.ImWrite(tempPath, black);
        }

        try
        {
            var result = await SkillReadingPipeline.ReadWithMetadataAsync(tempPath, knownSkills);
            Assert.Null(result);
        }
        finally
        {
            File.Delete(tempPath);
        }
    }

    public static IEnumerable<object[]> RightPanelAnchorCases()
    {
        yield return new object[] { "20251202231937_1.jpg", new[] { ("強化持続", 3), ("破壊王", 1), ("ひるみ軽減", 1) } };
        yield return new object[] { "20260516104120_1.jpg", new[] { ("見切り", 3), ("挑戦者", 1) } };
    }

    [Fact]
    public async Task ReadWithMetadataAsync_RightPanelCharmNameAlsoUnreadable_ReturnsNullInsteadOfLeftPanel()
    {
        // 上と同じ状況だが、右パネルの護石名(栄世の護石)も全画面OCRで読めない画像。右パネルが
        // アンカーに選ばれた上でIsCharmPanelが成立せずnull(護石画面ではない扱い)になる。修正前は
        // 左パネル(史伝の護石)のスキルと右パネルのスロットが混ざった結果を返していたため、
        // 左パネルを読まずに除外されることを確かめる(右パネルを読めるようにするのは今後の課題)。
        var assetsDir = FindAssetsDir();
        var resourcesDir = FindResourcesDir();
        var jsonPath = Path.Combine(resourcesDir, "skill-decoration-map.json");
        var imagePath = Path.Combine(assetsDir, "case9 right panel anchor", "20260523212858_1.jpg");

        if (!File.Exists(imagePath))
        {
            return;
        }

        var knownSkills = SkillNameLoader.Load(jsonPath);
        var result = await SkillReadingPipeline.ReadWithMetadataAsync(imagePath, knownSkills);

        Assert.Null(result);
    }

    [Theory]
    [MemberData(nameof(RightPanelAnchorCases))]
    public async Task ReadWithMetadataAsync_RightPanelDetailLabelUnreadable_StillPicksRightPanel(
        string file, (string Name, int Lv)[] expected)
    {
        // 装備変更の2護石比較画面で、右パネルの「装備詳細」が半透明の背景のためOCRで読めず、
        // 左パネルの「装備詳細」だけが見つかるケース(2026-11-07発見)。修正前は「装備詳細」が
        // 1つでも見つかると「装備スキル」からの逆算候補を使わなかったため左パネルが採用され、
        // スキル・護石名は左、スロットは右(固定の探索領域)という2つの護石が混ざった結果になっていた。
        // 右パネルの「装備スキル」は読めているので、それを別パネルの候補として採用し、右パネル
        // (いずれも栄世の護石)を読むべき回帰テスト。
        var assetsDir = FindAssetsDir();
        var resourcesDir = FindResourcesDir();
        var jsonPath = Path.Combine(resourcesDir, "skill-decoration-map.json");
        var imagePath = Path.Combine(assetsDir, "case9 right panel anchor", file);

        if (!File.Exists(imagePath))
        {
            return;
        }

        var knownSkills = SkillNameLoader.Load(jsonPath);
        var result = await SkillReadingPipeline.ReadWithMetadataAsync(imagePath, knownSkills);

        Assert.NotNull(result);
        Assert.Equal("栄世の護石", result.CharmName);
        Assert.Equal(expected.Length, result.Skills.Count);
        for (int i = 0; i < expected.Length; i++)
        {
            Assert.Equal(expected[i].Name, result.Skills[i].Name);
            Assert.Equal(expected[i].Lv, result.Skills[i].Lv);
        }
    }

    [Theory]
    [InlineData("、未解の護石", "未解の護石")]
    [InlineData(")秘歴の護石", "秘歴の護石")]
    [InlineData("栄世の護石", "栄世の護石")]
    [InlineData("x栄世の護石", "栄世の護石")]
    [InlineData("１栄世の護石", "栄世の護石")]
    [InlineData("の護石", "の護石")]
    public void StripLeadingNonJapanese_RemovesGarbagePrefix(string input, string expected)
    {
        // 護石アイコン(装飾グラフィック)がOCRで記号として誤読され、テキスト行の先頭に混入する
        // ケースの回帰テスト(2026-07-24、21:9スクリーンショットの黒帯除去検証で発見)。
        var result = SkillReadingPipeline.StripLeadingNonJapanese(input);
        Assert.Equal(expected, result);
    }

    [Theory]
    [MemberData(nameof(TestCases))]
    public async Task ReadAsync_MatchesExpected(string folder, string file, (string Name, int Lv)[] expected)
    {
        var assetsDir = FindAssetsDir();
        var resourcesDir = FindResourcesDir();
        var imagePath = Path.Combine(assetsDir, folder, file);
        var jsonPath = Path.Combine(resourcesDir, "skill-decoration-map.json");

        if (!File.Exists(imagePath))
        {
            // ローカル画像がない環境ではスキップ
            return;
        }

        var knownSkills = SkillNameLoader.Load(jsonPath);
        var result = await SkillReadingPipeline.ReadAsync(imagePath, knownSkills);

        Assert.NotNull(result);
        Assert.Equal(expected.Length, result.Count);

        for (int i = 0; i < expected.Length; i++)
        {
            Assert.Equal(expected[i].Name, result[i].Name);
            Assert.Equal(expected[i].Lv, result[i].Lv);
        }
    }
}
