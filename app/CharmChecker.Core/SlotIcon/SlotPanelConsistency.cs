namespace CharmChecker.Core.SlotIcon;

/// <summary>
/// 検出したスロット枠が、スキル欄を読んだパネル(アンカー)と同じパネルにあるかを判定する。
/// 2護石比較画面で、スキル欄は左パネル、スロット欄は右パネル(固定の探索領域)から読み、
/// 2つの護石が混ざった結果を保存することを防ぐ(2026-11-08)。
/// </summary>
public static class SlotPanelConsistency
{
    /// <summary>
    /// すべての枠の左端Xが、アンカーXから見て
    /// [<see cref="SlotIconConstants.SlotFrameMinDxFromAnchor"/>, <see cref="SlotIconConstants.SlotFrameMaxDxFromAnchor"/>]×scale
    /// の範囲(両端を含む)にあればtrueを返す。枠が0件ならtrue。
    /// 事前条件: frameLeftXsはnullでなく各値が有限、anchorXは有限、scaleは正の有限値。
    /// </summary>
    /// <param name="frameLeftXs">枠の左端X(元画像の座標)。</param>
    /// <param name="anchorX">スキル欄のアンカーX(元画像の座標)。</param>
    /// <param name="scale">UIの倍率(コンテンツ高さ/1440)。</param>
    public static bool AreFramesInSkillPanel(IReadOnlyList<double> frameLeftXs, double anchorX, double scale)
    {
        ArgumentNullException.ThrowIfNull(frameLeftXs);
        if (!double.IsFinite(anchorX))
            throw new ArgumentOutOfRangeException(nameof(anchorX), anchorX, "anchorXは有限の値である必要があります。");
        if (!double.IsFinite(scale) || scale <= 0)
            throw new ArgumentOutOfRangeException(nameof(scale), scale, "scaleは正の有限値である必要があります。");
        if (frameLeftXs.Any(x => !double.IsFinite(x)))
            throw new ArgumentException("frameLeftXsの各値は有限である必要があります。", nameof(frameLeftXs));

        return frameLeftXs.All(x => IsWithinRange((x - anchorX) / scale));
    }

    private static bool IsWithinRange(double dx) =>
        dx >= SlotIconConstants.SlotFrameMinDxFromAnchor && dx <= SlotIconConstants.SlotFrameMaxDxFromAnchor;
}
