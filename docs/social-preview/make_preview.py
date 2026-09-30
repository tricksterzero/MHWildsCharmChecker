"""MHWilds 護石チェッカーの social preview（1280x640）を作る。

構成は CLCLR の docs/social-preview/make_preview.py に揃えている。
アイコンは resources/app-icon.svg を resvg（CLI、PATH上）で描画して使う。
実行: python docs/social-preview/make_preview.py
"""
import subprocess
import tempfile
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

OUT = Path(__file__).parent
ICON_SVG = OUT.parent.parent / "resources" / "app-icon.svg"
FONT_B = r"C:\Windows\Fonts\BIZ-UDGothicB.ttc"
FONT_R = r"C:\Windows\Fonts\BIZ-UDGothicR.ttc"
P_INDEX = 1  # ttc の 2 番目が BIZ UDPゴシック（プロポーショナル）

W, H = 1280, 640
MARGIN_R = 80
BG = (246, 247, 249)
FG = (32, 33, 36)
SUB = (90, 94, 102)
ACCENT = (208, 103, 29)  # アイコン(app-icon.svg)の宝玉の面の銅色


def font(path, size):
    return ImageFont.truetype(path, size, index=P_INDEX)


def fit(d, text, path, size, max_w):
    """max_w に収まるまで文字を小さくする。"""
    while size > 10:
        f = font(path, size)
        if d.textlength(text, font=f) <= max_w:
            return f
        size -= 1
    return font(path, size)


ICON_SIZE = 240
with tempfile.TemporaryDirectory() as tmp:
    png = Path(tmp) / "icon.png"
    subprocess.run(["resvg", "-w", str(ICON_SIZE), "-h", str(ICON_SIZE), str(ICON_SVG), str(png)], check=True)
    icon = Image.open(png).convert("RGBA")

img = Image.new("RGB", (W, H), BG)
d = ImageDraw.Draw(img)

# 左: アイコン
ix, iy = 90, (H - ICON_SIZE) // 2
img.paste(icon, (ix, iy), icon)

# 右: 文字
x = ix + ICON_SIZE + 60
max_w = W - x - MARGIN_R
pre = "MHWilds"
title = "護石チェッカー"
tagline = "モンスターハンターワイルズの護石管理ツール"
sub = "スクショ読み取り・重複チェック・泣シミュとのCSV連携"

f_pre = font(FONT_B, 44)
f_title = fit(d, title, FONT_B, 116, max_w)
f_tag = fit(d, tagline, FONT_B, 42, max_w)
f_sub = fit(d, sub, FONT_R, 32, max_w)

d.text((x + 4, 138), pre, font=f_pre, fill=SUB)
d.text((x, 190), title, font=f_title, fill=FG)
d.rectangle((x + 4, 354, x + 84, 360), fill=ACCENT)
d.text((x + 4, 388), tagline, font=f_tag, fill=FG)
d.text((x + 4, 454), sub, font=f_sub, fill=SUB)

img.save(OUT / "social-preview.png", optimize=True)
print((OUT / "social-preview.png").stat().st_size, "bytes", "title", f_title.size, "tag", f_tag.size, "sub", f_sub.size)
