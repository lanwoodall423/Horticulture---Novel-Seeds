from pathlib import Path
from PIL import Image,ImageDraw,ImageFont,ImageStat
import sys,hashlib,json
p=Path(sys.argv[1]);out=p/'ornamental-gallery';out.mkdir(exist_ok=True)
def font(size,b=False):return ImageFont.truetype(r'C:\Windows\Fonts\segoeuib.ttf' if b else r'C:\Windows\Fonts\segoeui.ttf',size)
def txt(d,xy,s,n=18,b=False,c='#e5eee3'):d.text(xy,s,font=font(n,b),fill=c)
def read(name):
    im=Image.open(p/name).convert('RGB')
    assert max(ImageStat.Stat(im).stddev)>5,name
    return im
growth=[40,65,85,100]
def gallery(i):
    im=read(f'HNS-garden-ornamental-map-{i:02d}.png')
    im.thumbnail((1200,730))
    a=Image.new('RGB',(im.width,im.height+114),'#17231b');d=ImageDraw.Draw(a)
    txt(d,(15,7),'Adaptive breeding | independent inheritance',27,True)
    txt(d,(15,45),f'Actual growth {growth[i-1]}% | maternal parent, donor parent, inherited offspring',17,False,'#bdcfbc')
    a.paste(im,(0,74))
    txt(d,(15,im.height+82),'One mechanical donor slot + one neutral ornamental donor slot. Native colony-map screenshot.',16,False,'#bdcfbc')
    return a
def details(i):
    a=Image.new('RGB',(970,755),'#17231b');d=ImageDraw.Draw(a)
    txt(d,(18,9),'Ornamental inheritance | parent and offspring',28,True)
    txt(d,(18,47),f'Actual growth {growth[i-1]}% | native GPU readbacks of accepted live-map materials',17,False,'#bdcfbc')
    txt(d,(230,89),'Donor cultivar',20,True);txt(d,(590,89),'Crossbred offspring',20,True)
    txt(d,(230,118),'Cold-Resistant + Crimson Network',16,False,'#b9cdb9')
    txt(d,(590,118),'High-Yield + both donor traits',16,False,'#b9cdb9')
    for r,s in enumerate(['Corn','Cotton','Daylily']):
        y=152+r*194;txt(d,(20,y+78),s,23,True)
        for c,idx in enumerate([r*3+1,r*3+2]):
            img=read(f'HNS-garden-ornamental-gpu-{i:02d}-{idx:02d}.png')
            x=224+c*360
            d.rounded_rectangle((x,y,x+310,y+184),radius=7,fill='#101812',outline='#3f5444')
            scale=min(174/img.height,174/img.width)
            img=img.resize((round(img.width*scale),round(img.height*scale)),Image.Resampling.NEAREST)
            a.paste(img,(x+(310-img.width)//2,y+(184-img.height)//2))
    txt(d,(18,738),'Actual shader readback; sway frozen. Growth advanced in controlled steps.',14,False,'#bdcfbc')
    return a
def save(seq,name):
    thumbs=[i.resize((240,round(240*i.height/i.width))) for i in seq]
    w,h=thumbs[0].size;palette=Image.new('RGB',(w*4,h))
    for j,t in enumerate(thumbs):palette.paste(t,(j*w,0))
    palette=palette.quantize(colors=256)
    q=[im.quantize(palette=palette,dither=Image.Dither.NONE) for im in seq]
    order=[0,1,2,3,2,1];q[0].save(out/name,save_all=True,append_images=[q[i] for i in order[1:]],duration=[1000,700,700,1500,700,700],loop=0,optimize=True,disposal=1)
    with Image.open(out/name) as a:assert a.n_frames==6
    return dict(file=name,frames=6,source_frames=4,bytes=(out/name).stat().st_size)
frames=[gallery(i) for i in range(1,5)];detail=[details(i) for i in range(1,5)]
assert len({hashlib.sha256(i.tobytes()).hexdigest() for i in frames})==4
manifest=[save(frames,'independent-breeding-map.gif'),save(detail,'inherited-crimson-network.gif')]
detail[-1].save(out/'mature-offspring.png')
review=frames[-1].copy();review.thumbnail((800,550));review.save(p/'map-review.jpg',quality=56)
print('review bytes',(p/'map-review.jpg').stat().st_size)
(out/'gif-manifest.json').write_text(json.dumps(manifest,indent=2))
print(manifest)
