namespace CharmChecker.Core.SlotIcon;

/// <summary>
/// 読み取ったスロットLvが現行ゲームにありえない形だった場合(<see cref="SlotValidation.TryValidate"/>がnullを返した場合)に、
/// スクショの読み取り(ScreenshotCharmReader.ReadSlotsFromImage)がスローする。
/// 読み取りの誤りとみなし、呼び出し側は該当護石全体を読み取り対象から除外する。
/// </summary>
public sealed class InvalidSlotConfigurationException : Exception
{
    public InvalidSlotConfigurationException()
        : base("読み取ったスロットLvが現行ゲームにありえない形です。")
    {
    }
}
