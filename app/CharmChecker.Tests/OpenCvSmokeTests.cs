using OpenCvSharp;

namespace CharmChecker.Tests;

public class OpenCvSmokeTests
{
    [Fact]
    public void ImRead_LoadsAssetImage_WithExpectedResolution()
    {
        var path = FindAssetImage("20260604111556_1.jpg");

        using var mat = Cv2.ImRead(path);

        Assert.False(mat.Empty());
        Assert.Equal(2560, mat.Width);
        Assert.Equal(1440, mat.Height);
    }

    private static string FindAssetImage(string fileName)
        => Path.Combine(TestPaths.FindAssetsDir(), fileName);
}
