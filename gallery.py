from PIL import Image, ImageDraw, ImageFont
from pathlib import Path
import csv,sys
src=Path(sys.argv[1]); out=Path(sys.argv[2]); out.mkdir(parents=True,exist_ok=True)
ims=[Image.open(src/f'HNS-garden-panel-{i}.png').convert('RGB') for i in range(1,6)]
assert all(im.size==(1600,900) for im in ims)
with (src/'HNS-garden-mosaic.tsv').open(newline='',encoding='utf-8-sig') as f:
    rows=list(csv.DictReader(f,delimiter='\t'))
assert len(rows)==75
ev={(int(r['Panel']),r['Species'],int(r['Column'])):r for r in rows}
font=ImageFont.truetype(r'C:\Windows\Fonts\segoeui.ttf',17)
small=ImageFont.truetype(r'C:\Windows\Fonts\segoeui.ttf',13)
bold=ImageFont.truetype(r'C:\Windows\Fonts\segoeuib.ttf',26)
traits=['Succession Bloom','Sector Sport','Biform Harvest','Sister Buds','Ripening Mosaic']
slugs=['succession-bloom','sector-sport','biform-harvest','sister-buds','ripening-mosaic']
species=['Corn','Cotton','Daylily']
xs=[475,598,718,840,965,1085]
ys=[278,447,612]
def crop(im,x,y,size=(142,145)):
    return im.crop((x-43,y-48,x+43,y+40)).resize(size,Image.Resampling.NEAREST)
for p,(im,trait,slug) in enumerate(zip(ims,traits,slugs),1):
    left,top,cw,ch=123,110,166,184
    canvas=Image.new('RGB',(left+cw*6+15,top+ch*3+15),(25,27,29)); d=ImageDraw.Draw(canvas)
    d.text((16,12),trait+' | individual organ expression',font=bold,fill='#f2f4ed')
    d.text((16,48),'Each row contains one vanilla control and five plants of the same registered cultivar.',font=font,fill='#b5c1b7')
    for c,label in enumerate(['Vanilla','Individual 1','Individual 2','Individual 3','Individual 4','Individual 5']):
        d.text((left+c*cw+cw/2,top-10),label,font=font,fill='#e3e8dd',anchor='ms')
    for r,(name,y) in enumerate(zip(species,ys)):
        yy=top+r*ch
        d.text((16,yy+74),name,font=font,fill='#e3e8dd')
        for c,x in enumerate(xs):
            canvas.paste(crop(im,x,y,(155,159)),(left+c*cw+5,yy))
            label='Original' if c==0 else ('Variant '+ev[(p,'Plant_'+name,c+1)]['Variant frame']+' | '+ev[(p,'Plant_'+name,c+1)]['Source atlas'].split('_HNS_')[0])
            d.text((left+c*cw+cw/2,yy+165),label,font=small,fill='#c4d1c4',anchor='mt')
    canvas.save(out/f'mosaic-{slug}.jpg',quality=94,subsampling=0)
# Entire plot galleries; preserve the original positions.
tw,th=560,350
overview=Image.new('RGB',(tw*2+36,th*3+100),(25,27,29));d=ImageDraw.Draw(overview)
d.text((12,12),'Living mosaics | five experimental genetics',font=bold,fill='#f2f4ed')
d.text((12,50),'Rows: corn, cotton, daylily. Leftmost control; five same-cultivar individuals in each row.',font=font,fill='#b5c1b7')
for n,(im,trait) in enumerate(zip(ims,traits)):
    x=12+(n%2)*(tw+12);y=85+(n//2)*(th+6)
    cr=im.crop((405,218,1160,672));cr.thumbnail((tw,th-30))
    d.text((x,y),trait,font=font,fill='#e3e8dd')
    overview.paste(cr,(x,y+25))
overview.save(out/'mosaic-gallery.jpg',quality=94,subsampling=0)
# A separate species comparison, showing the same trait in five individuals.
for r,(name,y) in enumerate(zip(species,ys)):
    left,top,cw,ch=175,102,145,146
    canvas=Image.new('RGB',(left+cw*6+12,top+ch*5+12),(25,27,29));d=ImageDraw.Draw(canvas)
    d.text((16,12),name+' | living mosaics',font=bold,fill='#f2f4ed')
    d.text((16,48),'Five genetic candidates; one control and five same-cultivar instances in each row.',font=font,fill='#b5c1b7')
    for c,label in enumerate(['Vanilla','Individual 1','Individual 2','Individual 3','Individual 4','Individual 5']):
        d.text((left+c*cw+cw/2,top-10),label,font=small,fill='#e3e8dd',anchor='ms')
    for p,(im,trait) in enumerate(zip(ims,traits),1):
        yy=top+(p-1)*ch
        d.text((12,yy+60),trait,font=font,fill='#e3e8dd')
        for c,x in enumerate(xs):
            canvas.paste(crop(im,x,y,(135,138)),(left+c*cw+5,yy))
    canvas.save(out/f'mosaic-{name.lower()}.jpg',quality=94,subsampling=0)
# compact visual QA sheets
for name in ['corn','cotton','daylily']:
    im=Image.open(out/f'mosaic-{name}.jpg')
    im.thumbnail((770,650))
    im.save(src/f'mosaic-review-{name}.jpg',quality=60)
    print(name,(src/f'mosaic-review-{name}.jpg').stat().st_size)
