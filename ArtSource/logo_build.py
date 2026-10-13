import sys
from PIL import Image, ImageDraw, ImageFont
S = 2
title = ImageFont.truetype("/usr/share/fonts/opentype/urw-base35/NimbusSans-Bold.otf", 150*S)
tag = ImageFont.truetype("/usr/share/fonts/opentype/urw-base35/P052-Italic.otf", 52*S)
cream, red = (234,226,206,255), (200,30,35,255)
pad = 24*S
d0 = ImageDraw.Draw(Image.new("RGBA", (10,10)))
lines = ["EVERYDAY", "OBJECTS"]
bbs = [d0.textbbox((0,0), s, font=title) for s in lines]
tb = d0.textbbox((0,0), "nothing is an accident.", font=tag)
tw = max(b[2]-b[0] for b in bbs)
capH = bbs[0][3]-bbs[0][1]
gap, ul, ulGap, tagGap = 26*S, 14*S, 18*S, 22*S
W = pad*2 + tw
H = pad*2 + capH*2 + gap + ulGap + ul + tagGap + (tb[3]-tb[1])
im = Image.new("RGBA", (W, H), (0,0,0,0)); d = ImageDraw.Draw(im)
cx = W/2; y = pad
for s, b in zip(lines, bbs):
    d.text((cx-(b[2]-b[0])/2-b[0], y-b[1]), s, font=title, fill=cream); y += capH + gap
y += ulGap - gap
d.rectangle([cx-tw/2, y, cx+tw/2, y+ul], fill=red); y += ul + tagGap
d.text((cx-(tb[2]-tb[0])/2-tb[0], y-tb[1]), "nothing is an accident.", font=tag, fill=(205,196,172,255))
im = im.resize((W//S, H//S), Image.LANCZOS); im.save(sys.argv[1])
bg = Image.new("RGBA", im.size, (16,18,24,255)); bg.alpha_composite(im); bg.save(sys.argv[2]); print(im.size)
