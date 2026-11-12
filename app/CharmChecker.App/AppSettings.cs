using System.Text.Json.Serialization;

namespace CharmChecker.App;

public class AppSettings
{
    [JsonPropertyName("windowWidth")]
    public double WindowWidth { get; set; } = 800;
    [JsonPropertyName("windowHeight")]
    public double WindowHeight { get; set; } = 450;
    [JsonPropertyName("windowLeft")]
    public double WindowLeft { get; set; } = double.NaN;
    [JsonPropertyName("windowTop")]
    public double WindowTop { get; set; } = double.NaN;
    [JsonPropertyName("screenshotFolder")]
    public string ScreenshotFolder { get; set; } = "";
    [JsonPropertyName("detailPanelHeight")]
    public double DetailPanelHeight { get; set; } = double.NaN;
    /// <summary>"system"(Windowsの設定に合わせる、既定)/"light"/"dark"。<see cref="ThemeService.Parse"/>参照。</summary>
    [JsonPropertyName("theme")]
    public string Theme { get; set; } = "system";
}
