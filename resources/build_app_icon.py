"""app-icon.svg から app.ico(16/24/32/48/256px)を再生成する。

必要なもの: resvg(CLI、PATH上)、Pillow
実行: python resources/build_app_icon.py
"""
import subprocess
import tempfile
from pathlib import Path
from PIL import Image

HERE = Path(__file__).resolve().parent
SRC = HERE / "app-icon.svg"
ICO = HERE.parent / "app" / "CharmChecker.App" / "app.ico"
SIZES = [256, 48, 32, 24, 16]

with tempfile.TemporaryDirectory() as tmp:
    imgs = []
    # 縮小ではなく各サイズをSVGから直接描画する
    for s in SIZES:
        png = Path(tmp) / f"icon_{s}.png"
        subprocess.run(["resvg", "-w", str(s), "-h", str(s), str(SRC), str(png)], check=True)
        imgs.append(Image.open(png).convert("RGBA"))

    imgs[0].save(ICO, format="ICO", sizes=[(s, s) for s in SIZES], append_images=imgs[1:])

    # 検証: ICO内の各サイズが個別描画したPNGと一致するか
    ico = Image.open(ICO)
    for s, src in zip(SIZES, imgs):
        ico.size = (s, s)
        ico.load()
        if ico.convert("RGBA").tobytes() != src.tobytes():
            raise SystemExit(f"{s}px が描画結果と一致しません")

print(f"生成しました: {ICO}")
