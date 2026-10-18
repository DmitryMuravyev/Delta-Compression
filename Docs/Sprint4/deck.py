# Builds "Sprint 4: Mathematical foundation" in the style of the sprint 1-3 decks (960 x 540 pt slides).
import sys
import numpy as np
import matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt
from matplotlib.backends.backend_pdf import PdfPages
from matplotlib.patches import Rectangle, Circle, Wedge, FancyArrowPatch
from model import dp, greedy, methods

plt.rcParams.update({
    "font.family": "Liberation Sans",
    "mathtext.fontset": "dejavusans",
    "pdf.fonttype": 42,
})

W, H = 960, 540
GREEN, RED, ORANGE, BLUE = "#c9f5c9", "#ffc4c4", "#f7a541", "#1f5fbf"
GRAY = "#d9d9d9"
RC, GC, BC = "#f08080", "#93d293", "#6a9ff0"
OUT = sys.argv[1] if len(sys.argv) > 1 else "Sprint_4_Mathematical_foundation.pdf"


def slide(title=None):
    fig = plt.figure(figsize=(W / 72, H / 72))
    ax = fig.add_axes([0, 0, 1, 1])
    ax.set_xlim(0, W); ax.set_ylim(H, 0); ax.axis("off")
    if title:
        ax.text(22, 20, title, fontsize=15, fontweight="bold", va="top", ha="left",
                bbox=dict(boxstyle="square,pad=0.3", fc=GRAY, ec="none"))
    return fig, ax


def T(ax, x, y, s, size=15, **kw):
    kw.setdefault("va", "top"); kw.setdefault("ha", "left")
    return ax.text(x, y, s, fontsize=size, **kw)


def bullets(ax, x, y, items, size=15, gap=27):
    for it in items:
        if it is None:
            y += gap // 2; continue
        indent = 0
        while it.startswith(">"):
            indent += 1; it = it[1:]
        mark = "•" if indent == 0 else "–"
        T(ax, x + indent * 22, y, mark, size=size)
        T(ax, x + indent * 22 + 16, y, it, size=size)
        y += gap
    return y


def cells(ax, x, y, values, fills, cw=34, ch=26, size=13, bold=False, top=None, top_size=13):
    for i, v in enumerate(values):
        ax.add_patch(Rectangle((x + i * cw, y), cw, ch, fc=fills[i], ec="white", lw=1.2))
        ax.text(x + i * cw + cw / 2, y + ch / 2, str(v), fontsize=size, ha="center", va="center",
                fontweight="bold" if bold else "normal")
        if i > 0:
            ax.plot([x + i * cw, x + i * cw], [y + ch - 7, y + ch], color=BLUE, lw=1.2)
    if top is not None:
        for i, v in enumerate(top):
            if v is None: continue
            ax.text(x + i * cw + cw / 2, y - 4, str(v), fontsize=top_size, ha="center", va="bottom", fontweight="bold")


def table(ax, x, y, rows, colw, rowh=24, size=12, header_fill=GRAY, fills=None, aligns=None):
    for r, row in enumerate(rows):
        cx = x
        for c, val in enumerate(row):
            fc = header_fill if r == 0 else (fills[r][c] if fills and fills[r] and fills[r][c] else "white")
            ax.add_patch(Rectangle((cx, y + r * rowh), colw[c], rowh, fc=fc, ec="black", lw=0.6))
            al = aligns[c] if aligns else "center"
            tx = cx + colw[c] / 2 if al == "center" else cx + 6
            ax.text(tx, y + r * rowh + rowh / 2, val, fontsize=size, ha=al, va="center",
                    fontweight="bold" if r == 0 else "normal")
            cx += colw[c]
    return y + len(rows) * rowh


def note_box(ax, x, y, w, h, s, size=13, fc="#fff6d6"):
    ax.add_patch(Rectangle((x, y), w, h, fc=fc, ec="#e0c060", lw=0.8))
    T(ax, x + 10, y + 8, s, size=size, linespacing=1.45)


def chart_axes(fig, x, y, w, h):
    a = fig.add_axes([x / W, 1 - (y + h) / H, w / W, h / H])
    for s in ("top", "right"): a.spines[s].set_visible(False)
    a.tick_params(labelsize=10)
    a.grid(axis="y", color="#dddddd", lw=0.6); a.set_axisbelow(True)
    return a


pages = []
plt.rcParams["figure.max_open_warning"] = 0

# ------------------------------------------------------------------------------------------------
# Title
fig, ax = slide()
grad = np.linspace(0, 1, 512)[None, :]
cmap = matplotlib.colors.LinearSegmentedColormap.from_list("band", ["#2fbf2f", "#21b0a0", "#1f7fe0"])
ax.imshow(grad, extent=(0, W, 150, 0), cmap=cmap, aspect="auto", zorder=0)
gray = np.linspace(1, 0, 256)[:, None]
cmap2 = matplotlib.colors.LinearSegmentedColormap.from_list("bg", ["#ffffff", "#e6e6e6"])
ax.imshow(gray, extent=(0, W, H, 150), cmap=cmap2, aspect="auto", zorder=0)
# a light "circuit" texture on the band
rng = np.random.default_rng(4)
for _ in range(70):
    x0, y0 = rng.uniform(260, W), rng.uniform(8, 142)
    dx = rng.choice([-1, 1]) * rng.uniform(20, 90)
    ax.plot([x0, x0 + dx, x0 + dx], [y0, y0, y0 + rng.uniform(-25, 25)], color="white", alpha=0.18, lw=1.2, zorder=1)
    ax.add_patch(Circle((x0, y0), 2.2, fc="white", alpha=0.25, ec="none", zorder=1))
ax.set_xlim(0, W); ax.set_ylim(H, 0)
cx, cy, r = 150, 140, 105
ring = matplotlib.colors.LinearSegmentedColormap.from_list("ring", ["#22c55e", "#10b9a6", "#0ea5e9", "#22c55e"])
for a in range(0, 360, 2):
    ax.add_patch(Wedge((cx, cy), r, a, a + 2.5, width=16, fc=ring(a / 360), ec="none", zorder=3))
ax.add_patch(Circle((cx, cy), r - 15, fc="black", ec="none", zorder=3))
ax.add_patch(Circle((cx, cy), r + 4, fc="none", ec="white", lw=3, zorder=3))
ax.text(cx, cy + 4, "DM", fontsize=82, fontweight="bold", color="white", ha="center", va="center", zorder=4)
ax.text(290, 58, "Dmitry Muravyev", fontsize=34, fontweight="bold", color="white", va="center", zorder=4)
T(ax, 140, 285, "Delta encoding", size=58)
T(ax, 146, 380, "Sprint 4: Mathematical foundation", size=34)
pages.append(fig)

# ------------------------------------------------------------------------------------------------
fig, ax = slide("Notation")
rows = [
    ("Symbol", "Meaning"),
    (r"$N$", "number of frames (pixels, samples)"),
    (r"$C$", "number of channels"),
    (r"$x_{c,i}$", "value of channel c in frame i,  0 ≤ i < N"),
    (r"$k_c$", "bits per channel c (target bit depth)"),
    (r"$F=\sum_c k_c$", "bits per frame"),
    (r"$b$", "block size field (bits); block length 1 ≤ l ≤ 2^b"),
    (r"$m,\ w$", "method and window width (bits)"),
    (r"$M_c,\ d_c$", "number of methods and bits per method declaration"),
    (r"$H=b+\sum_c d_c$", "header size of a block"),
    (r"$L_{c,m}(p)$", "longest block from frame p for which method m of channel c works"),
    (r"$\mathrm{Bits}(p,l)$", "size of the cheapest block of length l starting at frame p"),
    (r"$\mathrm{Best}(p)$", "size of the cheapest chain of blocks covering frames p … N−1"),
]
table(ax, 60, 70, rows, [190, 640], rowh=31, size=14, aligns=["center", "left"])
T(ax, 60, 490, "All sizes are in bits.", size=13, style="italic")
pages.append(fig)

# ------------------------------------------------------------------------------------------------
fig, ax = slide("Frames and squares")
bullets(ax, 40, 70, ["A frame is the set of all channel values of one pixel (sample):",
                     ">$x_i=(x_{0,i},\\ x_{1,i},\\ \\ldots,\\ x_{C-1,i})$,   e.g. RGB 565: $k=(5,6,5),\\ F=16$",
                     None,
                     "Splitting into s × s squares (s divides the image width W and height):",
                     ">frame i is taken from pixel π(i):"], gap=30)
T(ax, 90, 238, r"$q=\lfloor i/s^2\rfloor,\quad o=i\ \mathrm{mod}\ s^2,\quad r=\lfloor q/(W/s)\rfloor,\quad u=q\ \mathrm{mod}\ (W/s)$", size=17)
T(ax, 90, 285, r"$\pi(i)=(r\cdot s+\lfloor o/s\rfloor)\cdot W+u\cdot s+(o\ \mathrm{mod}\ s)$", size=19)
# small picture: 2x2 squares order on a 8x4 grid
gx, gy, cw = 560, 340, 34
order = {}
for i in range(32):
    q, o = i // 4, i % 4
    rr, u = q // 4, q % 4
    px = (rr * 2 + o // 2) * 8 + u * 2 + o % 2
    order[px] = i
for p in range(32):
    rr, cc = p // 8, p % 8
    sq = ((rr // 2) * 4 + cc // 2)
    ax.add_patch(Rectangle((gx + cc * cw, gy + rr * cw), cw, cw, fc=[GREEN, "#bfe0ff", "#ffe2b0", RED][sq % 4], ec="white"))
    ax.text(gx + cc * cw + cw / 2, gy + rr * cw + cw / 2, str(order[p]), ha="center", va="center", fontsize=11)
T(ax, gx, gy + 4 * cw + 10, "Frame numbers for 2 × 2 squares, W = 8", size=12)
T(ax, 40, 350, "The squares keep neighboring pixels close in the frame\nsequence, so the deltas are smaller.", size=14, linespacing=1.5)
T(ax, 40, 420, "s = 1 means no splitting: π(i) = i.", size=14)
pages.append(fig)

# ------------------------------------------------------------------------------------------------
fig, ax = slide("Block format")
segs = [("Block frames\nl − 1", "b bits", 120), ("Method\nChannel 0", "d₀ bits", 100), ("...", "", 50),
        ("Method\nChannel C−1", "d_{C−1}", 110), ("Initial value\nChannel 0", "k₀ bits*", 120), ("...", "", 50),
        ("Initial value\nChannel C−1", "k_{C−1} bits*", 130), ("Data\n...", "", 150)]
x = 40
for name, bits, w_ in segs:
    ax.add_patch(Rectangle((x, 70), w_, 50, fc="#eaf2ff" if name != "..." else "white", ec="black", lw=0.8))
    ax.text(x + w_ / 2, 95, name, ha="center", va="center", fontsize=12)
    ax.text(x + w_ / 2, 128, bits, ha="center", va="top", fontsize=11, color="#444")
    x += w_
T(ax, 40, 160, "* the initial value is omitted for an uncompressed channel (m = k).", size=12, style="italic")
T(ax, 40, 200, r"$d_c=\lfloor\log_2(M_c-1)\rfloor+1,\qquad M_c=\{k_c+1$ (single Δ option)$,\ \ 2k_c$ (two Δ options)$\}$", size=17)
T(ax, 40, 245, r"$H=b+\sum_{c=0}^{C-1} d_c$", size=19)
rows = [("Channel bits k", "2", "3", "4", "5", "6", "7", "8")]
rows.append(("d, single Δ option",) + tuple(str(int(np.floor(np.log2(k))) + 1) for k in range(2, 9)))
rows.append(("d, two Δ options",) + tuple(str(int(np.floor(np.log2(2 * k - 1))) + 1) for k in range(2, 9)))
table(ax, 40, 300, rows, [170] + [52] * 7, rowh=28, size=13)
note_box(ax, 610, 300, 320, 110,
         "Block length is stored as l − 1,\nso b bits give blocks of 1 … 2^b frames.\nEvery block is self-contained: the decoder\nneeds no state from the previous block.", size=13)
pages.append(fig)

# ------------------------------------------------------------------------------------------------
fig, ax = slide("Methods")
rows = [("m", "Fixed window only", "Adaptive window only", "Fixed window first", "Adaptive window first"),
        ("0", "Fixed, w = 0", "Fixed, w = 0", "Fixed, w = 0", "Fixed, w = 0"),
        ("1 … k−1", "Fixed, w = m", "Adaptive, w = m", "Fixed, w = m", "Adaptive, w = m"),
        ("k", "Uncompressed", "Uncompressed", "Uncompressed", "Uncompressed"),
        ("k+1 … 2k−1", "—", "—", "Adaptive, w = m − k", "Fixed, w = m − k")]
table(ax, 40, 66, rows, [110, 195, 195, 195, 195], rowh=30, size=13)
T(ax, 40, 236, "Cost of a channel in a block of l frames (bits, without the header):", size=15)
T(ax, 70, 272, r"Fixed window:            $\mathrm{cost}=k+l\cdot w$          (initial value = minimum of the block)", size=16)
T(ax, 70, 308, r"Adaptive floating window:  $\mathrm{cost}=k+(l-1)\cdot w$     (initial value = first frame)", size=16)
T(ax, 70, 344, r"Uncompressed:            $\mathrm{cost}=l\cdot k$", size=16)
T(ax, 70, 380, r"All frames equal (Fixed, w = 0):  $\mathrm{cost}=k$", size=16)
note_box(ax, 40, 430, 880, 62,
         "The Δ option is a property of the whole stream: the compressor and the decoder (#define in Decompression.h)\nmust use the same one.", size=13)
pages.append(fig)

# ------------------------------------------------------------------------------------------------
fig, ax = slide("Fixed window")
T(ax, 40, 66, "Method with window width w is applicable to frames p … p+l−1 if", size=15)
T(ax, 80, 98, r"$\max_{p\leq i<p+l} x_i-\min_{p\leq i<p+l} x_i\ \leq\ 2^w-1$", size=19)
T(ax, 40, 140, "Initial value  v = min,  data  x_i − v  in w bits.", size=15)
T(ax, 40, 172, r"$L^F_w(p)=\max\{\,l\leq\min(2^b,\ N-p)\ :\ $range of frames $p\ldots p+l-1\ \leq 2^w-1\,\}$", size=16)
vals = [100, 110, 120, 110, 115, 120, 130, 140, 150, 160, 170, 180, 190, 200, 210, 220]
data = [100] + [v - 100 for v in vals]
fills = [ORANGE] + [GREEN if v - 100 <= 31 else RED for v in vals]
cells(ax, 40, 260, data, fills, cw=40, ch=28, size=14, bold=True, top=[None] + vals, top_size=14)
T(ax, 40, 300, "w = 5 (0 ~ 31), block length: 7", size=14)
vals2 = [103, 114, 118, 107, 102, 131, 119, 128, 127, 115, 110, 130, 100, 101, 122, 125]
data2 = [100] + [v - 100 for v in vals2]
cells(ax, 40, 370, data2, [ORANGE] + [GREEN] * 16, cw=40, ch=28, size=14, bold=True, top=[None] + vals2, top_size=14)
T(ax, 40, 410, "w = 5, block length: 16", size=14)
T(ax, 40, 460, "Example cost (k = 8, w = 5, l = 16):  8 + 16 × 5 = 88 bits instead of 128 bits.", size=14)
pages.append(fig)

# ------------------------------------------------------------------------------------------------
fig, ax = slide("Adaptive floating window")
T(ax, 40, 66, "Window of width 2^w around the previous frame, clamped to the value range 0 … 2^k − 1:", size=15)
T(ax, 80, 100, r"$s_w(y)=\min\left(\max\left(y-2^{w-1},\ 0\right),\ 2^k-2^w\right)$", size=19)
T(ax, 40, 146, r"Frame $x_i$ fits if $s_w(x_{i-1})\leq x_i<s_w(x_{i-1})+2^w$,  data  $x_i-s_w(x_{i-1})$  in w bits.", size=15)
T(ax, 40, 180, r"$L^A_w(p)=\max\{\,l\leq\min(2^b,\ N-p)\ :\ $every frame $p+1\ldots p+l-1$ fits$\,\}$,  initial value $=x_p$", size=15)
vals = [100, 110, 120, 110, 115, 120, 130, 140, 150, 160, 170, 180, 190, 200, 210, 220]
ss = [None]
dd = [100]
for i in range(1, 16):
    s_ = max(0, min(vals[i - 1] - 16, 256 - 32)); ss.append(s_); dd.append(vals[i] - s_)
cells(ax, 40, 262, dd, [ORANGE] + [GREEN] * 15, cw=44, ch=28, size=14, bold=True, top=vals, top_size=14)
for i, s_ in enumerate(ss):
    if s_ is not None:
        ax.text(40 + i * 44 + 22, 300, str(s_), ha="center", va="top", fontsize=11, color="#555")
T(ax, 6, 300, "s:", size=11, color="#555")
T(ax, 40, 330, "w = 5, block length: 16.  Cost: 8 + 15 × 5 = 83 bits.", size=14)
note_box(ax, 40, 380, 880, 92,
         "The window follows the source data (the previous frame is the original value, the compression is lossless),\n"
         "so the position of the window does not depend on w. This is what makes the windows nested (Lemma 1).\n"
         "Clamping keeps the whole window inside the value range, so no window bits are wasted at 0 and 2^k − 1.", size=13)
pages.append(fig)

# ------------------------------------------------------------------------------------------------
fig, ax = slide("Block size and efficiency")
T(ax, 40, 70, r"$\mathrm{Bits}(B)=H+\sum_{c=0}^{C-1}\mathrm{cost}_c(m_c,\ l)$", size=21)
T(ax, 40, 146, r"Method $m_c$ may be used only if $l\leq L_{c,m_c}(p)$.", size=16)
T(ax, 40, 175, r"$E_{block}=\left(1-\frac{\mathrm{Bits}(B)}{l\cdot F}\right)\times 100\%,\qquad E_{overall}=\left(1-\frac{\sum_j \mathrm{Bits}(B_j)}{N\cdot F}\right)\times 100\%$", size=19)
T(ax, 40, 250, "The output file is the chain of blocks B₀, B₁, … covering all N frames, padded to whole bytes:", size=15)
T(ax, 80, 285, r"$\mathrm{bytes}=\left\lceil\frac{1}{8}\sum_j \mathrm{Bits}(B_j)\right\rceil$", size=19)
rows = [("Window bits", "Max eff")] + [(str(w), f"{(1 - w / 8) * 100:.1f}%") for w in range(0, 9)]
table(ax, 700, 230, rows, [110, 90], rowh=24, size=12)
T(ax, 40, 360, "Max eff for k = 8: the data part of a block can't be smaller than w/k of the raw data.", size=13, style="italic")
pages.append(fig)

# ------------------------------------------------------------------------------------------------
fig, ax = slide("Lemma 1: nested windows")
T(ax, 40, 66, "For every start frame p and channel:", size=15)
T(ax, 80, 98, r"$L^F_0(p)\leq L^F_1(p)\leq\ldots\leq L^F_{k-1}(p),\qquad L^A_1(p)\leq L^A_2(p)\leq\ldots\leq L^A_{k-1}(p)$", size=18)
bullets(ax, 40, 150, [
    "Fixed: the condition range ≤ 2^w − 1 only gets weaker as w grows.",
    "Adaptive: for the same previous frame y the window of width 2^(w+1) contains the window of width 2^w:",
    r">$s_{w+1}(y)\leq s_w(y)$  and  $s_w(y)+2^w\leq s_{w+1}(y)+2^{w+1}$",
    ">without clamping: y − 2^w ≤ y − 2^(w−1) and y + 2^(w−1) ≤ y + 2^w;",
    ">clamping at 0 or 2^k − 2^w moves both windows the same way (case analysis,",
    ">also checked exhaustively for all y, w and k ≤ 12).",
    "The previous frame is the source value, the same for all w, so the nesting holds frame by frame.",
], size=14, gap=30)
y0 = 395
for w_, L in [(0, 2), (1, 3), (2, 3), (3, 6), (4, 9), (5, 14), (6, 16), (7, 16)]:
    ax.add_patch(Rectangle((90, y0 + w_ * 15), L * 28, 12, fc=GREEN, ec="#7fbf7f"))
    T(ax, 40, y0 + w_ * 15 - 1, f"w={w_}", size=10)
T(ax, 560, 410, "Consequence: one scan of the data finds the\nlengths of all windows at once (the break\npoints of w = 0, 1, 2, … in turn).\nO(2^b + k) instead of O(k · 2^b) per channel.", size=13, linespacing=1.45)
pages.append(fig)

# ------------------------------------------------------------------------------------------------
fig, ax = slide("Lemma 2: prefixes and the cheapest method")
bullets(ax, 40, 66, [
    "If a method works for l frames from p, it works for any shorter block from p:",
    ">Fixed: a prefix has a smaller (or equal) range; Adaptive: a prefix has fewer frames to fit.",
    "So for a block of exactly l frames, channel c may use every method with $L_{c,m}(p)\\geq l$.",
    "For the same l, a wider window only costs more, so the best choice in each family is the",
    ">narrowest window that lasts long enough:",
], size=14, gap=30)
T(ax, 80, 230, r"$w^F(l)=\min\{w:\ L^F_w(p)\geq l\},\qquad w^A(l)=\min\{w:\ L^A_w(p)\geq l\}$", size=18)
T(ax, 80, 285, r"$c^*_c(p,l)=\min\left(l\cdot k,\ \ k+l\cdot w^F(l),\ \ k+(l-1)\cdot w^A(l)\right)$", size=20)
T(ax, 80, 340, r"$\mathrm{Bits}(p,l)=H+\sum_{c=0}^{C-1}c^*_c(p,l)$", size=20)
note_box(ax, 40, 400, 880, 70,
         "Bits(p, l) is the exact size of the cheapest block of l frames starting at p: every decodable block of that\n"
         "length is at least this big, and the narrowest windows give a real block of exactly this size.", size=13)
pages.append(fig)

# ------------------------------------------------------------------------------------------------
fig, ax = slide("Finding the best common block: exact search")
T(ax, 40, 64, "Sprints 2–3: a block is a combination of methods (m₀, …, m_{C−1}), its length is l = min_c L_{c,m_c}(p),", size=14)
T(ax, 40, 88, "and the best block has the highest E_block. Brute force tries all ∏ M_c combinations.", size=14)
T(ax, 40, 126, "Group the combinations by their length. For a candidate length l (one of the lengths L_{c,m}):", size=14)
T(ax, 80, 160, r"$A_c(l)=\min_{m:\ L_{c,m}\geq l}\mathrm{cost}_c(m,l),\qquad B_c(l)=\min_{m:\ L_{c,m}=l}\mathrm{cost}_c(m,l)$", size=17)
T(ax, 80, 205, r"$\mathrm{Bits}^*(l)=H+\sum_c A_c(l)+\min_c\left(B_c(l)-A_c(l)\right)$", size=20)
bullets(ax, 40, 258, [
    "Proof: the length is exactly l if every channel lasts ≥ l and at least one channel j ends at l.",
    ">For a fixed j the best is B_j + Σ_{c≠j} A_c; minimizing over j gives the formula.",
    r"Best block: $\max_l\ 1-\mathrm{Bits}^*(l)\,/\,(l\cdot F)$ over at most $\sum_c M_c$ candidate lengths.",
    "Ties: the lexicographically first (m₀, …, m_{C−1}), rebuilt channel by channel, so the result is the same",
    ">block as the brute force, byte for byte.",
], size=14, gap=29)
rows = [("Frame format", "Brute force, 2 Δ", "Exact, 2 Δ (≤)"),
        ("RGB 565", "1 200", "34² = 1 156"),
        ("RGB 888", "4 096", "48² = 2 304"),
        ("RGB 48 bit", "32 768", "96² = 9 216"),
        ("4ch × 32 bit", "16 777 216", "256² = 65 536")]
table(ax, 590, 410, rows, [120, 120, 120], rowh=22, size=11)
T(ax, 40, 420, "Complexity: O(Σ M_c · |candidates|) ≤ O((Σ M_c)²)\ninstead of O(∏ M_c).\nLogo RGB 888: 17.5 s → 0.19 s, same output.", size=14, linespacing=1.5)
pages.append(fig)

# ------------------------------------------------------------------------------------------------
fig, ax = slide("Chain of blocks as a shortest path")
# DAG picture
n = 9
xs_ = [80 + i * 95 for i in range(n)]
for i in range(n):
    ax.add_patch(Circle((xs_[i], 150), 15, fc="#eaf2ff", ec=BLUE, lw=1.2, zorder=3))
    ax.text(xs_[i], 150, str(i) if i < n - 1 else "N", ha="center", va="center", fontsize=12, zorder=4)
for (a, b_, col) in [(0, 1, "#999"), (0, 2, "#999"), (0, 4, "#999"), (1, 3, "#999"), (2, 6, "#999"), (4, 8, "#999"),
                     (3, 5, "#999"), (5, 8, "#999"), (6, 8, "#999")]:
    ax.add_patch(FancyArrowPatch((xs_[a], 135), (xs_[b_], 135), connectionstyle=f"arc3,rad=-{0.25 + 0.03 * (b_ - a)}",
                                 arrowstyle="-|>", mutation_scale=12, color=col, lw=1.1, zorder=2))
for (a, b_) in [(0, 2), (2, 6), (6, 8)]:
    ax.add_patch(FancyArrowPatch((xs_[a], 165), (xs_[b_], 165), connectionstyle=f"arc3,rad={0.25 + 0.03 * (b_ - a)}",
                                 arrowstyle="-|>", mutation_scale=14, color="#2a9d2a", lw=2.2, zorder=2))
T(ax, 40, 262, "Frame boundaries are nodes 0 … N. A block of l frames from p is an edge p → p + l with weight Bits(p, l).", size=14)
T(ax, 40, 287, "Every compressed stream is a path from 0 to N, and its size is the path weight.", size=14)
T(ax, 80, 325, r"$\mathrm{Best}(N)=0$", size=20)
T(ax, 80, 362, r"$\mathrm{Best}(p)=\min_{1\leq l\leq\min(2^b,\ N-p)}\left(\mathrm{Bits}(p,l)+\mathrm{Best}(p+l)\right)$", size=20)
T(ax, 80, 412, r"$\mathrm{size}_{min}=\mathrm{Best}(0)$", size=20)
note_box(ax, 40, 462, 880, 58,
         "Best(p) is computed from p = N − 1 down to 0; the chosen length of every position is stored,\n"
         "and the chain is restored from frame 0 forward.", size=13)
pages.append(fig)

# ------------------------------------------------------------------------------------------------
fig, ax = slide("Theorem: the chain is optimal")
T(ax, 40, 66, "For the given block size field b, Δ option and squares, the restored chain has the minimum size among", size=15)
T(ax, 40, 92, "all streams that the decoder accepts.", size=15)
T(ax, 40, 140, "Proof", size=15, fontweight="bold")
bullets(ax, 40, 172, [
    "Any accepted stream is a sequence of blocks with lengths 1 ≤ l_j ≤ 2^b and Σ l_j = N.",
    "In each block every channel uses an applicable method, so the block is at least Bits(p, l) (Lemma 2).",
    "By induction from p = N down to 0, a stream for frames p … N−1 is at least Best(p).",
    "The restored chain uses, in every block, the narrowest windows that reach Bits(p, l), with the initial",
    ">value = minimum (Fixed) or first frame (Adaptive), so its size is exactly Best(0).  ■",
], size=14, gap=32)
note_box(ax, 40, 360, 880, 108,
         "Optimal \"per block\" (sprints 2–3: the most efficient block at each step, or the best block for every frame)\n"
         "is not optimal for the chain: a very efficient long block can leave a tail that compresses badly, and\n"
         "a short, less efficient block can open the way for a much better one. The example is on the next slide.\n"
         "Bits(p, l) does not depend on the previous blocks, so the dynamic programming covers all chains.", size=13)
pages.append(fig)

# ------------------------------------------------------------------------------------------------
fig, ax = slide("Example: greedy vs optimal chain")
seq = [97, 97, 117, 117, 117, 116, 116, 117, 117, 117]
best, ch = dp(seq, 3)
g = greedy(seq, 3)
T(ax, 40, 64, "k = 8, b = 3 (blocks of 1 … 8 frames), Fixed window first (d = 4), H = 7 bits, raw data 80 bits.", size=14)
cw = 54
def chain_row(y, chain, title, total):
    T(ax, 40, y - 52, title, size=15, fontweight="bold")
    x = 40
    cols = ["#c9f5c9", "#bfe0ff", "#ffe2b0"]
    for j, (p, l, bits_, name) in enumerate(chain):
        for i in range(l):
            ax.add_patch(Rectangle((x + (p + i) * cw, y), cw, 30, fc=cols[j % 3], ec="white"))
            ax.text(x + (p + i) * cw + cw / 2, y + 15, str(seq[p + i]), ha="center", va="center", fontsize=14, fontweight="bold")
        ax.plot([x + p * cw, x + p * cw], [y - 6, y + 36], color=BLUE, lw=2)
        w_ = int(name[1:]) if name[0] in "FA" else None
        kind = "Fixed" if name[0] == "F" else ("Adaptive" if name[0] == "A" else "Uncompressed")
        e = (1 - bits_ / (l * 8)) * 100
        ax.text(x + (p + l / 2) * cw, y + 42, f"{kind} w={w_}: {bits_} bits, E={e:.2f}%".replace("0%", "%"), ha="center", va="top", fontsize=12)
    T(ax, 40 + 10 * cw + 20, y + 4, f"{total} bits\nE = {(1 - total / 80) * 100:.1f}%", size=15, fontweight="bold")
chain_row(150, g, "Greedy: the most efficient block at each step", sum(c[2] for c in g))
chain_row(320, ch, "Optimal chain (dynamic programming)", best[0])
T(ax, 40, 430, "Greedy: 7 + 8 + 8·5 = 55 (E = 14.1%) then 7 + 8 = 15 → 70 bits.", size=14)
T(ax, 40, 458, "Optimal: 7 + 8 = 15 (E = 6.25%) then 7 + 8 + 8·1 = 23 (E = 64.1%) → 38 bits, 46% smaller.", size=14)
pages.append(fig)

# ------------------------------------------------------------------------------------------------
fig, ax = slide("Bits(p, l) is piecewise linear")
T(ax, 40, 64, "For one channel the candidates are three lines (with the narrowest windows that still last l frames):", size=14)
T(ax, 80, 98, r"Uncompressed: $k\cdot l$,     Fixed: $k+w\cdot l$,     Adaptive: $(k-w)+w\cdot l$", size=17)
bullets(ax, 40, 140, [
    "w^F(l) and w^A(l) change only when l passes some L_w; between those points",
    ">the cheapest line a + s·l stays the cheapest until a line with a smaller slope crosses it:",
], size=14, gap=28)
T(ax, 80, 205, r"$l_\times=\left\lfloor\frac{a_i-a}{s-s_i}\right\rfloor+1$", size=19)
T(ax, 40, 260, "So Bits(p, l) = α + σ·l on segments,\nand the next segment start is the\nnearest l_× or L_w + 1 over all\nchannels. Inside a segment each\nlength costs one addition.", size=14, linespacing=1.5)
# plot an illustrative channel
k = 8
LF = [2, 3, 5, 7, 12, 18, 30, 64]
LA = [0, 4, 6, 10, 20, 34, 64, 64]
l = np.arange(1, 65)
def wmin(L, l_, start):
    for w_ in range(start, k):
        if L[w_] >= l_: return w_
    return None
fx = [k + li * wmin(LF, li, 0) if wmin(LF, li, 0) is not None else np.nan for li in l]
fa = [k + (li - 1) * wmin(LA, li, 1) if wmin(LA, li, 1) is not None else np.nan for li in l]
un = [k * li for li in l]
best_ = np.nanmin(np.vstack([fx, fa, un]), axis=0)
a = chart_axes(fig, 330, 245, 600, 260)
a.plot(l, un, color="#999", lw=1.2, label="Uncompressed")
a.step(l, fx, where="post", color="#e07b00", lw=1.2, label="Fixed (narrowest)")
a.step(l, fa, where="post", color=BLUE, lw=1.2, label="Adaptive (narrowest)")
a.plot(l, best_, color="#2a9d2a", lw=3, alpha=0.6, label="cheapest c*(l)")
a.set_xlabel("block length l", fontsize=10); a.set_ylabel("bits", fontsize=10)
a.set_ylim(0, 320); a.legend(fontsize=9, frameon=False)
pages.append(fig)

# ------------------------------------------------------------------------------------------------
fig, ax = slide("Lemma 3: pruning")
T(ax, 40, 66, r"$c^*_c(p,l)$ never decreases as l grows:", size=16)
bullets(ax, 40, 104, [
    "every candidate line has a non-negative slope and the windows only get wider;",
    "the set of applicable methods only shrinks (Lemma 2).",
], size=14, gap=28)
T(ax, 40, 175, r"So $\mathrm{Bits}(p,l)$ is non-decreasing in l, and with $\mathrm{Best}(\cdot)\geq 0$:", size=16)
T(ax, 80, 220, r"$\mathrm{Bits}(p,l)\geq\mathrm{best\ so\ far}\ \Rightarrow\ \mathrm{Bits}(p,l')+\mathrm{Best}(p+l')\geq\mathrm{best\ so\ far}\quad\forall\,l'\geq l$", size=18)
T(ax, 40, 280, "The scan of lengths stops at the first such l. No candidate is lost, so the result stays optimal.", size=14)
note_box(ax, 40, 340, 880, 88,
         "In practice a block that is already bigger than the best whole tail appears after a few dozen frames,\n"
         "so the scan looks at a small part of the 2^b possible lengths. Example: mit-text, b = 15 (32 768 lengths),\n"
         "takes about 1 second for the whole file.", size=13)
pages.append(fig)

# ------------------------------------------------------------------------------------------------
fig, ax = slide("Incremental lengths (backward pass)")
T(ax, 40, 64, "The DP goes from p = N − 1 down to 0, so the lengths L_w(p) are updated from L_w(p+1):", size=14)
T(ax, 40, 100, "Fixed window", size=15, fontweight="bold")
bullets(ax, 40, 130, [
    r"End of the longest block $e_w(p)=p+L^F_w(p)$. If $[p,e)$ fits, $[p+1,e)$ fits too, so $e_w(p)\leq e_w(p+1)$.",
    "Start from e = e_w(p+1) and move it left while e > p + 2^b or the range of [p, e) > 2^w − 1.",
    "The min and max of the window are kept in two monotonic deques (amortized O(1)).",
], size=14, gap=28)
T(ax, 40, 230, "Adaptive floating window", size=15, fontweight="bold")
bullets(ax, 40, 260, [
    r"A block breaks at the first frame that doesn't fit the window around its predecessor:",
], size=14, gap=28)
T(ax, 80, 292, r"$\beta_w(p)=p+1$  if $x_{p+1}$ doesn't fit around $x_p$,  otherwise  $\beta_w(p)=\beta_w(p+1)$", size=16)
T(ax, 80, 330, r"$L^A_w(p)=\min\left(\beta_w(p)-p,\ 2^b,\ N-p\right)$", size=17)
note_box(ax, 40, 390, 880, 70,
         "All lengths of all windows for all positions: O(N · Σ_c k_c) amortized,\n"
         "instead of rescanning up to 2^b frames for each window at each position: O(N · 2^b · Σ_c k_c).", size=13)
pages.append(fig)

# ------------------------------------------------------------------------------------------------
fig, ax = slide("Complexity")
rows = [("Step", "Before (sprint 3)", "Now"),
        ("Lengths of all methods at p", "O(Σ M_c · 2^b)", "O(Σ k_c) amortized"),
        ("Best common block at p", "O(∏ M_c)", "O((Σ M_c)²)"),
        ("Chain of blocks", "heuristic (frame-by-frame)", "exact DP, pruned"),
        ("Memory allocated, Logo RGB 888", "41 GB (total)", "< 30 MB"),
        ("Compression time, all test cases", "53.7 s", "2.9 s"),
        ("Decoder (flash / RAM)", "—", "unchanged")]
table(ax, 40, 70, rows, [300, 270, 270], rowh=36, size=14, aligns=["left", "center", "center"])
T(ax, 40, 345, "Compressor implementation: data cached once as arrays, Span<T> and stackalloc in the hot loops,\n"
               "no allocations per frame, multithreaded search for the legacy chain and for Auto settings.", size=14, linespacing=1.5)
pages.append(fig)

# ------------------------------------------------------------------------------------------------
fig, ax = slide("Auto settings")
T(ax, 40, 66, "The DP gives the exact output size without writing the stream, so the settings can be searched:", size=14)
T(ax, 80, 106, r"$(s^*,\ \delta^*,\ b^*)=\arg\min_{s\in S,\ \delta\in\Delta,\ b\in\mathcal{B}}\ \mathrm{Best}_{s,\delta,b}(0)$", size=20)
bullets(ax, 40, 160, [
    "S: square widths that divide the image width and height (1 = no squares);",
    "Δ: the 4 Δ options;  $\\mathcal{B}$: the block size field options.",
    "All (s, δ) pairs are tried in parallel: squares and Δ option interact,",
    ">choosing them one after another was up to 36% worse.",
    "For each pair b grows until the size gets worse 3 times in a row (in practice the",
    ">size has a single minimum in b; this stop rule is the only heuristic step).",
    "The predicted size equals the real output, so the choice is not an estimate.",
], size=14, gap=28)
note_box(ax, 40, 380, 880, 62,
         "Auto may choose any Δ option. The decoder must be built with the matching #define in Decompression.h\n"
         "(DECOMPRESSION_FIXED_WINDOW_FIRST etc.).", size=13)
pages.append(fig)

# ------------------------------------------------------------------------------------------------
cases = ["logo-gray", "mattress-gray", "noise-gray", "gradient-gray", "parrot-gray-2x2", "mit-text",
         "logo-565-2x2", "gradient-565-2x2", "nebula-565-2x2", "parrot-565-2x2", "logo-888", "icons-888"]
labels = ["Logo\ngray", "Mattress\ngray", "Noise\ngray", "Gradient\ngray", "Parrot\ngray 2x2", "Text",
          "Logo\n565", "Gradient\n565", "Nebula\n565", "Parrot\n565", "Logo\n888", "Icons\n888"]
base = dict(zip(cases, [12678, 23231, 50233, 16282, 23391, 28431, 15546, 17468, 62974, 41318, 26734, 29382]))
base_eff = dict(zip(cases, [77.99, 59.67, 12.79, 71.73, 22.03, 13.24, 86.51, 84.84, 45.34, 31.14, 84.53, 83.00]))
opt = dict(zip(cases, [12206, 22735, 50188, 16157, 23037, 28430, 15220, 17341, 60676, 40095, 26090, 27924]))
auto = dict(zip(cases, [10332, 22735, 50073, 16157, 23037, 28430, 14644, 15535, 60676, 40095, 22903, 22203]))
raw = {c: base[c] / (1 - base_eff[c] / 100) for c in cases}
eff = lambda d, c: (1 - d[c] / raw[c]) * 100

fig, ax = slide("Results")
a = chart_axes(fig, 60, 60, 860, 380)
xi = np.arange(len(cases)); bw = 0.27
for j, (d, name, col) in enumerate([(base, "Sprint 3 (frame-by-frame)", "#8c8c8c"),
                                    (opt, "Optimal chain, same settings", "#7fd67f"),
                                    (auto, "Optimal chain + Auto settings", "#4fa3e8")]):
    vals_ = [eff(d, c) for c in cases]
    a.bar(xi + (j - 1) * bw, vals_, bw, color=col, label=name)
    if j == 2:
        for x_, v in zip(xi, vals_):
            a.text(x_ + bw, v + 1, f"{v:.1f}%", ha="center", fontsize=8)
a.set_xticks(xi); a.set_xticklabels(labels, fontsize=9)
a.set_ylabel("Compression efficiency, %", fontsize=10); a.set_ylim(0, 100)
a.legend(fontsize=10, frameon=False, ncol=3, loc="upper center", bbox_to_anchor=(0.5, 1.08))
T(ax, 60, 490, "Sprint 3 settings for the first two series; 240 × 240 images, text 32 KB. Every output is checked with the C++ decoder.", size=11, style="italic")
pages.append(fig)

fig, ax = slide("Results: size reduction")
a = chart_axes(fig, 60, 60, 860, 380)
red_opt = [(1 - opt[c] / base[c]) * 100 for c in cases]
red_auto = [(1 - auto[c] / base[c]) * 100 for c in cases]
a.bar(xi - 0.2, red_opt, 0.4, color="#7fd67f", label="Optimal chain, same settings")
a.bar(xi + 0.2, red_auto, 0.4, color="#4fa3e8", label="Optimal chain + Auto settings")
for x_, v in zip(xi, red_auto):
    a.text(x_ + 0.2, v + 0.4, f"−{v:.1f}%", ha="center", fontsize=8)
a.set_xticks(xi); a.set_xticklabels(labels, fontsize=9)
a.set_ylabel("Smaller than sprint 3, %", fontsize=10)
a.legend(fontsize=10, frameon=False, ncol=2, loc="upper left")
T(ax, 60, 490, "Auto: block size field, Δ option and squares chosen per image (1–13 s per image on 4 cores).", size=11, style="italic")
pages.append(fig)

fig, ax = slide("Results: compression time")
tc = ["logo-gray", "mattress-gray", "noise-gray", "mit-text", "logo-565-2x2", "gradient-565-2x2",
      "nebula-565-2x2", "parrot-565-2x2", "logo-888", "icons-888"]
tl = ["Logo\ngray", "Mattress\ngray", "Noise\ngray", "Text", "Logo\n565", "Gradient\n565", "Nebula\n565", "Parrot\n565", "Logo\n888", "Icons\n888"]
t0 = [333, 244, 802, 1189, 9025, 3500, 2184, 1331, 17533, 8141]
t1 = [140, 109, 153, 899, 106, 104, 96, 55, 100, 85]
a = chart_axes(fig, 60, 60, 860, 380)
xt = np.arange(len(tc))
a.bar(xt - 0.2, t0, 0.4, color="#8c8c8c", label="Sprint 3 compressor")
a.bar(xt + 0.2, t1, 0.4, color="#4fa3e8", label="Now (optimal chain)")
a.set_yscale("log"); a.set_ylabel("ms (log scale)", fontsize=10)
for x_, v0, v1 in zip(xt, t0, t1):
    a.text(x_, max(v0, v1) * 1.25, (f"{v0 / v1:.0f}×" if v0 / v1 >= 10 else f"{v0 / v1:.1f}×"), ha="center", fontsize=9, fontweight="bold")
a.set_xticks(xt); a.set_xticklabels(tl, fontsize=9)
a.legend(fontsize=10, frameon=False, ncol=2, loc="upper left")
T(ax, 60, 490, "Same settings, one image at a time, 4-core cloud machine, .NET 10.", size=11, style="italic")
pages.append(fig)

# ------------------------------------------------------------------------------------------------
fig, ax = slide("Decompression")
bullets(ax, 40, 70, [
    "The stream format is unchanged: the existing decoder reads every new file.",
    "All the improvements are on the compressor side; the decoder stays as small as before:",
    ">per channel it keeps only the method, the window and the last value;",
    ">one pass over the data, no tables, no buffers beyond one block header.",
    "The Δ option and the channel bit depths must match the decoder build (#define).",
    "Squares need DECOMPRESSION_USE_SQUARES and the same square width.",
], size=15, gap=34)
pages.append(fig)

fig, ax = slide("Next sprint")
bullets(ax, 40, 70, [
    "Cross-platform interface (Avalonia) on top of the UI-free core library.",
    "Repository cleanup, build and test automation, a proper release.",
    "Format extensions worth measuring (would change the decoder):",
    ">a flag \"same methods as the previous block\" to shorten headers;",
    ">variable-size length field for long uniform blocks.",
], size=15, gap=34)
pages.append(fig)

with PdfPages(OUT) as pdf:
    for f in pages:
        pdf.savefig(f)
        plt.close(f)
    d = pdf.infodict()
    d["Title"] = "Delta encoding. Sprint 4: Mathematical foundation"
    d["Author"] = "Dmitry Muravyev"
print("pages:", len(pages))
