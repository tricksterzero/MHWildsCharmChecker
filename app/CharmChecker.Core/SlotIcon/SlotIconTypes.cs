namespace CharmChecker.Core.SlotIcon;

/// <summary>スロットのレベル（三角アイコンの数で判定）。</summary>
public enum SlotLevel
{
    Unknown,
    Lv1,
    Lv2,
    Lv3,
}

/// <summary>スロットの種別（バッジアイコンで判定）。</summary>
public enum SlotType
{
    Unknown,
    Weapon,
    Armor,
}

/// <summary>
/// レベル判定の結果。PeakCount/ValleyRatios はチューニング・デバッグ用の中間値。
/// </summary>
/// <param name="Level">判定されたレベル。</param>
/// <param name="PeakCount">列プロファイルのピーク数。</param>
/// <param name="ValleyRatios">隣接ピーク間の谷比率（谷が無い場合はnull）。</param>
public record LevelClassification(SlotLevel Level, int PeakCount, IReadOnlyList<double?> ValleyRatios);

/// <summary>
/// 種別判定の結果。BukiScore/BouguScore は武器/防具の参照テンプレートとの一致度（TM_CCOEFF_NORMED）。
/// </summary>
public record TypeClassification(SlotType Type, double? BukiScore, double? BouguScore);

/// <summary>
/// pipeline.py で検証済みの判定パラメータ。
/// 基準解像度2560x1440に対する比率・px値として定義し、実画像サイズに応じてスケーリングする。
/// </summary>
public static class SlotIconConstants
{
    public const int RefWidth = 2560;
    public const int RefHeight = 1440;

    // 装備BOX側スロットアイコンの探索領域（基準解像度に対する比率）
    public const double PanelY0Frac = 320.0 / RefHeight;
    public const double PanelY1Frac = 400.0 / RefHeight;
    public const double PanelX0Frac = 2340.0 / RefWidth;
    public const double PanelX1Frac = 2480.0 / RefWidth;

    // ソケット枠検出のサイズフィルタ（基準解像度でのpx範囲）
    public const double FrameWidthMin = 30;
    public const double FrameWidthMax = 55;
    public const double FrameHeightMin = 20;
    public const double FrameHeightMax = 45;
    public const double FrameYMin = 10;

    // バッジ探索領域（枠基準のオフセット、基準解像度でのpx）
    public const double BadgeOffsetLeft = -15;
    public const double BadgeOffsetRight = 25;
    public const double BadgeOffsetTop = -35;
    public const double BadgeOffsetBottom = 8;
}
