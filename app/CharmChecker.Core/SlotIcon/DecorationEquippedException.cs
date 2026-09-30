namespace CharmChecker.Core.SlotIcon;

/// <summary>
/// 空スロットテンプレート照合(<see cref="SlotIconAnalyzer.MatchEmptyTemplate"/>)でどのLvの空スロットにも
/// 一致しなかった(Level=null)スロットを装飾品装着済みとみなし、護石全体を読み取り対象から除外するための例外
/// (スロット分類処理がスローし、呼び出し側が除外として扱う)。装着が確定したことを意味するのではなく、
/// テンプレート未収録の画面パターンでは空スロットでも該当しうる(安全側の除外。CLAUDE.md
/// 「空スロットテンプレート照合によるLv判定・装飾品検知」節参照)。
/// </summary>
public sealed class DecorationEquippedException : Exception
{
    public DecorationEquippedException()
        : base("スロットに装飾品が装着されているため、レベル判定をスキップしました。")
    {
    }
}
