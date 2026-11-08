using CharmChecker.Core.Model;

namespace CharmChecker.Tests;

/// <summary>構成表(charm-combinations.json)の読み込み時の検査と、構成表の実データが満たすべき性質のテスト。</summary>
public class CharmCombinationTableTests
{
    private static string Combo(string rarity = "7", string groups = "[3,7]", string slots = """[{"armor":[1],"weapon":[]}]""") =>
        $$"""[{"rarity":{{rarity}},"skillGroups":{{groups}},"slots":{{slots}}}]""";

    [Fact]
    public void ParseCombinations_ValidJson_Parses()
    {
        var result = RarityInference.ParseCombinations(Combo());
        Assert.Single(result);
        Assert.Equal(7, result[0].Rarity);
    }

    [Theory]
    [InlineData("4")] // 境界値: RAREは5〜8
    [InlineData("9")]
    public void ParseCombinations_RarityOutOfRange_Throws(string rarity)
    {
        Assert.Throws<InvalidOperationException>(() => RarityInference.ParseCombinations(Combo(rarity: rarity)));
    }

    [Theory]
    [InlineData("5")]
    [InlineData("8")]
    public void ParseCombinations_RarityBoundary_Accepted(string rarity)
    {
        Assert.Single(RarityInference.ParseCombinations(Combo(rarity: rarity)));
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("[1,2,3,4]")]
    public void ParseCombinations_SkillGroupCountOutOfRange_Throws(string groups)
    {
        Assert.Throws<InvalidOperationException>(() => RarityInference.ParseCombinations(Combo(groups: groups)));
    }

    [Theory]
    [InlineData("[]")] // スロットのパターンがない
    [InlineData("""[{"armor":[4],"weapon":[]}]""")] // Lvが3を超える
    [InlineData("""[{"armor":[-1],"weapon":[]}]""")]
    [InlineData("""[{"armor":[1,1,1,1],"weapon":[]}]""")] // 4個
    public void ParseCombinations_InvalidSlots_Throws(string slots)
    {
        Assert.Throws<InvalidOperationException>(() => RarityInference.ParseCombinations(Combo(slots: slots)));
    }

    [Fact]
    public void ParseCombinations_WeightOnlyOnSomeSlots_Throws()
    {
        var slots = """[{"armor":[1],"weapon":[1],"weight":0.5},{"armor":[1,1],"weapon":[1]}]""";
        Assert.Throws<InvalidOperationException>(() => RarityInference.ParseCombinations(Combo(rarity: "8", slots: slots)));
    }

    [Theory]
    [InlineData(0.5, 0.48)] // 合計0.98: 誤差0.01を超える
    [InlineData(0.5, 0.52)]
    [InlineData(1.0, 0.0)]  // 正でない重み
    public void ParseCombinations_InvalidWeights_Throws(double w1, double w2)
    {
        var slots = $$"""[{"armor":[1],"weapon":[1],"weight":{{w1}}},{"armor":[1,1],"weapon":[1],"weight":{{w2}}}]""";
        Assert.Throws<InvalidOperationException>(() => RarityInference.ParseCombinations(Combo(rarity: "8", slots: slots)));
    }

    [Fact]
    public void ParseCombinations_WeightsSumWithinTolerance_Accepted()
    {
        // 実データは0.5/0.33/0.17(合計1.00)。誤差0.01までは認める
        var slots = """[{"armor":[1],"weapon":[1],"weight":0.5},{"armor":[1,1],"weapon":[1],"weight":0.33},{"armor":[2],"weapon":[1],"weight":0.175}]""";
        Assert.Single(RarityInference.ParseCombinations(Combo(rarity: "8", slots: slots)));
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
        var skillGroups = RarityInference.LoadSkillGroups();
        var patterns = RarityInference.LoadCombinations()
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
}
