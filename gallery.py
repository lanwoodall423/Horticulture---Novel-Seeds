from PIL import Image,ImageDraw,ImageFont
from pathlib import Path
import sys
p=Path(sys.argv[1])
ims=[Image.open(p/f'HNS-garden-panel-{i}.png').convert('RGB') for i in (1,2,3)]
assert all(im.size==(1600,900) for im in ims)
font=ImageFont.truetype(r'C:\Windows\Fonts\segoeui.ttf',18)
small=ImageFont.truetype(r'C:\Windows\Fonts\segoeui.ttf',15)
bold=ImageFont.truetype(r'C:\Windows\Fonts\segoeuib.ttf',25)
names=['Control','Ivory Nerves','Crimson Network','Living Glass','Vascular Chimera','Bronze Reticulum']
species=['Corn','Cotton','Strawberry']
xs=[475,598,718,840,965,1085]; ys=[278,447,612]
left,top,cw,ch=125,99,166,158
im=Image.new('RGB',(left+cw*6+12,top+ch*3+14),(25,27,29));d=ImageDraw.Draw(im)
d.text((14,12),'Living Tissue Atlases | native daylight',font=bold,fill='#f2f4ed')
d.text((14,48),'Source-art anatomy, not whole-plant color filters.',font=font,fill='#b5c1b7')
for i,name in enumerate(names):d.text((left+i*cw+cw/2,top-10),name,font=small,fill='#e3e8dd',anchor='ms')
for row,(name,y) in enumerate(zip(species,ys)):
    yy=top+row*ch
    d.text((12,yy+ch/2),name,font=font,fill='#dce5d7',anchor='lm')
    for col,x in enumerate(xs):
        crop=ims[0].crop((x-59,y-59,x+59,y+59)).resize((152,152),Image.Resampling.NEAREST)
        im.paste(crop,(left+col*cw+4,yy))
im.save(p/'vascular-gallery.jpg',quality=95,subsampling=0)
im.thumbnail((850,520));im.save(p/'vascular-review.jpg',quality=70)
w,h=760,477
im=Image.new('RGB',(w+20,h*3+112),(25,27,29));d=ImageDraw.Draw(im)
d.text((10,12),'Same specimens | day, night, day',font=bold,fill='#f2f4ed')
for n,img in enumerate(ims):
    y=56+n*(h+16)
    d.text((10,y),['Noon','Midnight','Noon again'][n],font=font,fill='#e3e8dd')
    im.paste(img.crop((404,200,1164,680)).resize((w,h-24)),(10,y+25))
im.save(p/'vascular-light-cycle.jpg',quality=94,subsampling=0)
print('review bytes', (p/'vascular-review.jpg').stat().st_size)
