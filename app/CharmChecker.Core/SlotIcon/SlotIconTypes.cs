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
/// <param name="BestDiff">最も近かったテンプレートとの平均絶対差分(チャネルごとの平均を引いたBGR、0〜255スケール)。
/// チューニング・デバッグ用の中間値。</param>
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

    // 空スロットテンプレート照合(MatchEmptyTemplate)で、枠の位置・大きさを上下左右に何pxまで
    // ずらして探すか。Cannyの輪郭検出で同じスロットでも枠が1〜2px揺れ、固定サイズへの縮小後に
    // 三角マークの位置がずれて差分が跳ね上がる(同じ画面・同じスロットで差分17前後)ことへの対策
    // (2026-11-07)。
    public const int FrameJitterSearchPx = 2;

    // 空スロットテンプレート照合(MatchEmptyTemplate)の閾値: 枠下部の三角マーク領域とEmptySlotTemplatesを、
    // チャネルごとの平均を引いたBGR平均絶対差分で比較し(枠を±FrameJitterSearchPxずらした最小値)、
    // この値以下なら空スロットとみなす。
    // 2026-11-07、パネルが半透明で背景の色が透け、空スロットでも色の絶対値がずれて除外される
    // 不具合(狩猟後の鑑定BOX画面・マカ錬金画面、2560x1440)を受けて、比較方式の変更と同時に
    // 3.0→5.0へ決め直した。根拠はassets全画像+不具合の3枚でのleave-one-out相当の測定
    // (テンプレートの元画像自身との一致を除いた最近傍差分):
    //   - 空スロット: 同じ画面パターンのサンプルが複数ある画像で最大3.16。サンプルが乏しい画面では
    //     5.29(簡易キャンプ画面)・5.38(鑑定BOXの武器スロット)
    //   - 装飾品あり16枚: 最小7.77(Lv3スロットに灰色のLv1装飾品。空スロットに最も近い見た目)
    // 装飾品ありを空と判定すると誤ったスロット値が保存される(除外より害が大きい)ため、
    // 装飾品ありの最小値との余裕(約2.8)を残す位置にした。装飾品ありのサンプルは16枚のみで、
    // 手元にない色・画面ではより低い値になりうる点は未確認。未収録の画面パターンで空スロットの
    // 取りこぼし(護石ごと除外)が増えた場合は、その画面の空スロットサンプルをEmptySlotTemplatesに
    // 追加する。
    public const double EmptyTemplateThreshold = 5.0;

    // スロット枠がスキル欄と同じパネルにあるか(SlotPanelConsistency)の許容範囲: 枠の左端X−アンカーX
    // (基準解像度でのpx。UIの倍率を掛けて使う)。2026-11-08、護石画面の159枚・303枠で測ると、
    // 読み取り成功の画像は221〜389、スキル欄とスロット欄が別のパネルだった3枚は-49・744・1218
    // (2護石比較画面の左右パネルの間隔は約480px)。両側に60〜70pxの余裕を取った値。Y方向は、
    // 「装備スキル」からの逆算などでアンカーが最大約100pxずれる(読み取り成功でも185〜329)ため使わない。
    public const double SlotFrameMinDxFromAnchor = 150.0;
    public const double SlotFrameMaxDxFromAnchor = 450.0;
}
