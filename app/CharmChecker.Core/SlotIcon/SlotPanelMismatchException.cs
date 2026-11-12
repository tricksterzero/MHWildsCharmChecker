namespace CharmChecker.Core.SlotIcon;

/// <summary>
/// 検出したスロット枠が、スキル欄を読んだパネルとは別のパネルにあると判定された場合(<see cref="SlotPanelConsistency"/>が
/// falseを返した場合)に、スクショの読み取り(ScreenshotCharmReader.ReadSlotsFromImage)がスローする。そのまま保存すると2つの護石が混ざった結果になるため、
/// 呼び出し側は該当護石全体を読み取り対象から除外する。
/// </summary>
public sealed class SlotPanelMismatchException : Exception
{
    public SlotPanelMismatchException()
        : base("スロット欄がスキル欄と別の護石パネルにあると判定されました。")
    {
    }
}
