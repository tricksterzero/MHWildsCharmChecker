using CharmChecker.Core;

namespace CharmChecker.Tests;

/// <summary>埋め込みリソース(Coreに取り込んだresources/*.json)の読み込みのテスト。</summary>
public class EmbeddedResourcesTests
{
    [Theory]
    [InlineData("skill-decoration-map.json")]
    [InlineData("charm-types.json")]
    [InlineData("skill-groups.json")]
    [InlineData("charm-combinations.json")]
    [InlineData("skill-order.json")]
    public void ReadText_RegisteredResource_ReturnsJsonText(string fileName)
    {
        // CharmChecker.Core.csprojに登録した5つの埋め込みリソースがすべて読める
        var text = EmbeddedResources.ReadText(fileName).TrimStart();
        Assert.True(text.StartsWith('[') || text.StartsWith('{'), $"{fileName}の先頭がJSONではありません。");
    }

    [Fact]
    public void ReadText_MissingResource_ThrowsWithFileName()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => EmbeddedResources.ReadText("missing.json"));
        Assert.Equal("埋め込みリソース 'missing.json' が見つかりません。", ex.Message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void ReadText_BlankFileName_Rejected(string? fileName)
    {
        // 境界値: ファイル名は空・空白にできない
        Assert.ThrowsAny<ArgumentException>(() => EmbeddedResources.ReadText(fileName!));
    }
}
