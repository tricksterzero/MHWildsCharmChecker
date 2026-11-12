using CharmChecker.Core.Image;
using CharmChecker.Core.Model;
using CharmChecker.Core.Skill;
using CharmChecker.Core.SlotIcon;
using OpenCvSharp;

namespace CharmChecker.Core.Screenshot;

/// <summary>スクショ1枚の読み取り結果の種類。</summary>
public enum ScreenshotOutcomeKind
{
    NotCharmPanel,
    Detected,
    NoValidSkill,
    NoCharmName,
    DecorationEquipped,
    SlotDetectionFailed,
    InconsistentWithCombinations,
    SlotPanelMismatch,
    Error,
}

/// <summary>スクショ1枚の読み取り結果。Detectedのときだけ<see cref="Charm"/>、Errorのときだけ<see cref="Error"/>を持つ。</summary>
public sealed record ScreenshotOutcome(ScreenshotOutcomeKind Kind, Charm? Charm = null, Exception? Error = null);

/// <summary>
/// スクショから護石(スキル・護石名・スロット)を読み取る。画面(MainWindow)から切り離し、UI要素・ErrorLoggerには
/// 触れずに結果を<see cref="ScreenshotOutcome"/>で返す(ログ出力・集計は呼び出し側がファイル順に行う)。
/// 2026-11-08にMainWindowから移した(処理の内容は変えていない)。
/// </summary>
public static class ScreenshotCharmReader
{
    /// <summary>
    /// 複数のスクショを<paramref name="parallelism"/>本のワーカーで並列に読み取る。戻り値は
    /// <paramref name="files"/>と同じ順の配列で、中断により処理しなかったファイルはnull。
    /// ワーカーは共有カウンタで先頭から順にファイルを取り出すため、中断時も「処理を開始した
    /// ファイル」は常に先頭からの連続範囲になり(開始済みのファイルは最後まで処理する)、
    /// 中断時の「X / N枚まで処理」の意味が逐次処理時と変わらない。
    /// <paramref name="progress"/>には完了件数を報告する(並列のため到着順は前後しうる)。
    /// 事前条件: files・knownSkills・charmTypesはnullでなく、parallelismは1以上。
    /// </summary>
    public static async Task<ScreenshotOutcome?[]> ProcessAllAsync(
        IReadOnlyList<string> files, IReadOnlyList<string> knownSkills, IReadOnlyList<CharmTypeInfo> charmTypes,
        int parallelism, IProgress<int>? progress, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(knownSkills);
        ArgumentNullException.ThrowIfNull(charmTypes);
        ArgumentOutOfRangeException.ThrowIfLessThan(parallelism, 1);

        var outcomes = new ScreenshotOutcome?[files.Count];
        int nextIndex = -1;
        int completed = 0;

        var workers = Enumerable.Range(0, Math.Min(parallelism, files.Count))
            .Select(_ => Task.Run(async () =>
            {
                while (!ct.IsCancellationRequested)
                {
                    int i = Interlocked.Increment(ref nextIndex);
                    if (i >= files.Count) return;
                    outcomes[i] = await ProcessAsync(files[i], knownSkills, charmTypes);
                    progress?.Report(Interlocked.Increment(ref completed));
                }
            }))
            .ToArray();
        await Task.WhenAll(workers);
        return outcomes;
    }

    /// <summary>
    /// スクショ1枚からの護石読み取り。スレッドプールから並列に呼ばれる。読み取れなかった理由・例外も
    /// 結果として返し、例外は投げない(事前条件の違反を除く)。
    /// 事前条件: 引数はnullでない。
    /// </summary>
    public static async Task<ScreenshotOutcome> ProcessAsync(
        string file, IReadOnlyList<string> knownSkills, IReadOnlyList<CharmTypeInfo> charmTypes)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentNullException.ThrowIfNull(knownSkills);
        ArgumentNullException.ThrowIfNull(charmTypes);

        try
        {
            // スキル欄とスロット欄で同じ画像を使い、ファイルは1回だけ読む(2026-11-12。それまでは2回読んでいた)
            using var img = ScreenshotImage.Read(file);
            var readResult = await SkillReadingPipeline.ReadWithMetadataFromImageAsync(img, knownSkills);
            if (readResult is null)
                return new(ScreenshotOutcomeKind.NotCharmPanel);

            var validSkills = readResult.Skills
                .Where(s => s.Name is not null && s.Lv is not null)
                .Select(s => new CharmSkill(s.Name!, s.Lv!.Value))
                .ToList();

            if (validSkills.Count == 0)
                return new(ScreenshotOutcomeKind.NoValidSkill);

            // 同じスキル名を2行で読んだ結果は実在しない護石(護石の不変条件にも反する)ため、
            // 構成表に当てはまらない読み取り結果と同じ扱いで除外する
            if (validSkills.Select(s => s.Name).Distinct(StringComparer.Ordinal).Count() != validSkills.Count)
                return new(ScreenshotOutcomeKind.InconsistentWithCombinations);

            if (readResult.CharmName is null)
            {
                // 護石名テキスト自体が読み取れなかった場合、charm-types.json登録4種
                // (未解/史伝/秘歴/栄世)のどれに該当するかも、そもそも該当しない一般護石・
                // 希望の護石等かも判別できない。hasWeaponSlot=falseのままReadSlotsへ渡すと、
                // 実際は栄世の護石(武器スロット持ち)だった場合に武器スロットの検出値が
                // 防具スロットとして誤保存されるリスクがある(2026-08-05発見)。
                // スロット値自体は妥当範囲に収まるためSlotValidationでは検出できないサイレント
                // 誤りになるため、護石名が完全に不明な場合のみ読み取り失敗として除外する。
                // 護石名は読めたがcharm-types.jsonに未登録(希望の護石等)の場合は、従来通り
                // hasWeaponSlot=false・Rarity=nullで処理を続け、後続のRarityInferenceによる
                // 補完に委ねる(未登録=栄世の護石ではないと確定できるため誤混同のリスクがない)。
                return new(ScreenshotOutcomeKind.NoCharmName);
            }

            var charmType = CharmTypeLoader.Lookup(readResult.CharmName, charmTypes);
            var slots = ReadSlotsFromImage(img, charmType?.HasWeaponSlot ?? false, readResult.AnchorX);

            var charm = new Charm
            {
                Skills = validSkills,
                ArmorSlots = slots.ArmorSlots,
                WeaponSlots = slots.WeaponSlots,
                Rarity = charmType?.Rarity,
                Source = CharmSource.Screenshot,
                SourceTimestamp = File.GetLastWriteTime(file),
            };

            // スキル・スロットが構成表のどの組み合わせにも当てはまらない護石は実在しないため、
            // 読み取りの誤り(スキルの読み落とし、スロットLvの読み違い等)として保存せずに除外する
            // (2026-11-07、2スキルのうち1つしか読めず1スキルの護石として保存されかけた事例による)。
            if (!CharmCombinationValidator.MatchesAnyCombination(charm))
                return new(ScreenshotOutcomeKind.InconsistentWithCombinations);

            return new(ScreenshotOutcomeKind.Detected, charm);
        }
        catch (DecorationEquippedException)
        {
            return new(ScreenshotOutcomeKind.DecorationEquipped);
        }
        catch (SlotPanelMismatchException)
        {
            return new(ScreenshotOutcomeKind.SlotPanelMismatch);
        }
        catch (InvalidSlotConfigurationException)
        {
            // ありえないスロットLvの読み取り結果は、構成表に当てはまらない読み取り結果と同じ扱いで除外する
            return new(ScreenshotOutcomeKind.InconsistentWithCombinations);
        }
        catch (SlotDetectionFailedException)
        {
            // BOX/Detail両領域からスロットアイコンが1件も検出できなかったケース。
            // 対象護石にスロット完全ゼロの構成は存在しない(charm-combinations.json参照)ため
            // 検出失敗として扱い、[0,0,0]のまま正常保存されることを防ぐ(2026-08-05発見)。
            return new(ScreenshotOutcomeKind.SlotDetectionFailed);
        }
        catch (Exception ex)
        {
            return new(ScreenshotOutcomeKind.Error, Error: ex);
        }
    }

    /// <summary>
    /// スクショからスロット構成を読み取る。<paramref name="anchorX"/>(スキル欄のアンカーX、元画像の座標)を
    /// 渡した場合は、採用したスロット枠がスキル欄と同じパネルにあるかを確かめ、別のパネルなら
    /// <see cref="SlotPanelMismatchException"/>をスローする(nullなら確かめない)。
    /// 枠が無ければ<see cref="SlotDetectionFailedException"/>、装飾品ありなら<see cref="DecorationEquippedException"/>、
    /// ありえないLvの形なら<see cref="InvalidSlotConfigurationException"/>をスローする。
    /// 事前条件: imagePathは空でなく、画像として読み込める(読み込めなければ<see cref="InvalidDataException"/>)。
    /// </summary>
    public static (List<int> ArmorSlots, List<int> WeaponSlots) ReadSlots(
        string imagePath, bool hasWeaponSlot, double? anchorX = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(imagePath);

        using var img = ScreenshotImage.Read(imagePath);
        return ReadSlotsFromImage(img, hasWeaponSlot, anchorX);
    }

    /// <summary>
    /// 読み込み済みの画像から<see cref="ReadSlots"/>と同じ読み取りをする(画像は変更しない。例外も同じ)。
    /// スキル欄の読み取りと同じ画像を使い、ファイルを2回読まないため(2026-11-12)。
    /// 事前条件: imgは空でない3チャネル(BGR)の画像。
    /// </summary>
    internal static (List<int> ArmorSlots, List<int> WeaponSlots) ReadSlotsFromImage(
        Mat img, bool hasWeaponSlot, double? anchorX = null)
    {
        ScreenshotImage.ThrowIfNotBgr(img, nameof(img));

        var (sx, sy) = SlotIconAnalyzer.ScaleFactors(img);

        var boxRegion = SlotIconAnalyzer.PanelRegion(img);
        var detRegion = SlotIconAnalyzer.DetailPanelRegion(img);
        using var box = DetectInRegion(img, boxRegion, sx, sy);
        using var detail = DetectInRegion(img, detRegion, sx, sy);

        // BOX領域で1件以上検出できていれば常にBOX側を信頼する。Detail領域(x:1400-1650)は
        // 画面パターンによっては護石画像プレビュー等の無関係な内容(背景テクスチャ等)を
        // 誤検出しうるため、BOX側の検出数がDetail側を下回っていてもBOX優先とする
        // (マカ錬金鑑定結果画面で背景の木目模様がDetail領域に2枠の偽陽性を生み、
        // BOX側の正しい1枠より多く検出されてDetail側が誤採用される回帰を確認したため)。
        var chosen = box.Frames.Count > 0 ? box : detail;

        if (chosen.Frames.Count == 0)
            throw new SlotDetectionFailedException();

        // 空スロットの照合より前に確かめる。別のパネルの枠は照合でも「装飾品あり」と判定される
        // ことが多く、後に置くと除外理由が装飾品になってしまうため。
        // 枠の座標は探索領域内の座標のため、領域の左端を足して元画像の座標にする(sxはUIの倍率)。
        if (anchorX is { } ax &&
            !SlotPanelConsistency.AreFramesInSkillPanel(
                chosen.Frames.Select(f => (double)(chosen.Region.X + f.X)).ToList(), ax, sx))
            throw new SlotPanelMismatchException();

        var levels = chosen.Frames.Select(frame => ToLevelNumber(SlotIconAnalyzer.MatchEmptyTemplate(chosen.Color, frame))).ToList();
        return AssignSlots(levels, hasWeaponSlot) ?? throw new InvalidSlotConfigurationException();
    }

    /// <summary>
    /// 枠のLv(左から順)を武器スロット・防具スロットに振り分け、現行ゲームにありうる形なら返す(副作用なし)。
    /// 栄世の護石(<paramref name="hasWeaponSlot"/>)は1つ目の枠が武器スロット、それ以外はすべて防具スロット。
    /// 枠が3個を超える場合も切り捨てずに<see cref="SlotValidation.TryValidate"/>へ渡し、ありえない形としてnullを返す。
    /// 事前条件: levelsはnullでない。
    /// </summary>
    internal static (List<int> ArmorSlots, List<int> WeaponSlots)? AssignSlots(IReadOnlyList<int> levels, bool hasWeaponSlot)
    {
        ArgumentNullException.ThrowIfNull(levels);

        var weaponSlots = hasWeaponSlot && levels.Count > 0 ? levels.Take(1).ToList() : new List<int>();
        var armorSlots = levels.Skip(weaponSlots.Count).ToList();
        return SlotValidation.TryValidate(armorSlots, weaponSlots);
    }

    /// <summary>空スロットの照合結果をLvの数値にする。装飾品あり(Level=null)なら<see cref="DecorationEquippedException"/>。</summary>
    private static int ToLevelNumber(EmptyTemplateMatch match) => match.Level switch
    {
        SlotLevel.Lv1 => 1,
        SlotLevel.Lv2 => 2,
        SlotLevel.Lv3 => 3,
        null => throw new DecorationEquippedException(),
        _ => 0,
    };

    /// <summary>探索領域1つ分の枠の検出結果。Colorは元画像の領域部分(BGR)で、Disposeで解放する。</summary>
    private sealed class RegionDetection(Rect region, List<Rect> frames, Mat color) : IDisposable
    {
        public Rect Region { get; } = region;
        public List<Rect> Frames { get; } = frames;
        public Mat Color { get; } = color;
        public void Dispose() => Color.Dispose();
    }

    private static RegionDetection DetectInRegion(Mat img, Rect region, double sx, double sy)
    {
        var color = new Mat(img, region);
        try
        {
            using var gray = new Mat();
            Cv2.CvtColor(color, gray, ColorConversionCodes.BGR2GRAY);
            var frames = SlotIconAnalyzer.DetectFrames(gray, sx, sy);
            return new RegionDetection(region, frames, color);
        }
        catch
        {
            color.Dispose();
            throw;
        }
    }
}
