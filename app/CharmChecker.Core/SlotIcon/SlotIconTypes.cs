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
/// 空スロットテンプレート照合の結果。
/// </summary>
/// <param name="Level">空スロットと判定された場合のレベル。装飾品装着済み(テンプレートと十分近くない)と
/// 判定された場合はnull。</param>
/// <param name="BestDiff">最も近かったテンプレートとの平均絶対差分(BGR、0〜255スケール)。チューニング・
/// デバッグ用の中間値。</param>
public record EmptyTemplateMatch(SlotLevel? Level, double BestDiff);

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
    // 「RARE」ラベルの「R」の文字の輪郭(実測37x22px)がソケット枠として誤検出される問題を
    // 高さ下限の引き上げ(20→28)で対処しようとしたが、既存資産に正当なソケット枠が高さ26〜27pxの
    // ケースがあり(誤検出との差が4〜5pxしかない)安全に分離できないため撤回(2026-07-27)。
    // 誤検出の排除はMatchEmptyTemplateの差分値ベースの候補除外(ClassifyFrames)で行う。
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

    // 空スロットテンプレート照合(MatchEmptyTemplate)の閾値: 枠下部の三角マーク領域を
    // EmptySlotTemplatesとBGR平均絶対差分で比較し、この値以下なら空スロットとみなす。
    // **注意**: 「空:0.00〜0.03 / 装着:6.71〜49.48で明確に分離」という値は、テンプレート自身を
    // 検証対象に含めた自己参照の結果であり不正確(2026-07-27、Codexの指摘で判明)。
    // leave-one-out検証(各サンプルを一時的にテンプレート集合から除いて最近傍マッチさせる)での
    // 実際の値は、類似した画面パターンのサンプルが複数ある場合は0.0〜2.7程度で収まるが、
    // 類似サンプルが無い「孤立した」画面パターン(例: 単一サンプルしかないDetailパネル画面、
    // 鑑定BOXの武器スロット)ではleave-one-out差分が5.4〜38.0まで跳ね上がり、装着ケースの
    // 最小値(6.71)を上回ることがある。つまりこの閾値が保証するのは「EmptySlotTemplatesに
    // 十分近い画面パターンのサンプルが複数存在する場合」のみで、未収録の画面パターンでは
    // 空スロットでも閾値を超えて「装着済み」= 護石ごと除外側に倒れうる(誤読ではなく安全側の
    // 取りこぼし)。新しい画面パターンで空スロットの取りこぼしが増えた場合は、その画面パターンの
    // 空スロットサンプルをEmptySlotTemplatesに追加することが正攻法(閾値を緩めると装着見逃しの
    // リスクが増すため非推奨)。
    public const double EmptyTemplateThreshold = 3.0;
}
