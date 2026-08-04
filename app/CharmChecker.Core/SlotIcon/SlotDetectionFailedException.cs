namespace CharmChecker.Core.SlotIcon;

/// <summary>
/// BOX領域・Detail領域のいずれからもソケット枠が1件も検出できなかった場合にスローされる。
/// charm-combinations.jsonの全パターン(RARE5〜8)にスロット完全ゼロの組み合わせは存在しないため
/// (2026-08-05確認)、0件は正常な「穴なし護石」ではなく検出失敗である。呼び出し側は該当護石全体を
/// 読み取り対象から除外する。
/// </summary>
public sealed class SlotDetectionFailedException : Exception
{
    public SlotDetectionFailedException()
        : base("スロットアイコンを1件も検出できませんでした。")
    {
    }
}
