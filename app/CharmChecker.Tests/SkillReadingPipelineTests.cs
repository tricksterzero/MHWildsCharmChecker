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
