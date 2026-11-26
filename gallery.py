from PIL import Image, ImageDraw, ImageFont
from pathlib import Path
import sys
src=Path(sys.argv[1]); out=src/'gallery'; out.mkdir(exist_ok=True)
ims=[Image.open(src/f'HNS-garden-panel-{i}.png').convert('RGB') for i in range(1,6)]
assert all(im.size==(1600,900) for im in ims)
font=ImageFont.truetype(r'C:\Windows\Fonts\segoeui.ttf',17)
small=ImageFont.truetype(r'C:\Windows\Fonts\segoeui.ttf',15)
bold=ImageFont.truetype(r'C:\Windows\Fonts\segoeuib.ttf',25)
traits=['Control','Crooked Chronicle','Ringed Habit','Asymmetric Whorls','Release Crown','Stretch Bands']
states=['Open / open / open','Left / left / open','Right / right / open','Left / right / left','Both / both / open']
species=['Corn','Cotton','Daylily']
xs=[475,598,718,840,965,1085]; ys=[278,447,612]
for name,y in zip(species,ys):
    left,top,cw,ch=186,100,169,160
    canvas=Image.new('RGB',(left+6*cw+12,top+5*ch+14),(25,27,29)); d=ImageDraw.Draw(canvas)
    d.text((14,12),name+' | botanical chronicles',font=bold,fill='#f2f4ed')
    d.text((14,49),'Early / middle / late growing history; every mature plot is now isolated.',font=font,fill='#b5c1b7')
    for c,t in enumerate(traits):
        d.text((left+c*cw+cw/2,top-10),t,font=small,fill='#e3e8dd',anchor='ms')
    for i,(im,state) in enumerate(zip(ims,states)):
        yy=top+i*ch
        d.text((12,yy+ch/2),state,font=small,fill='#dce5d7',anchor='lm')
        for c,x in enumerate(xs):
            crop=im.crop((x-59,y-60,x+59,y+58)).resize((159,159),Image.Resampling.NEAREST)
            canvas.paste(crop,(left+c*cw+3,yy))
    canvas.save(out/f'chronicle-{name.lower()}.jpg',quality=94,subsampling=0)
tw,th=570,361
canvas=Image.new('RGB',(tw*2+36,th*3+94),(25,27,29));d=ImageDraw.Draw(canvas)
d.text((12,12),'Botanical chronicles | identical surroundings, different growing histories',font=bold,fill='#f2f4ed')
d.text((12,49),'Rows: corn, cotton, daylily. Columns: control, crooked, ringed, whorls, release, stretch.',font=font,fill='#b5c1b7')
for n,(im,state) in enumerate(zip(ims,states)):
    x=12+(n%2)*(tw+12); y=85+(n//2)*(th+6)
    cr=im.crop((404,200,1168,680));cr.thumbnail((tw,th-30))
    d.text((x,y),state,font=font,fill='#e3e8dd')
    canvas.paste(cr,(x,y+24))
canvas.save(out/'chronicle-gallery.jpg',quality=94,subsampling=0)
for name in ('corn','cotton','daylily'):
    im=Image.open(out/f'chronicle-{name}.jpg');im.thumbnail((750,625))
    im.save(src/f'review-{name}.jpg',quality=59)
    print(name,(src/f'review-{name}.jpg').stat().st_size)
