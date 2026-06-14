# C#移植時の参照用スクリプト（検証済みアルゴリズムの記録。ビルド対象ではない）。
# 概要・パラメータの説明はリポジトリルートのCLAUDE.md「スロットアイコン判定ロジック」を参照。
import cv2
import numpy as np
import os

SRC = r"C:\File\Program\MHWildsCharmChecker\assets"
TMP = r"C:\File\Program\MHWildsCharmChecker\legacy\slot-icon-pipeline"

# 基準解像度（検証済み: 2560x1440, 16:9）。実画像サイズとの比率でスケーリングする。
REF_W, REF_H = 2560, 1440

# 装備BOX側スロットアイコンの探索領域（基準解像度に対する比率: y0,y1,x0,x1）
PANEL_REGION_FRAC = (320 / REF_H, 400 / REF_H, 2340 / REF_W, 2480 / REF_W)

# ソケット枠検出のサイズフィルタ（基準解像度でのpx範囲）
FRAME_W_RANGE = (30, 55)
FRAME_H_RANGE = (20, 45)
FRAME_Y_MIN = 10

# バッジ探索領域（枠基準のオフセット、基準解像度でのpx）
BADGE_OFFSETS = dict(left=-15, right=25, top=-35, bottom=8)


def panel_region(img):
    h, w = img.shape[:2]
    y0f, y1f, x0f, x1f = PANEL_REGION_FRAC
    return int(y0f * h), int(y1f * h), int(x0f * w), int(x1f * w)


def detect_frames(gray, sx, sy):
    wlo, whi = FRAME_W_RANGE[0] * sx, FRAME_W_RANGE[1] * sx
    hlo, hhi = FRAME_H_RANGE[0] * sy, FRAME_H_RANGE[1] * sy
    y_min = FRAME_Y_MIN * sy

    edges = cv2.Canny(gray, 50, 150)
    contours, _ = cv2.findContours(edges, cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_SIMPLE)
    frames = []
    for c in contours:
        x, y, fw, fh = cv2.boundingRect(c)
        if wlo < fw < whi and hlo < fh < hhi and y >= y_min:
            frames.append((x, y, fw, fh))
    frames.sort(key=lambda f: f[0])
    return frames


def classify_level(gray, frame):
    x, y, w, h = frame
    y0 = y + int(h * 0.45)
    crop = gray[y0:y+h, x:x+w]
    resized = cv2.resize(crop, (50, 20))
    _, binimg = cv2.threshold(resized, 0, 255, cv2.THRESH_BINARY + cv2.THRESH_OTSU)
    profile = (binimg > 127).sum(axis=0).astype(float)
    sm = np.convolve(profile, np.ones(3) / 3, mode="same")
    peak = sm.max()
    thresh = peak * 0.75
    above = sm >= thresh
    groups, cur = [], []
    for i, b in enumerate(above):
        if b:
            cur.append(i)
        else:
            if cur:
                groups.append(cur)
                cur = []
    if cur:
        groups.append(cur)
    ratios = []
    for a, b in zip(groups[:-1], groups[1:]):
        seg = sm[a[-1]+1:b[0]]
        ratios.append(float(seg.min() / peak) if len(seg) else None)

    n = len(groups)
    if n == 2 and ratios[0] is not None and ratios[0] < 0.5:
        level = "Lv1"
    elif n == 2 and ratios[0] is not None and ratios[0] >= 0.55:
        level = "Lv2"
    elif n >= 3:
        level = "Lv3"
    else:
        level = f"unknown(n={n})"
    return level, n, [round(r, 2) if r is not None else None for r in ratios]


def extract_badge(img, frame, sx, sy):
    x, y, w, h = frame
    y0 = max(0, int(y + BADGE_OFFSETS["top"] * sy))
    y1 = int(y + BADGE_OFFSETS["bottom"] * sy)
    x0 = int(x + w + BADGE_OFFSETS["left"] * sx)
    x1 = int(x + w + BADGE_OFFSETS["right"] * sx)
    region = img[y0:y1, x0:x1]
    gray = cv2.cvtColor(region, cv2.COLOR_BGR2GRAY)
    _, mask = cv2.threshold(gray, 150, 255, cv2.THRESH_BINARY)
    contours, _ = cv2.findContours(mask, cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_SIMPLE)
    if not contours:
        return None
    biggest = max(contours, key=cv2.contourArea)
    bx, by, bw, bh = cv2.boundingRect(biggest)
    badge = region[by:by+bh, bx:bx+bw]
    if badge.size == 0:
        return None
    g = cv2.cvtColor(badge, cv2.COLOR_BGR2GRAY)
    return cv2.resize(g, (32, 32))


def classify_type(badge, ref_buki, ref_bougu):
    if badge is None:
        return "unknown", None, None
    c_buki = cv2.matchTemplate(badge, ref_buki, cv2.TM_CCOEFF_NORMED).max()
    c_bougu = cv2.matchTemplate(badge, ref_bougu, cv2.TM_CCOEFF_NORMED).max()
    t = "buki" if c_buki > c_bougu else "bougu"
    return t, round(float(c_buki), 3), round(float(c_bougu), 3)


def scale_factors(img):
    h, w = img.shape[:2]
    return w / REF_W, h / REF_H


# --- 参照バッジテンプレートの生成 ---
def build_refs():
    # buki ref: 20260614061441 右パネル frames[0]（剣バッジ確認済み）
    img = cv2.imread(os.path.join(SRC, "20260614061441_1.jpg"))
    sx, sy = scale_factors(img)
    y0, y1, x0, x1 = panel_region(img)
    region = img[y0:y1, x0:x1]
    gray = cv2.cvtColor(region, cv2.COLOR_BGR2GRAY)
    frames = detect_frames(gray, sx, sy)
    ref_buki = extract_badge(region, frames[0], sx, sy)

    # bougu ref: 20260614061312 右パネル frame0（兜バッジ・Lv3確認済み）
    img2 = cv2.imread(os.path.join(SRC, "20260614061312_1.jpg"))
    sx2, sy2 = scale_factors(img2)
    y0, y1, x0, x1 = panel_region(img2)
    region2 = img2[y0:y1, x0:x1]
    gray2 = cv2.cvtColor(region2, cv2.COLOR_BGR2GRAY)
    frames2 = detect_frames(gray2, sx2, sy2)
    ref_bougu = extract_badge(region2, frames2[0], sx2, sy2)
    return ref_buki, ref_bougu


def run_panel(label, img, ref_buki, ref_bougu):
    sx, sy = scale_factors(img)
    y0, y1, x0, x1 = panel_region(img)
    region = img[y0:y1, x0:x1]
    gray = cv2.cvtColor(region, cv2.COLOR_BGR2GRAY)
    frames = detect_frames(gray, sx, sy)
    print(f"=== {label} (img {img.shape[1]}x{img.shape[0]}) === region={(y0,y1,x0,x1)} frames={frames}")
    for f in frames:
        level, n, ratios = classify_level(gray, f)
        badge = extract_badge(region, f, sx, sy)
        typ, c_buki, c_bougu = classify_type(badge, ref_buki, ref_bougu)
        print(f"  frame={f}: level={level} (n={n}, ratios={ratios})  type={typ} (buki={c_buki}, bougu={c_bougu})")


if __name__ == "__main__":
    ref_buki, ref_bougu = build_refs()
    cv2.imwrite(os.path.join(TMP, "ref_buki.png"), cv2.resize(ref_buki, (128, 128), interpolation=cv2.INTER_NEAREST))
    cv2.imwrite(os.path.join(TMP, "ref_bougu.png"), cv2.resize(ref_bougu, (128, 128), interpolation=cv2.INTER_NEAREST))

    new_files = [
        "20260614061312_1.jpg",
        "20260614061337_1.jpg",
        "20260614061353_1.jpg",
        "20260614061357_1.jpg",
        "20260614061411_1.jpg",
        "20260614061416_1.jpg",
        "20260614061441_1.jpg",
        "20260614061456_1.jpg",
    ]
    for f in new_files:
        img = cv2.imread(os.path.join(SRC, f))
        run_panel(f, img, ref_buki, ref_bougu)

    img3 = cv2.imread(os.path.join(SRC, "20260614022239_1.jpg"))
    run_panel("20260614022239_1.jpg (img3 right)", img3, ref_buki, ref_bougu)
