namespace CharmChecker.Core.Skill;

/// <summary>
/// OCR認識結果の1テキスト項目（テキスト内容と画像上の座標）。
/// </summary>
public readonly record struct OcrTextItem(string Text, double X0, double Y0, double X1, double Y1);

/// <summary>
/// 読み取ったスキル1件（名前 + Lv）。名前やLvが読み取れなかった場合はnull。
/// </summary>
public readonly record struct SkillEntry(string? Name, int? Lv);
