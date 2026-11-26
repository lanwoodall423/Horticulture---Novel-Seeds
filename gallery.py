from pathlib import Path
from PIL import Image, ImageDraw, ImageFont
import csv,sys
p=Path(sys.argv[1]); out=p/'gallery';out.mkdir(exist_ok=True)
ims=[Image.open(p/f'HNS-garden-panel-{i}.png').convert('RGB') for i in range(1,9)]
assert all(im.size==(1600,900) for im in ims)
rows=list(csv.DictReader((p/'HNS-garden-life-suite.tsv').open(encoding='utf-8-sig'),delimiter='\t'))
assert len(rows)==120
ev={(int(r['Panel']),r['Species'],int(r['Column'])):r for r in rows}
names=['Photosynthetic Pulse','Nyctinastic Folding','Hydraulic Wilt','Crystal Dew Lensing','Pollination Cloud','Mosaic Organism','Seasonal Color Front','Subsurface Root Halo','Leaf Skeletonization','Bioluminescent Signaling','Wind Differential Anatomy','Fractal Venation Bloom','Translucent Petal Tissue','Canopy Volume Parallax','Micro-Habitat Response','Fruit Ripening Cascade','Frost Etching','Mycelial Relay','Morphogenetic Reconfiguration','Event-Driven Expression']
species=['Corn','Cotton','Daylily']
states=[('Morning / heat sample','Midnight / cool sample'),('Noon / autumn input','Midnight / late-autumn input'),('Morning / open plots','Afternoon / western companions'),('72% growth / dry','Mature / rain / frost sample')]
shortnames=['Tissue and weather','Seasonal and ground expression','Anatomical motion and habitat','Growth and connected ecology']
fonts={}
def font(n,b=False):
    k=(n,b)
    if k not in fonts:fonts[k]=ImageFont.truetype(r'C:\Windows\Fonts\segoeui'+('b' if b else '')+'.ttf',n)
    return fonts[k]
bg='#191d1b';ink='#f0f5ea';muted='#afbeb1';accent='#c1d5a3'
xs=[475,598,718,840,965,1085]; ys=[278,447,612]
def crop(im,x,y):
    return im.crop((x-56,y-56,x+56,y+56)).resize((150,150),Image.Resampling.NEAREST)
def label(d,xy,text,n=16,b=False,c=ink,anchor=None):
    d.text(xy,text,font=font(n,b),fill=c,anchor=anchor)
for g in range(4):
    left,top,cw,ch=150,116,188,174
    im=Image.new('RGB',(left+cw*6+12,top+ch*6+75),bg);d=ImageDraw.Draw(im)
    label(d,(14,12),f'{g+1:02d}  |  {shortnames[g]}',28,True)
    label(d,(14,49),'Unretouched crops from two native game screenshots; each row uses the same specimen in both states.',18,c=muted)
    for c,name in enumerate(['Vanilla control']+names[g*5:g*5+5]):
        # Wrap long headers at spaces, measuring actual text width.
        words=name.split(); lines=['']
        for word in words:
            test=(lines[-1]+' '+word).strip()
            if font(17,True).getlength(test)>cw-8 and lines[-1]:lines.append(word)
            else:lines[-1]=test
        for j,t in enumerate(lines):label(d,(left+c*cw+cw/2,top-20-18*(len(lines)-1-j)),t,17,True,anchor='ms')
    for k in range(2):
        for j,(name,y) in enumerate(zip(species,ys)):
            yy=top+(k*3+j)*ch+(26 if k else 0)
            if j==0:label(d,(10,yy+5),states[g][k],13,True,c=accent)
            label(d,(10,yy+76),name,18)
            for c,x in enumerate(xs):
                x0=left+c*cw+(cw-150)//2
                im.paste(crop(ims[g*2+k],x,y),(x0,yy))
                if c:
                    r=ev[(g*2+k+1,'Plant_'+name,c+1)]
                    status='Fallback' if r['Material ID']=='0' else 'Active' if r['Active']=='True' else 'Dormant'
                    label(d,(left+c*cw+cw/2,yy+151),status,13,c=accent if status=='Active' else muted,anchor='mt')
                else:label(d,(left+c*cw+cw/2,yy+151),'Original',13,c=muted,anchor='mt')
    im.save(out/f'life-suite-group-{g+1}.jpg',quality=94,subsampling=0)
    thumb=im.copy();thumb.thumbnail((740,760));thumb.save(p/f'group-{g+1}-review.jpg',quality=57)
    print('review',g+1,(p/f'group-{g+1}-review.jpg').stat().st_size)
# A compact all-effects daylight/night contact sheet. Pick active state
# independently per trait and show actual native plants, not generated art.
best=[0,1,0,0,0,2,2,2,2,3,4,5,4,4,5,7,7,6,7,7]
cw,ch=378,258
im=Image.new('RGB',(5*cw+24,4*ch+99),bg);d=ImageDraw.Draw(im)
label(d,(14,12),'Responsive Botanical Life  |  20 visual experiments',31,True)
label(d,(14,53),'Actual game captures. Each card: corn, cotton, daylily; state selected for active expression.',19,c=muted)
for i,name in enumerate(names):
    g,c=divmod(i,5);x=12+c*cw;y=87+g*ch
    d.rectangle((x,y,x+cw-6,y+ch-6),fill='#242c27')
    label(d,(x+10,y+8),f'{i+1:02d}  {name}',20,True)
    panel=best[i]
    for j,(s,yy) in enumerate(zip(species,ys)):
        # Crop enlarged from game capture (no modifications).
        pic=ims[panel].crop((xs[c+1]-55,yy-55,xs[c+1]+55,yy+55)).resize((115,115),Image.Resampling.NEAREST)
        xx=x+8+j*120
        im.paste(pic,(xx,y+43))
        r=ev[(panel+1,'Plant_'+s,c+2)]
        label(d,(xx+57,y+163),s,13,anchor='mt')
        status='Fallback' if r['Material ID']=='0' else 'Active' if r['Active']=='True' else 'Dormant'
        label(d,(xx+57,y+182),status,12,c=accent if status=='Active' else muted,anchor='mt')
    label(d,(x+10,y+218),states[g][panel%2],15,c=muted)
im.save(out/'life-suite-gallery.jpg',quality=94,subsampling=0)
print('gallery', (out/'life-suite-gallery.jpg').stat().st_size)
