"""Fetch and prepare the third-party assets used by SafeRoute VR (all licence-verified; see CREDITS.md).

  python Tools/Assets/fetch_third_party_assets.py fetch      download raw files into the cache (outside the project)
  python Tools/Assets/fetch_third_party_assets.py process    convert them into Assets/SafeRoute/ThirdParty/<Source>/<Asset>/
  python Tools/Assets/fetch_third_party_assets.py credits    (re)write Assets/SafeRoute/ThirdParty/CREDITS.md

Processing makes the files Unity-ready for URP/Lit in a Linear project: OpenGL (+Y) normal maps only, roughness
converted to a MetallicSmoothness map (R = metallic, A = 1 - roughness), signs and flipbooks resized to VR-sensible
sizes. The scene generator only loads these by path and falls back to its procedural materials when a file is missing.
Requires: numpy, Pillow. Cache: %TEMP%/SafeRouteAssetCache (override with SAFEROUTE_ASSET_CACHE).
"""
import io
import json
import os
import shutil
import sys
import tempfile
import time
import urllib.parse
import urllib.request
import zipfile

import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageFont

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
OUT = os.path.join(ROOT, "Assets", "SafeRoute", "ThirdParty")
CACHE = os.environ.get("SAFEROUTE_ASSET_CACHE", os.path.join(tempfile.gettempdir(), "SafeRouteAssetCache"))
UA = {"User-Agent": "SafeRouteVR-asset-fetch/1.0 (educational VR fire-safety training project)"}

# ---------- Manifest (licences verified against each source's API / licence page on 2026-10-30) ----------

POLYHAVEN_TEXTURES = {  # id: (resolution, real size in metres, use)
    "painted_plaster_wall": ("2k", 2.0, "Corridor and classroom painted walls"),
    "oak_veneer_01": ("1k", 1.83, "Door leaves, teacher's desk top and outdoor benches"),
    "smooth_concrete_floor": ("1k", 2.0, "Stairs, lift alcove, copings and roof (tinted to the scene's grey)"),
    "concrete_pavers": ("1k", 1.92, "Outdoor path to the assembly point"),
}
POLYHAVEN_MODELS = {  # id: (resolution, maps to fetch, use)
    "SchoolDesk_01": ("1k", ["Diffuse", "nor_gl", "Rough", "Metal"], "Classroom 1 desks"),
    "SchoolChair_01": ("1k", ["Diffuse", "nor_gl", "Rough", "Metal"], "Classroom 1 chairs"),
    "korean_fire_extinguisher_01": ("1k", ["body_diff", "body_nor_gl", "body_rough", "body_metal", "glass_diff", "glass_rough",
                                           "glass_opacity", "paper_diff", "paper_opacity", "paper_rough"],
                                    "Corridor fire extinguishers (Korean paper label replaced by an English label)"),
    "fire_alarm": ("1k", ["Diffuse", "nor_gl", "Rough", "Metal"], "Manual call points"),
}
AMBIENTCG = {  # id: (resolution, real size in metres or None = unknown, use)
    "Tiles110": ("2K", 1.85, "Corridor and classroom terrazzo floor"),
    "OfficeCeiling001": ("1K", 3.85, "Suspended T-bar ceiling (tiled at 3.6 m so its 6 x 6 tiles fit the 0.6 m ceiling grid)"),
    "Metal009": ("1K", None, "Brushed steel lift doors and lift metalwork"),
}
# Reviewed in the sourcing audit but not shipped (no material in the scene they fit without new geometry):
# burned_ground_01, PaintedMetal004 (its material is unused), Leaking001/008, SurfaceImperfections014.
NOT_SHIPPED = ["Poly Haven burned_ground_01", "ambientCG PaintedMetal004", "ambientCG Leaking001", "ambientCG Leaking008",
               "ambientCG SurfaceImperfections014", "Unity Labs Flame03, WispySmoke03, DiscSmoke01"]
WIKIMEDIA = {  # file: (licence, author, use)
    "ISO_Exit_-_Left.svg": ("CC0 1.0", "Maxxl2 (arrow) and The Navigators (running man)", "Exit sign pointing left"),
    "ISO_Exit_-_Right.svg": ("CC0 1.0", "Maxxl2 (arrow) and The Navigators (running man)", "Exit sign pointing right"),
    "ISO_Exit_-_Up.svg": ("CC0 1.0", "Maxxl2 (arrow) and The Navigators (running man)", "Exit sign straight ahead / through this door"),
    "ISO_7010_F001.svg": ("Public domain", "MaxxL", "Fire extinguisher sign"),
    "ISO_7010_F005.svg": ("Public domain", "MaxxL", "Fire alarm call point sign"),
    "ISO_7010_E007.svg": ("Public domain", "MaxxL", "Evacuation assembly point sign"),
}
# Flame03, WispySmoke03 and DiscSmoke01 were reviewed too; only the two sheets the scene uses are kept.
UNITY_LABS = ["Flame02", "WispySmoke01"]
UNITY_LABS_USE = {"Flame02": "Flame tongues of all fire patches", "WispySmoke01": "Smoke plumes and the ceiling smoke layer"}
OPENGAMEART = {
    "BurnedWood2": (["https://opengameart.org/sites/default/files/BurnedWood2_S.jpg", "https://opengameart.org/sites/default/files/BurnedWood2_N.jpg"],
                    "https://opengameart.org/content/burned-wood-texture-seamless-texture-with-normalmap-0", "CC-BY 3.0", "Keith333",
                    "Charred wood on burnt debris and boxes"),
}


def get(url, binary=True, retries=4):
    for attempt in range(retries):
        try:
            with urllib.request.urlopen(urllib.request.Request(url, headers=UA), timeout=120) as r:
                data = r.read()
                return data if binary else data.decode("utf-8")
        except urllib.error.HTTPError as e:
            if e.code == 429 and attempt < retries - 1:
                time.sleep(5 * (attempt + 1))
                continue
            raise


def cached(name, url):
    path = os.path.join(CACHE, name)
    if not os.path.exists(path):
        os.makedirs(os.path.dirname(path), exist_ok=True)
        data = get(url)
        open(path, "wb").write(data)
        time.sleep(0.5)
    return path


# ---------- fetch ----------

def fetch():
    os.makedirs(CACHE, exist_ok=True)
    for a, (res, _, _) in POLYHAVEN_TEXTURES.items():
        files = json.loads(get(f"https://api.polyhaven.com/files/{a}", False))
        for m in ["Diffuse", "nor_gl", "Rough", "AO"]:
            if m in files:
                fmt = "jpg" if "jpg" in files[m][res] else "png"
                cached(f"polyhaven/{a}/{m}.{fmt}", files[m][res][fmt]["url"])
    for a, (res, maps, _) in POLYHAVEN_MODELS.items():
        files = json.loads(get(f"https://api.polyhaven.com/files/{a}", False))
        fbx = files["fbx"][res]["fbx"]
        cached(f"polyhaven/{a}/{a}_{res}.fbx", fbx["url"])
        for m in maps:
            fmt = "png" if "png" in files[m][res] else "jpg"
            cached(f"polyhaven/{a}/{m}.{fmt}", files[m][res][fmt]["url"])
    for a, (res, _, _) in AMBIENTCG.items():
        kind = "PNG" if a.startswith("Leaking") or a.startswith("SurfaceImperfections") else "JPG"
        cached(f"ambientcg/{a}_{res}-{kind}.zip", f"https://ambientcg.com/get?file={a}_{res}-{kind}.zip")
    for f in WIKIMEDIA:
        q = "https://commons.wikimedia.org/w/api.php?" + urllib.parse.urlencode(
            {"action": "query", "titles": "File:" + f, "prop": "imageinfo", "iiprop": "url", "iiurlwidth": 1024, "format": "json"})
        page = next(iter(json.loads(get(q, False))["query"]["pages"].values()))
        cached(f"wikimedia/{f}.png", page["imageinfo"][0]["thumburl"])
        time.sleep(1.0)
    for n in UNITY_LABS:
        cached(f"unitylabs/{n}-flipbooks.zip", f"https://unity3d.com/files/labs/downloads/vfx/assets01/{n}/{n}-flipbooks.zip")
    for n, (urls, *_rest) in OPENGAMEART.items():
        for u in urls:
            cached(f"opengameart/{n}/{os.path.basename(u)}", u)
    total = sum(os.path.getsize(os.path.join(d, f)) for d, _, fs in os.walk(CACHE) for f in fs)
    print(f"cache: {CACHE}  ({total / 1e6:.1f} MB)")


# ---------- process helpers ----------

def load(path, mode=None):
    im = Image.open(path)
    im.load()
    return im.convert(mode) if mode else im


def load_gray(path):
    """8-bit greyscale; 16-bit PNGs (Poly Haven opacity maps) are scaled, not clipped."""
    im = load(path)
    if im.mode in ("I;16", "I;16B", "I"):
        a = np.asarray(im, dtype=np.float32)
        return Image.fromarray(np.clip(a / (257.0 if a.max() > 255 else 1.0), 0, 255).astype(np.uint8), "L")
    return im.convert("L")


def save(im, *parts, quality=92):
    path = os.path.join(OUT, *parts)
    os.makedirs(os.path.dirname(path), exist_ok=True)
    if path.lower().endswith(".jpg"):
        im.convert("RGB").save(path, quality=quality, optimize=True)
    else:
        im.save(path, optimize=True)
    return path


def metallic_smoothness(rough_path, metal_path=None, size=None):
    """URP/Lit Metallic Map: R = metallic, G/B unused, A = smoothness (1 - roughness)."""
    rough = np.asarray(load(rough_path, "L"), dtype=np.float32) / 255.0
    metal = np.asarray(load(metal_path, "L"), dtype=np.float32) / 255.0 if metal_path else np.zeros_like(rough)
    if metal.shape != rough.shape:
        metal = np.asarray(Image.fromarray((metal * 255).astype(np.uint8)).resize(rough.shape[::-1], Image.BILINEAR), dtype=np.float32) / 255.0
    rgba = np.zeros(rough.shape + (4,), dtype=np.uint8)
    rgba[..., 0] = (metal * 255).round()
    rgba[..., 3] = ((1.0 - rough) * 255).round()
    im = Image.fromarray(rgba, "RGBA")
    return im.resize((size, size), Image.LANCZOS) if size else im


def zip_member(zpath, suffix):
    with zipfile.ZipFile(zpath) as z:
        names = [n for n in z.namelist() if n.lower().endswith(suffix.lower())]
        if not names:
            return None
        return Image.open(io.BytesIO(z.read(names[0])))


def resized(im, max_side):
    w, h = im.size
    s = min(1.0, max_side / max(w, h))
    return im.resize((max(1, round(w * s)), max(1, round(h * s))), Image.LANCZOS) if s < 1 else im


def font(size, bold=True, hand=False):
    names = ["Inkfree.ttf", "segoepr.ttf"] if hand else (["arialbd.ttf", "segoeuib.ttf"] if bold else ["arial.ttf", "segoeui.ttf"])
    for n in names:
        try:
            return ImageFont.truetype(os.path.join(os.environ.get("WINDIR", "C:/Windows"), "Fonts", n), size)
        except OSError:
            pass
    return ImageFont.load_default()


def centred(draw, box, text, f, fill):
    x0, y0, x1, y1 = box
    l, t, r, b = draw.textbbox((0, 0), text, font=f)
    draw.text(((x0 + x1 - (r - l)) / 2 - l, (y0 + y1 - (b - t)) / 2 - t), text, font=f, fill=fill)


def worn(new, grime, strength=0.55):
    """Multiply new artwork by the scratches/dirt (high-pass luminance) of a text-free patch of the original."""
    g = grime.convert("L").resize(new.size)
    o = np.asarray(g, dtype=np.float32) + 1.0
    base = np.asarray(g.filter(ImageFilter.GaussianBlur(8)), dtype=np.float32) + 1.0
    detail = 1.0 + strength * (np.clip(o / base, 0.4, 1.3) - 1.0)
    n = np.asarray(new.convert("RGB"), dtype=np.float32) * detail[..., None]
    return Image.fromarray(np.clip(n, 0, 255).astype(np.uint8), "RGB")


def english_extinguisher_labels(im, tag):
    """Replace the Korean text on Poly Haven's extinguisher with English, keeping the UV layout (1k maps)."""
    im = im.copy()
    s = im.width / 1024.0
    S = lambda *v: tuple(round(x * s) for x in v)
    if tag == "body":
        # 1) Stencilled band (top-left): paint the letters out with each row's red, then stencil English.
        box = S(18, 28, 316, 104)
        a = np.asarray(im.crop(box), dtype=np.float32)
        red = np.asarray(Image.fromarray((((a[..., 0] - a[..., 1]) > 40) * 255).astype(np.uint8)).filter(ImageFilter.MinFilter(5))) > 0
        fill = a.copy()
        for y in range(a.shape[0]):
            row = a[y][red[y]]
            fill[y] = np.median(row, axis=0) if len(row) else a[y].mean(axis=0)
        band = Image.fromarray(np.where(red[..., None], a, fill).astype(np.uint8), "RGB")
        grime = band.copy()
        d = ImageDraw.Draw(band)
        centred(d, (0, 0, band.width, band.height * 0.62), "FIRE", font(round(30 * s)), (214, 212, 206))
        centred(d, (0, band.height * 0.52, band.width, band.height), "EXTINGUISHER", font(round(24 * s)), (214, 212, 206))
        im.paste(worn(band, grime, 0.6), box[:2])
        # 2) Product sticker (centre-left): English operating label at the same place and size.
        box = S(196, 600, 432, 687)
        w, h = box[2] - box[0], box[3] - box[1]
        st = Image.new("RGB", (w, h), (238, 236, 230))
        d = ImageDraw.Draw(st)
        d.rectangle((0, 0, w - 1, h - 1), outline=(60, 60, 60), width=max(1, round(s)))
        d.rectangle((round(w * 0.02), round(h * 0.06), round(w * 0.98), round(h * 0.36)), fill=(196, 30, 36))
        centred(d, (0, h * 0.06, w, h * 0.36), "FIRE EXTINGUISHER", font(round(20 * s)), (255, 255, 255))
        centred(d, (0, h * 0.38, w, h * 0.56), "ABC DRY POWDER  3.3 kg", font(round(12 * s)), (20, 40, 110))
        centred(d, (0, h * 0.58, w, h * 0.76), "1 PULL PIN  2 AIM  3 SQUEEZE  4 SWEEP", font(round(10 * s)), (20, 20, 20))
        centred(d, (0, h * 0.78, w, h * 0.95), "Aim at the base of the fire", font(round(9 * s), bold=False), (20, 20, 20))
        im.paste(worn(st, im.crop((box[0], box[1] + h + round(14 * s), box[2], box[3] + h + round(14 * s))), 0.9), box[:2])  # plain red paint below
    else:
        # Paper inspection tag (left card): blank it with clean paper from the back card, then an English record.
        box = S(52, 58, 446, 634)
        w, h = box[2] - box[0], box[3] - box[1]
        src = S(548, 390, 548 + 394, 390 + 576)
        card = im.crop(src).resize((w, h))
        d = ImageDraw.Draw(card)
        red, ink, pen = (205, 40, 44), (25, 25, 25), (30, 40, 90)
        lw = max(2, round(3 * s))
        d.rectangle((0, 0, w - 1, h - 1), outline=red, width=round(10 * s))
        d.rectangle((0, 0, w, round(58 * s)), fill=red)
        centred(d, (0, 0, w, 58 * s), "EXTINGUISHER INSPECTION", font(round(26 * s)), (255, 255, 255))
        top = round(70 * s)
        for i, (k, v) in enumerate([("Year", "2026"), ("Location", "Corridor 1F"), ("Type", "ABC powder")]):
            y = top + i * round(44 * s)
            d.rectangle((round(14 * s), y, w - round(14 * s), y + round(44 * s)), outline=ink, width=lw // 2 or 1)
            d.text((round(24 * s), y + round(10 * s)), k, font=font(round(19 * s)), fill=ink)
            d.text((round(150 * s), y + round(6 * s)), v, font=font(round(24 * s), hand=True), fill=pen)
        y0 = top + round(146 * s)
        cols = [14, 110, 230, 300, w / s - 14]
        heads = ["Date", "Inspector", "OK", "Checks"]
        rows = [("14/01", "J. Park"), ("11/02", "A. Rao"), ("13/03", "J. Park"), ("15/04", "M. Chen"),
                ("12/05", "A. Rao"), ("16/06", "J. Park"), ("14/07", "M. Chen"), ("", ""), ("", "")]
        rh = round(36 * s)
        for r in range(len(rows) + 1):
            y = y0 + r * rh
            d.line((round(14 * s), y, round(300 * s), y), fill=ink, width=1)
        d.line((round(14 * s), y0 + (len(rows) + 1) * rh, round(300 * s), y0 + (len(rows) + 1) * rh), fill=ink, width=1)
        for cx in cols[:4]:
            d.line((round(cx * s), y0, round(cx * s), y0 + (len(rows) + 1) * rh), fill=ink, width=1)
        for i, hd in enumerate(heads[:3]):
            centred(d, (cols[i] * s, y0, cols[i + 1] * s, y0 + rh), hd, font(round(17 * s)), ink)
        for r, (dt, who) in enumerate(rows):
            y = y0 + (r + 1) * rh
            if dt:
                centred(d, (cols[0] * s, y, cols[1] * s, y + rh), dt, font(round(22 * s), hand=True), pen)
                centred(d, (cols[1] * s, y, cols[2] * s, y + rh), who, font(round(20 * s), hand=True), pen)
                centred(d, (cols[2] * s, y, cols[3] * s, y + rh), "OK", font(round(20 * s), hand=True), pen)
        d.text((round(312 * s), y0 + round(8 * s)), "Checks", font=font(round(17 * s)), fill=ink)
        for i, t in enumerate(["Location", "Access", "Signage", "Gauge", "Pin & seal", "Hose", "Body", "Weight"]):
            d.text((round(312 * s), y0 + rh + round(10 * s) + i * round(34 * s)), "• " + t, font=font(round(15 * s), bold=False), fill=ink)
        im.paste(card, box[:2])  # the back card already carries clean paper grime
    return im


def pictogram_mask(im):
    """White pictogram strokes of a safety sign as alpha (white RGB), cropped to the coloured panel inside the border."""
    a = np.asarray(im.convert("RGB"), dtype=np.float32)
    coloured = (a.max(-1) - a.min(-1)) > 60
    ys, xs = np.nonzero(coloured)
    x0, x1, y0, y1 = xs.min(), xs.max() + 1, ys.min(), ys.max() + 1
    a = a[y0:y1, x0:x1]
    white = np.clip((a.min(-1) - 90.0) / 120.0, 0, 1)
    out = np.zeros(a.shape[:2] + (4,), dtype=np.uint8)
    out[..., :3] = 255
    out[..., 3] = (white * 255).round()
    return Image.fromarray(out, "RGBA")


def pad4(im):
    """Centre on a transparent canvas whose sides are multiples of 4, so Unity can block-compress it (DXT/BC)."""
    w, h = (im.width + 3) // 4 * 4, (im.height + 3) // 4 * 4
    if (w, h) == im.size:
        return im
    out = Image.new("RGBA", (w, h), (255, 255, 255, 0))
    out.paste(im, ((w - im.width) // 2, (h - im.height) // 2))
    return out


def square_cells(im, gx, gy, n):
    """Repack a gx x gy sheet of tall cells into an n x n sheet of square cells (frame order kept, row-major)."""
    cw, ch = im.width // gx, im.height // gy
    side = max(cw, ch)
    out = Image.new("RGBA", (side * n, side * n), (0, 0, 0, 0))
    for i in range(gx * gy):
        cell = im.crop(((i % gx) * cw, (i // gx) * ch, (i % gx + 1) * cw, (i // gx + 1) * ch))
        out.paste(cell, ((i % n) * side + (side - cw) // 2, (i // n) * side + (side - ch) // 2))
    return out


# ---------- process ----------

def process():
    os.makedirs(OUT, exist_ok=True)
    c = lambda *p: os.path.join(CACHE, *p)
    # Poly Haven surfaces
    for a in POLYHAVEN_TEXTURES:
        d = os.path.join("PolyHaven", a)
        os.makedirs(os.path.join(OUT, d), exist_ok=True)
        if a == "smooth_concrete_floor":
            # The photo is a warm-stained polished floor; cast concrete in the scene is grey, so keep only its luminance
            # (tinting brown to grey per channel would multiply the blue channel's noise ~12x), and lift it in linear
            # light to a mean albedo of 0.2 (the photo averages 0.055, darker than any concrete in the scene).
            grey = np.asarray(load(c("polyhaven", a, "Diffuse.jpg"), "L"), dtype=np.float32) / 255.0
            lin = np.where(grey <= 0.04045, grey / 12.92, ((grey + 0.055) / 1.055) ** 2.4)
            lin = np.clip(lin * (0.2 / lin.mean()), 0, 1)
            grey = np.where(lin <= 0.0031308, lin * 12.92, 1.055 * lin ** (1 / 2.4) - 0.055)
            save(Image.fromarray((grey * 255).round().astype(np.uint8), "L").convert("RGB"), d, f"{a}_Color.jpg")
        else:
            shutil.copyfile(c("polyhaven", a, "Diffuse.jpg"), os.path.join(OUT, d, f"{a}_Color.jpg"))
        shutil.copyfile(c("polyhaven", a, "nor_gl.jpg"), os.path.join(OUT, d, f"{a}_NormalGL.jpg"))
        save(metallic_smoothness(c("polyhaven", a, "Rough.jpg")), d, f"{a}_MS.png")
        if os.path.exists(c("polyhaven", a, "AO.jpg")):
            shutil.copyfile(c("polyhaven", a, "AO.jpg"), os.path.join(OUT, d, f"{a}_AO.jpg"))
    # Poly Haven models (FBX + our own converted maps; the generator builds URP materials from them)
    for a, (res, maps, _) in POLYHAVEN_MODELS.items():
        d = os.path.join("PolyHaven", a)
        os.makedirs(os.path.join(OUT, d), exist_ok=True)
        shutil.copyfile(c("polyhaven", a, f"{a}_{res}.fbx"), os.path.join(OUT, d, f"{a}.fbx"))
        def mp(name):
            for ext in ("png", "jpg"):
                p = c("polyhaven", a, f"{name}.{ext}")
                if os.path.exists(p):
                    return p
            return None
        for prefix in ["", "body_", "glass_", "paper_"]:
            diff = mp(prefix + ("Diffuse" if prefix == "" else "diff"))
            if not diff:
                continue
            tag = prefix.rstrip("_") or "main"
            color = load(diff, "RGB")
            if a == "korean_fire_extinguisher_01" and tag in ("body", "paper"):
                color = english_extinguisher_labels(color, tag)
            opacity = mp(prefix + "opacity")
            if opacity:
                # URP Lit reads coverage from the base map alpha: glass (transparent) and the paper tag (alpha-tested).
                color = color.convert("RGBA")
                color.putalpha(load_gray(opacity).resize(color.size))
                save(color, d, f"{a}_{tag}_Color.png")
            else:
                save(color, d, f"{a}_{tag}_Color.jpg")
            nor = mp(prefix + "nor_gl")
            if nor:
                save(load(nor, "RGB"), d, f"{a}_{tag}_NormalGL.png")
            rough = mp(prefix + ("Rough" if prefix == "" else "rough"))
            if rough:
                save(metallic_smoothness(rough, mp(prefix + ("Metal" if prefix == "" else "metal"))), d, f"{a}_{tag}_MS.png")
    # ambientCG (ZIPs): Color, NormalGL, Roughness (+Metalness) -> MS; decal Opacity -> mask
    for a, (res, _, _) in AMBIENTCG.items():
        kind = "PNG" if a.startswith("Leaking") or a.startswith("SurfaceImperfections") else "JPG"
        z = c("ambientcg", f"{a}_{res}-{kind}.zip")
        d = os.path.join("ambientCG", a)
        ext = "." + kind.lower()
        color = zip_member(z, "_Color" + ext)
        if color is not None:
            save(color.convert("RGB"), d, f"{a}_Color.jpg")
        nor = zip_member(z, "_NormalGL" + ext)
        if nor is not None:
            save(nor.convert("RGB"), d, f"{a}_NormalGL.png" if kind == "PNG" else f"{a}_NormalGL.jpg")
        rough = zip_member(z, "_Roughness" + ext)
        metal = zip_member(z, "_Metalness" + ext)
        if rough is not None:
            tmp_r = os.path.join(CACHE, "_tmp_rough.png"); rough.convert("L").save(tmp_r)
            tmp_m = None
            if metal is not None:
                tmp_m = os.path.join(CACHE, "_tmp_metal.png"); metal.convert("L").save(tmp_m)
            save(metallic_smoothness(tmp_r, tmp_m), d, f"{a}_MS.png")
        opacity = zip_member(z, "_Opacity" + ext)
        if opacity is not None:
            save(opacity.convert("L"), d, f"{a}_Mask.png")
        ao = zip_member(z, "_AmbientOcclusion" + ext)
        if ao is not None:
            save(ao.convert("L"), d, f"{a}_AO.jpg")
    # Wikimedia signs: keep the official artwork untouched (no mirroring); VR-sized. Plus a white-on-transparent mask of
    # the pictogram (border trimmed to the coloured panel) so the generator can lay it on its own lit sign field.
    for f in WIKIMEDIA:
        im = load(c("wikimedia", f + ".png"), "RGBA")
        out = f.replace(".svg", "")
        save(resized(im, 1024 if f.startswith("ISO_Exit") else 512), "Wikimedia", out + ".png")
        save(pad4(resized(pictogram_mask(im), 1024 if f.startswith("ISO_Exit") else 512)), "Wikimedia", out + "_Mask.png")
    # Unity Labs flipbooks: the 8-bit TGA sheets (not the EXR copies). Tall 16x4 sheets are repacked into an 8x8 grid
    # of square cells (each frame centred on a transparent square, same frame order) because FireSpread scales a
    # uniform particle size; square sheets are kept as they are.
    for n in UNITY_LABS:
        z = c("unitylabs", f"{n}-flipbooks.zip")
        with zipfile.ZipFile(z) as zz:
            for m in [m for m in zz.namelist() if m.lower().endswith(".tga")]:
                im = Image.open(io.BytesIO(zz.read(m))).convert("RGBA")
                name = os.path.splitext(os.path.basename(m))[0]
                if "16x4" in name:
                    im, name = square_cells(resized(im, 1024), 16, 4, 8), name.replace("16x4", "8x8")
                save(resized(im, 1024), "UnityLabs", n, name + ".png")
    # OpenGameArt burned wood (CC-BY): 2048x1169 -> 1024x585 (aspect kept, still tiles).
    for n, (urls, *_r) in OPENGAMEART.items():
        for u in urls:
            im = load(c("opengameart", n, os.path.basename(u)), "RGB")
            suffix = "Color" if u.endswith("_S.jpg") else "NormalGL"
            save(resized(im, 1024), "OpenGameArt", n, f"{n}_{suffix}.jpg")
    total = sum(os.path.getsize(os.path.join(dd, f)) for dd, _, fs in os.walk(OUT) for f in fs)
    print(f"processed into {OUT}  ({total / 1e6:.1f} MB on disk)")


def credits():
    lines = ["# SafeRoute VR - third-party assets", "",
             "All files below were downloaded by `Tools/Assets/fetch_third_party_assets.py` and converted for Unity URP",
             "(OpenGL normal maps; roughness converted to a MetallicSmoothness map; signs and flipbooks resized).",
             "Licences were verified on 2026-10-30 against each source's API or licence page. The scene generator falls",
             "back to its procedural materials and props when any of these files is missing.", "",
             "| Asset | Source | Licence | Attribution | Used for | Files |", "|---|---|---|---|---|---|"]
    for a, (res, size, use) in POLYHAVEN_TEXTURES.items():
        lines.append(f"| {a} | https://polyhaven.com/a/{a} | CC0 1.0 | Not required | {use} | PolyHaven/{a}/ ({res}, {size} m tile) |")
    for a, (res, _, use) in POLYHAVEN_MODELS.items():
        lines.append(f"| {a} (model) | https://polyhaven.com/a/{a} | CC0 1.0 | Not required | {use} | PolyHaven/{a}/ (FBX, {res} maps) |")
    for a, (res, size, use) in AMBIENTCG.items():
        lines.append(f"| {a} | https://ambientcg.com/view?id={a} | CC0 1.0 | Not required | {use} | ambientCG/{a}/ ({res}; real size {f'{size} m' if size else 'not published, tiled by eye'}) |")
    for f, (lic, author, use) in WIKIMEDIA.items():
        lines.append(f"| {f} | https://commons.wikimedia.org/wiki/File:{f} | {lic} | Not required (author: {author}) | {use} | Wikimedia/{f.replace('.svg', '.png')} |")
    for n in UNITY_LABS:
        lines.append(f"| {n} flipbook | https://unity.com/blog/engine-platform/free-vfx-image-sequences-flipbooks | CC0 1.0 | Not required (Unity Technologies, Unity Labs) | {UNITY_LABS_USE[n]} | UnityLabs/{n}/ |")
    for n, (_u, page, lic, author, use) in OPENGAMEART.items():
        lines.append(f"| {n} | {page} | {lic} | **Required:** \"Burned Wood Texture\" by {author}, {page}, CC-BY 3.0 | {use} | OpenGameArt/{n}/ |")
    lines += ["", "## Changes made to the original files",
              "- korean_fire_extinguisher_01: the Korean text on the inspection tag, the stencilled band and the product sticker",
              "  was replaced by English artwork (inspection record, FIRE EXTINGUISHER, ABC dry powder / PULL-AIM-SQUEEZE-SWEEP).",
              "- smooth_concrete_floor: colour map desaturated to grey and lifted to a 0.2 mean albedo (cast concrete).",
              "- BurnedWood2 (CC-BY 3.0): resized from 2048 x 1169 to 1024 x 584; otherwise unchanged.",
              "- Flame02: the 16 x 4 sheet of tall frames repacked, frame order unchanged, into an 8 x 8 sheet of square frames.",
              "- ISO signs: *_Mask.png files are the white pictogram strokes of the official artwork as an alpha mask (border",
              "  trimmed, padded to a multiple of 4 px); the artwork itself is not altered or mirrored.",
              "- All roughness maps were converted to Unity MetallicSmoothness maps (R = metallic, A = 1 - roughness).",
              "", "## Excluded on purpose",
              "- ISO 7010 P020 (do not use lift in case of fire): Wikimedia lists it as public domain, but the author field is 'ISO' on a",
              "  self-published release, so the redistribution rights are unclear. The lift keeps its own text notice.",
              "- Poly Haven cardboard_box_01: about 17k triangles, over the VR budget for a small prop.",
              "- Reviewed and licence-checked but not shipped (no fitting material in the scene): " + "; ".join(NOT_SHIPPED) + ".",
              "- Sketchfab, Kenney and Unity Asset Store items: manual downloads, skipped for now."]
    path = os.path.join(OUT, "CREDITS.md")
    os.makedirs(OUT, exist_ok=True)
    open(path, "w", encoding="utf-8").write("\n".join(lines) + "\n")
    print("wrote", path)


if __name__ == "__main__":
    {"fetch": fetch, "process": process, "credits": credits}[sys.argv[1] if len(sys.argv) > 1 else "fetch"]()
