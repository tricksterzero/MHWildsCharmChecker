namespace CharmChecker.Core.SlotIcon;

/// <summary>スロットのレベル（三角アイコンの数で判定）。</summary>
public enum SlotLevel
{
    Unknown,
    Lv1,
    Lv2,
    Lv3,
}

/// <summary>
/// レベル判定の結果。PeakCount/ValleyRatios はチューニング・デバッグ用の中間値。
/// </summary>
/// <param name="Level">判定されたレベル。</param>
/// <param name="PeakCount">列プロファイルのピーク数。</param>
/// <param name="ValleyRatios">隣接ピーク間の谷比率（谷が無い場合はnull）。</param>
public record LevelClassification(SlotLevel Level, int PeakCount, IReadOnlyList<double?> ValleyRatios);

/// <summary>
/// 判定パラメータ。基準解像度2560x1440に対する比率・px値として定義し、実画像サイズに応じてスケーリングする。
///
/// パネル探索領域(PanelX0等)は「画面幅に対する比率」ではなく「右上コーナーからの固定距離」で
/// 定義する。実測(2026-07-26、真の21:9=3440x1440ネイティブ、黒帯なし)で、ゲーム内UIは画面幅が
/// 変わっても右上コーナー基準・絶対サイズ不変で配置される(横に広がった分は3Dシーンの表示領域が
/// 増えるだけ)ことを確認済み。「画面幅に対する比率」で計算すると、真の21:9(3440幅)でパネルが
/// 実際より内側にずれて誤検出・検出漏れの原因になっていた。
/// </summary>
public static class SlotIconConstants
{
    public const int RefWidth = 2560;
    public const int RefHeight = 1440;

    // 装備BOX側スロットアイコンの探索領域（基準解像度でのpx、右上コーナー基準）
    public const double PanelY0 = 280.0;
    public const double PanelY1 = 420.0;
    public const double PanelX0 = 2200.0;
    public const double PanelX1 = 2500.0;

    // ソケット枠検出のサイズフィルタ（基準解像度でのpx範囲）
    public const double FrameWidthMin = 30;
    public const double FrameWidthMax = 55;
    public const double FrameHeightMin = 20;
    public const double FrameHeightMax = 45;
    public const double FrameYMin = 10;

    // x近接統合・yクラスタリングの閾値（基準解像度でのpx）
    public const double MergeXThreshold = 20;
    public const double ClusterYThreshold = 15;

    // レベル判定に使う枠下部の割合
    public const double LevelCropTopFraction = 0.45;

    // 単一護石詳細画面のスロットアイコン探索領域（基準解像度でのpx、右上コーナー基準）
    public const double DetailPanelY0 = 310.0;
    public const double DetailPanelY1 = 410.0;
    public const double DetailPanelX0 = 1400.0;
    public const double DetailPanelX1 = 1650.0;

    // 装飾品装着判定: 枠上部(菱形相当領域)の中心部分の低輝度画素残存率(第25百分位)を、
    // 上部領域全体の高輝度画素(第90百分位、菱形の輪郭線相当)で正規化した値。
    // 未装着(菱形の中が暗い穴)は0.148~0.248、装着済み(菱形が実体色で塗りつぶし)は0.399~0.895で、
    // 実データ(2026-07-27、19枠+5枠)で分離を確認済み。中間値を閾値とする。
    public const double DecorationInnerLowPercentile = 0.25;
    public const double DecorationUpperHighPercentile = 0.90;
    public const double DecorationFeatureThreshold = 0.32;
}
