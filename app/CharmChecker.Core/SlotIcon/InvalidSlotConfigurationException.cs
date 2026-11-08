namespace CharmChecker.Core.SlotIcon;

/// <summary>
/// 読み取ったスロットLvが現行ゲームにありえない形だった場合にスローされる(<see cref="SlotValidation.TryValidate"/>)。
/// 読み取りの誤りとみなし、呼び出し側は該当護石全体を読み取り対象から除外する。
/// </summary>
public sealed class InvalidSlotConfigurationException : Exception
{
    public InvalidSlotConfigurationException()
        : base("読み取ったスロットLvが現行ゲームにありえない形です。")
    {
    }
}
