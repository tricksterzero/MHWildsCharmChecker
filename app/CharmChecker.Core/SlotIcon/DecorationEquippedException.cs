namespace CharmChecker.Core.SlotIcon;

/// <summary>
/// スロットに装飾品が装着されているため、レベル判定(<see cref="SlotIconAnalyzer.ClassifyLevel"/>)が
/// 信頼できないと判断された場合にスローされる。装飾品装着済みソケットは菱形が実体色で塗りつぶされ
/// 2次元形状になり、列プロファイル方式(2次元形状を1次元に潰す)ではレベル誤判定を起こすことを
/// 実データで確認済み(CLAUDE.md参照)。呼び出し側は該当護石全体を読み取り対象から除外する。
/// </summary>
public sealed class DecorationEquippedException : Exception
{
    public DecorationEquippedException()
        : base("スロットに装飾品が装着されているため、レベル判定をスキップしました。")
    {
    }
}
