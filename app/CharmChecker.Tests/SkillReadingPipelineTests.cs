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
    ];

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
