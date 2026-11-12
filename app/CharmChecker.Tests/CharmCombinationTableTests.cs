using CharmChecker.Core.Model;

namespace CharmChecker.Tests;

/// <summary>
/// 構成表(skill-groups.json・charm-combinations.json)の読み込み時の検査、構成表の実データが満たすべき性質、
/// 護石との照合(<see cref="CharmCombinationTable"/>)のテスト。
/// </summary>
public class CharmCombinationTableTests
{
    private static string Combo(string rarity = "7", string groups = "[3,7]", string slots = """[{"armor":[1],"weapon":[]}]""") =>
        $$"""[{"rarity":{{rarity}},"skillGroups":{{groups}},"slots":{{slots}}}]""";

    [Fact]
    public void ParseCombinations_ValidJson_Parses()
    {
        var result = CharmCombinationTable.ParseCombinations(Combo());
        Assert.Single(result);
        Assert.Equal(7, result[0].Rarity);
    }

    [Theory]
    [InlineData("4")] // 境界値: RAREは5〜8
    [InlineData("9")]
    public void ParseCombinations_RarityOutOfRange_Throws(string rarity)
    {
        Assert.Throws<InvalidOperationException>(() => CharmCombinationTable.ParseCombinations(Combo(rarity: rarity)));
    }

    [Theory]
    [InlineData("5")]
    [InlineData("8")]
    public void ParseCombinations_RarityBoundary_Accepted(string rarity)
    {
        Assert.Single(CharmCombinationTable.ParseCombinations(Combo(rarity: rarity)));
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("[1,2,3,4]")]
    public void ParseCombinations_SkillGroupCountOutOfRange_Throws(string groups)
    {
        Assert.Throws<InvalidOperationException>(() => CharmCombinationTable.ParseCombinations(Combo(groups: groups)));
    }

    [Theory]
    [InlineData("[]")] // スロットのパターンがない
    [InlineData("""[{"armor":[4],"weapon":[]}]""")] // Lvが3を超える
    [InlineData("""[{"armor":[-1],"weapon":[]}]""")]
    [InlineData("""[{"armor":[1,1,1,1],"weapon":[]}]""")] // 4個
    public void ParseCombinations_InvalidSlots_Throws(string slots)
    {
        Assert.Throws<InvalidOperationException>(() => CharmCombinationTable.ParseCombinations(Combo(slots: slots)));
    }

    [Fact]
    public void ParseCombinations_WeightOnlyOnSomeSlots_Throws()
    {
        var slots = """[{"armor":[1],"weapon":[1],"weight":0.5},{"armor":[1,1],"weapon":[1]}]""";
        Assert.Throws<InvalidOperationException>(() => CharmCombinationTable.ParseCombinations(Combo(rarity: "8", slots: slots)));
    }

    [Theory]
    [InlineData(0.5, 0.48)] // 合計0.98: 誤差0.01を超える
    [InlineData(0.5, 0.52)]
    [InlineData(1.0, 0.0)]  // 正でない重み
    public void ParseCombinations_InvalidWeights_Throws(double w1, double w2)
    {
        var slots = $$"""[{"armor":[1],"weapon":[1],"weight":{{w1}}},{"armor":[1,1],"weapon":[1],"weight":{{w2}}}]""";
        Assert.Throws<InvalidOperationException>(() => CharmCombinationTable.ParseCombinations(Combo(rarity: "8", slots: slots)));
    }

    [Fact]
    public void ParseCombinations_WeightsSumWithinTolerance_Accepted()
    {
        // 実データは0.5/0.33/0.17(合計1.00)。誤差0.01までは認める
        var slots = """[{"armor":[1],"weapon":[1],"weight":0.5},{"armor":[1,1],"weapon":[1],"weight":0.33},{"armor":[2],"weapon":[1],"weight":0.175}]""";
        Assert.Single(CharmCombinationTable.ParseCombinations(Combo(rarity: "8", slots: slots)));
    }

    [Fact]
    public void RealSkills_NeverMatchTwoDifferentSkillGroupPatterns()
    {
        // 構成確率(CharmProbabilityEstimator)と理論値判定(CharmTheoreticalValueChecker)は、スキルに合う
        // 組み合わせをFirstOrDefaultで1つだけ選ぶ。これは「実在するスキルの組(名前が異なる2〜3個)が、
        // 構成表の異なる2つのスキルグループのパターンに同時に当てはまることはない」ことが前提のため、
        // 複数のグループに属するスキル(実データで20件)も含めて実データで確かめる(2026-11-08)。
        // パターンPとQの両方に当てはまるスキルの組があるのは、Qの並べ替えσのどれかで、各位置iについて
        // グループP[i]とQ[σ(i)]の両方に属するスキルを、名前が重ならないように選べるときに限る。
        var skillGroups = CharmCombinationTable.LoadSkillGroups();
        var patterns = CharmCombinationTable.LoadCombinations()
            .Select(c => c.SkillGroups.OrderBy(g => g).ToArray())
            .DistinctBy(p => string.Join(",", p))
            .ToList();

        foreach (var p in patterns)
        foreach (var q in patterns)
        {
            if (p.Length != q.Length || p.SequenceEqual(q)) continue;
            foreach (var sigma in Permutations(q.Length))
            {
                var candidates = Enumerable.Range(0, p.Length)
                    .Select(i => skillGroups.Where(e => e.Groups.Contains(p[i]) && e.Groups.Contains(q[sigma[i]])).ToList())
                    .ToList();
                var chosen = FindDistinctNames(candidates, 0, []);
                Assert.True(chosen is null,
                    $"[{string.Join(",", p)}] と [{string.Join(",", q)}] の両方に当てはまるスキルの組: " +
                    string.Join(", ", chosen ?? []));
            }
        }
    }

    private static List<string>? FindDistinctNames(List<List<SkillGroupEntry>> candidates, int index, List<SkillGroupEntry> picked)
    {
        if (index == candidates.Count)
            return picked.Select(e => $"{e.Name}Lv{e.Level}").ToList();
        foreach (var entry in candidates[index])
        {
            if (picked.Any(e => e.Name == entry.Name)) continue;
            var result = FindDistinctNames(candidates, index + 1, [.. picked, entry]);
            if (result is not null) return result;
        }
        return null;
    }

    private static IEnumerable<int[]> Permutations(int n)
    {
        if (n == 0)
        {
            yield return [];
            yield break;
        }
        foreach (var rest in Permutations(n - 1))
            for (int pos = 0; pos <= rest.Length; pos++)
                yield return [.. rest[..pos], n - 1, .. rest[pos..]];
    }

    // 以下は2026-11-12にRarityInferenceTestsから移したテスト(内容は変えていない)

    [Fact]
    public void OrderIndependentMatching_NoSameRarityAmbiguityAcrossRealData()
    {
        // 順序非依存化によって、同一レアリティ内で複数の異なる組み合わせパターンに
        // 同時マッチしうる護石が新たに生まれていないかを実データ全件で検証する
        // (Codexとの相談で事前に確認済みだが、コードでも固定する)。
        // comboの宣言パターン自体を「各位置がその値のみ許容される仮想スキル」とみなし、
        // 同一(Rarity,長さ)の他comboパターンと曖昧一致しないことを確認する
        var combinations = CharmCombinationTable.LoadCombinations();
        foreach (var group in combinations.GroupBy(c => (c.Rarity, Length: c.SkillGroups.Length)))
        {
            var list = group.ToList();
            for (int i = 0; i < list.Count; i++)
            {
                var possibleGroups = list[i].SkillGroups.Select(g => new[] { g }).ToList();
                for (int j = 0; j < list.Count; j++)
                {
                    if (i == j) continue;
                    Assert.False(
                        CharmCombinationTable.MatchesGroupPattern(possibleGroups, list[j].SkillGroups),
                        $"rarity={group.Key.Rarity} len={group.Key.Length}: " +
                        $"combo[{i}]=[{string.Join(",", list[i].SkillGroups)}] が " +
                        $"combo[{j}]=[{string.Join(",", list[j].SkillGroups)}] と曖昧一致");
                }
            }
        }
    }

    [Theory]
    [InlineData("防御力ＤＯＷＮ耐性", 1)]
    [InlineData("貫通弾・竜の矢強化", 1)]
    [InlineData("災禍転福", 1)]
    public void CanonicalSkillName_ResolvesInSkillGroups(string name, int level)
    {
        // skill-decoration-map.json（正規名）と表記が食い違っていたため
        // RARE推定が静かに失敗していた3件の回帰テスト
        var groups = CharmCombinationTable.LoadSkillGroups();
        Assert.Contains(groups, e => e.Name == name && e.Level == level);
    }

    [Fact]
    public void ResourcesLoadCorrectly()
    {
        var groups = CharmCombinationTable.LoadSkillGroups();
        Assert.True(groups.Count > 200);

        var combos = CharmCombinationTable.LoadCombinations();
        Assert.True(combos.Count > 20);
    }

    [Fact]
    public void ParseSkillGroups_DuplicateNameLevel_Throws()
    {
        var json = """
            [
                { "name": "攻撃", "level": 1, "groups": [1] },
                { "name": "攻撃", "level": 1, "groups": [2] }
            ]
            """;

        Assert.Throws<InvalidOperationException>(() => CharmCombinationTable.ParseSkillGroups(json));
    }

    // MatchingCombinationsのテストは、実データの変更に左右されないよう小さな表で行う
    private static readonly SkillGroupEntry[] TestSkillGroups =
    [
        new("スキルA", 1, [1]),
        new("スキルB", 1, [2]),
    ];

    private static Charm MakeSkillCharm(params (string Name, int Lv)[] skills) => new()
    {
        Skills = skills.Select(s => new CharmSkill(s.Name, s.Lv)).ToList(),
    };

    private static CharmCombination MakeCombination(int rarity, params int[] skillGroups) =>
        new(rarity, skillGroups, [new CharmCombinationSlot([1], [])]);

    [Fact]
    public void MatchingCombinations_ReturnsMatchesInTableOrder_RegardlessOfSkillOrder()
    {
        var rare5 = MakeCombination(5, 1, 2);
        var rare6 = MakeCombination(6, 3);
        var rare7Reversed = MakeCombination(7, 2, 1);
        var rare8 = MakeCombination(8, 1, 2);

        var matched = CharmCombinationTable.MatchingCombinations(
            MakeSkillCharm(("スキルA", 1), ("スキルB", 1)), TestSkillGroups, [rare5, rare6, rare7Reversed, rare8]);

        Assert.Equal(new[] { rare5, rare7Reversed, rare8 }, matched);
    }

    [Fact]
    public void MatchingCombinations_GroupCountDiffers_DoesNotMatch()
    {
        // スキル2つの護石は、スキルグループが1つ・3つの組み合わせには当てはまらない
        var matched = CharmCombinationTable.MatchingCombinations(
            MakeSkillCharm(("スキルA", 1), ("スキルB", 1)), TestSkillGroups,
            [MakeCombination(5, 1), MakeCombination(6, 1, 2, 2)]);

        Assert.Empty(matched);
    }

    [Fact]
    public void MatchingCombinations_SkillLevelNotInSkillGroups_ReturnsEmpty()
    {
        var matched = CharmCombinationTable.MatchingCombinations(
            MakeSkillCharm(("スキルA", 1), ("スキルB", 2)), TestSkillGroups, [MakeCombination(5, 1, 2)]);

        Assert.Empty(matched);
    }

    [Fact]
    public void MatchingCombinations_NoSkills_DoesNotMatchCombinationsWithSkillGroups()
    {
        // 構成表の組み合わせはスキルグループ1〜3個のため、スキルが0個の護石はどれにも当てはまらない
        var matched = CharmCombinationTable.MatchingCombinations(
            MakeSkillCharm(), TestSkillGroups, [MakeCombination(5, 1), MakeCombination(6, 1, 2)]);

        Assert.Empty(matched);
    }

    [Fact]
    public void MatchingCombinations_NullArguments_ThrowBeforeEnumeration()
    {
        // 戻り値を列挙しなくても、呼び出した時点で例外になる
        var charm = MakeSkillCharm(("スキルA", 1));
        CharmCombination[] combinations = [MakeCombination(5, 1)];
        Assert.Throws<ArgumentNullException>(() => CharmCombinationTable.MatchingCombinations(null!, TestSkillGroups, combinations));
        Assert.Throws<ArgumentNullException>(() => CharmCombinationTable.MatchingCombinations(charm, null!, combinations));
        Assert.Throws<ArgumentNullException>(() => CharmCombinationTable.MatchingCombinations(charm, TestSkillGroups, null!));
    }
}
