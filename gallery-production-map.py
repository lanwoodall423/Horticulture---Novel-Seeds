from PIL import Image,ImageDraw,ImageFont,ImageStat,ImageChops
from pathlib import Path
import sys,json,csv,hashlib
p=Path(sys.argv[1]);out=p/'production-gallery';out.mkdir(exist_ok=True)
fontfile=r'C:\Windows\Fonts\segoeui.ttf';boldfile=r'C:\Windows\Fonts\segoeuib.ttf'
def f(n,b=False):return ImageFont.truetype(boldfile if b else fontfile,n)
def text(d,x,y,s,size=18,b=False,fill='#e8efe4'):d.text((x,y),str(s),font=f(size,b),fill=fill)
def get(name):
    a=Image.open(p/f'HNS-garden-production-{name}.png').convert('RGB')
    print(name,a.size,ImageStat.Stat(a).stddev,a.getbbox())
    if min(ImageStat.Stat(a).stddev)<5:raise ValueError('blank screenshot '+name)
    return a
def frame(im,title,sub,foot,maxwidth=1200,crop=None):
    if crop:im=im.crop(crop)
    im.thumbnail((maxwidth,800),Image.Resampling.LANCZOS)
    a=Image.new('RGB',(im.width,im.height+108),'#18231d');d=ImageDraw.Draw(a)
    text(d,13,5,title,25,True);text(d,13,36,sub,15,False,'#c1d0bf')
    a.paste(im,(0,62));text(d,13,im.height+74,foot,14,False,'#c1d0bf')
    return a
def gif(seq,path,order,duration):
    # One palette across all captured frames; no generated intermediate frames.
    small=[]
    for a in seq:
        t=a.copy();t.thumbnail((270,200));small.append(t)
    w,h=small[0].size
    pal=Image.new('RGB',(w*3,h*((len(seq)+2)//3)))
    for i,a in enumerate(small):pal.paste(a,(i%3*w,i//3*h))
    pal=pal.quantize(colors=256,method=Image.Quantize.MEDIANCUT)
    q=[a.quantize(palette=pal,dither=Image.Dither.NONE) for a in seq]
    q[order[0]].save(out/path,save_all=True,append_images=[q[i] for i in order[1:]],duration=duration,loop=0,optimize=True,disposal=1)
    with Image.open(out/path) as a:print(path,a.n_frames,(out/path).stat().st_size);assert a.n_frames==len(order)
    return dict(file=path,frames=len(order),source_frames=len(seq),bytes=(out/path).stat().st_size)
manifest=[]
showcase=[get('showcase-%02d'%i) for i in range(2,7)]
assert len({hashlib.sha256(a.tobytes()).hexdigest() for a in showcase})==5
growth=[55,65,80,90,100]
seq=[frame(im,'Inherited Botanical Expression | colony map',f'Actual growth {growth[i]}%  |  Corn, cotton and daylily', 'First column: original control. Next three: same crossbred cultivar. Last two: comparison cultivar.') for i,im in enumerate(showcase)]
manifest.append(gif(seq,'map-growth.gif',[0,1,2,3,4,3,2,1],[1000,850,850,850,1500,850,850,850]))
cropseq=[frame(im,'Inherited cultivars | closer view',f'Actual growth {growth[i]}% | cropped native map screenshot', 'Corn, cotton and daylily. Rice companions change during the controlled growth test.',crop=(440,175,1200,690)) for i,im in enumerate(showcase)]
manifest.append(gif(cropseq,'map-growth-closeup.gif',[0,1,2,3,4,3,2,1],[1000,850,850,850,1500,850,850,850]))
before=get('field-before-save');after=get('field-after-reload')
seq=[frame(before,'Botanical Expression | 300-plant field','Before full colony save','Live map capture. Shared materials, actual cultivar IDs and recorded growth history.'),frame(after,'Botanical Expression | 300-plant field','After full colony reload','Live map capture from reloaded plant instances; identity and growth history checks performed.')]
manifest.append(gif(seq,'field-save-reload.gif',[0,1],[1900,1900]))
seq=[frame(showcase[-1],'Botanical Expression | individual cultivar garden','Mature crossbred specimens, three original controls','Actual colony-map capture; no synthetic plant images.'),frame(before,'Botanical Expression | full test field','300 actual plants, original and crossbred cultivars','The same showcase specimens are retained inside the larger field.'),frame(after,'Botanical Expression | after colony reload','Re-created plant instances in the reloaded save','Stable genetic identity and history; real full save/reload.')]
manifest.append(gif(seq,'garden-to-field.gif',[0,1,2],[1600,1600,2000]))
showcase[-1].save(out/'mature-map.png');after.save(out/'field-reloaded.png')
review=showcase[-1].copy();review.thumbnail((900,506));review.save(p/'map-review.jpg',quality=60)
review=after.copy();review.thumbnail((900,506));review.save(p/'field-review.jpg',quality=60)
(out/'gif-manifest.json').write_text(json.dumps(manifest,indent=2),encoding='utf-8')
print('review bytes',(p/'map-review.jpg').stat().st_size,(p/'field-review.jpg').stat().st_size)
