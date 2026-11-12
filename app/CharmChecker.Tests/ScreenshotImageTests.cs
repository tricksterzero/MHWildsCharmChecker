using CharmChecker.Core.Image;
using CharmChecker.Core.Screenshot;
using CharmChecker.Core.Skill;
using OpenCvSharp;

namespace CharmChecker.Tests;

/// <summary>
/// スクショの画像を1回だけ読み、スキル欄とスロット欄の読み取りに同じ画像を渡す処理のテスト(2026-11-12)。
/// </summary>
public class ScreenshotImageTests
{
    [Fact]
    public void Read_NullOrEmptyPath_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => ScreenshotImage.Read(null!));
        Assert.Throws<ArgumentException>(() => ScreenshotImage.Read(""));
    }

    [Fact]
    public void Read_NotAnImageFile_ThrowsInvalidDataExceptionWithPath()
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"charmchecker_notimage_{Guid.NewGuid():N}.jpg");
        File.WriteAllText(tempPath, "これは画像ではありません");
        try
        {
            var ex = Assert.Throws<InvalidDataException>(() => ScreenshotImage.Read(tempPath));
            Assert.Equal($"画像を読み込めませんでした: {tempPath}", ex.Message);
        }
        finally
        {
            File.Delete(tempPath);
        }
    }

    [Fact]
    public void Read_ImageFile_ReturnsBgrImageOfSameSize()
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"charmchecker_image_{Guid.NewGuid():N}.png");
        using (var source = new Mat(new Size(12, 7), MatType.CV_8UC3, Scalar.All(100)))
        {
            Cv2.ImWrite(tempPath, source);
        }
        try
        {
            using var img = ScreenshotImage.Read(tempPath);
            Assert.Equal(MatType.CV_8UC3, img.Type());
            Assert.Equal(new Size(12, 7), img.Size());
        }
        finally
        {
            File.Delete(tempPath);
        }
    }

    [Fact]
    public async Task ReadWithMetadataFromImageAsync_InvalidArguments_Throw()
    {
        var knownSkills = SkillNameLoader.LoadFromEmbeddedResource();
        using var empty = new Mat();
        using var gray = new Mat(new Size(10, 10), MatType.CV_8UC1, Scalar.All(0));
        using var bgr = new Mat(new Size(10, 10), MatType.CV_8UC3, Scalar.All(0));

        await Assert.ThrowsAsync<ArgumentNullException>(() => SkillReadingPipeline.ReadWithMetadataFromImageAsync(null!, knownSkills));
        await Assert.ThrowsAsync<ArgumentException>(() => SkillReadingPipeline.ReadWithMetadataFromImageAsync(empty, knownSkills));
        await Assert.ThrowsAsync<ArgumentException>(() => SkillReadingPipeline.ReadWithMetadataFromImageAsync(gray, knownSkills));
        await Assert.ThrowsAsync<ArgumentNullException>(() => SkillReadingPipeline.ReadWithMetadataFromImageAsync(bgr, null!));
    }

    [Fact]
    public void ReadSlotsFromImage_InvalidArguments_Throw()
    {
        using var empty = new Mat();
        using var gray = new Mat(new Size(10, 10), MatType.CV_8UC1, Scalar.All(0));

        Assert.Throws<ArgumentNullException>(() => ScreenshotCharmReader.ReadSlotsFromImage(null!, hasWeaponSlot: false));
        Assert.Throws<ArgumentException>(() => ScreenshotCharmReader.ReadSlotsFromImage(empty, hasWeaponSlot: false));
        Assert.Throws<ArgumentException>(() => ScreenshotCharmReader.ReadSlotsFromImage(gray, hasWeaponSlot: false));
    }

    [Fact]
    public async Task FromImage_SameResultsAsFromPath()
    {
        // ファイルのパスを受け取る公開メソッドと、読んだ画像を受け取るメソッドで結果が同じこと
        var path = Path.Combine(TestPaths.FindAssetsDir(), "case5 craft result", "20260615054704_1.jpg");
        var knownSkills = SkillNameLoader.LoadFromEmbeddedResource();
        using var img = ScreenshotImage.Read(path);

        var fromPath = await SkillReadingPipeline.ReadWithMetadataAsync(path, knownSkills);
        var fromImage = await SkillReadingPipeline.ReadWithMetadataFromImageAsync(img, knownSkills);
        Assert.NotNull(fromPath);
        Assert.NotNull(fromImage);
        Assert.Equal(fromPath.Skills, fromImage.Skills);
        Assert.Equal(fromPath.CharmName, fromImage.CharmName);
        Assert.Equal(fromPath.AnchorX, fromImage.AnchorX);
        Assert.Equal(fromPath.AnchorY, fromImage.AnchorY);

        var slotsFromPath = ScreenshotCharmReader.ReadSlots(path, hasWeaponSlot: false, fromPath.AnchorX);
        var slotsFromImage = ScreenshotCharmReader.ReadSlotsFromImage(img, hasWeaponSlot: false, fromImage.AnchorX);
        Assert.Equal(slotsFromPath.ArmorSlots, slotsFromImage.ArmorSlots);
        Assert.Equal(slotsFromPath.WeaponSlots, slotsFromImage.WeaponSlots);
    }
}
