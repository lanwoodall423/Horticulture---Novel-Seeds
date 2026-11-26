from pathlib import Path
from PIL import Image,ImageDraw,ImageFont
import csv,json,sys,hashlib
p=Path(sys.argv[1]);out=p/'sculpture-gallery';out.mkdir(exist_ok=True)
rows=list(csv.DictReader((p/'HNS-garden-sculpture-gpu.tsv').open(encoding='utf-8-sig'),delimiter='\t'))
assert len(rows)==135,len(rows)
names=['Circinate Frond','Climbing Helix','Umbel Crown','Pendant Raceme','Crested Fan']
slugs=['circinate-frond','climbing-helix','umbel-crown','pendant-raceme','crested-fan']
descs=['Curled shoots unfold into pinnate fronds.','A winding secondary stem develops alternate leaves.','Reproductive stalks radiate from a common junction.','Arching stems develop hanging reproductive clusters.','A curved crest supports a fan of leaves and organs.']
species=['Corn','Cotton','Daylily'];species_ids=['Plant_'+s for s in species]
bg='#171f1a';ink='#f2f5ed';muted='#aabbb0';accent='#bcd89f'
fonts={}
def font(n,b=False):
    if (n,b) not in fonts:fonts[n,b]=ImageFont.truetype(r'C:\Windows\Fonts\segoeui'+('b' if b else '')+'.ttf',n)
    return fonts[n,b]
def text(d,xy,s,n=16,b=False,c=ink,anchor=None):d.text(xy,s,font=font(n,b),fill=c,anchor=anchor)
def progress(d,x,y,w,f):
    d.rounded_rectangle((x,y,x+w,y+7),radius=3,fill='#3b493d')
    if f:d.rounded_rectangle((x,y,x+w*f/8,y+7),radius=3,fill=accent)
def files(col,s):
    tag=s+'-'+str(col+1)
    src=Image.open(p/f'HNS-garden-sculpture-control-{tag}.png').convert('RGB')
    seq=[Image.open(p/f'HNS-garden-sculpture-gpu-{f:02d}-{tag}.png').convert('RGB') for f in range(1,10)]
    assert len(set(hashlib.sha256(a.tobytes()).hexdigest() for a in seq))>=2
    return src,seq
def tile(src,styled,size):
    w,h=styled.size
    c=Image.new('RGB',(w,h),'black')
    c.paste(src,((w-src.width)//2,(h-src.height)//2))
    def fit(im):
        r=min(size/im.width,size/im.height)
        a=im.resize((round(im.width*r),round(im.height*r)),Image.Resampling.NEAREST)
        o=Image.new('RGB',(size,size),'black')
        o.paste(a,((size-a.width)//2,(size-a.height)//2))
        return o
    return fit(c),fit(styled)
def gif(frames,name):
    thumbs=[]
    for a in frames:
        t=a.copy();t.thumbnail((250,250));thumbs.append(t)
    w,h=thumbs[0].size;pal=Image.new('RGB',(3*w,3*h))
    for i,a in enumerate(thumbs):pal.paste(a,((i%3)*w,(i//3)*h))
    pal=pal.quantize(colors=256,method=Image.Quantize.MEDIANCUT)
    q=[a.quantize(palette=pal,dither=Image.Dither.NONE) for a in frames]
    order=list(range(9))+list(range(7,0,-1))
    durations=[400]+[140]*7+[700]+[140]*7
    q[0].save(out/name,save_all=True,append_images=[q[i] for i in order[1:]],duration=durations,loop=0,optimize=True,disposal=1)
    with Image.open(out/name) as im:
        assert im.n_frames==16,(name,im.n_frames)
        print(name,im.n_frames,(out/name).stat().st_size,flush=True)
        return dict(file=name,source_frames=9,frames=im.n_frames,bytes=(out/name).stat().st_size)
data={(c,s):files(c,s) for c in range(1,6) for s in species_ids}
manifest=[]
for c in range(1,6):
    frames=[]
    for f in range(9):
        a=Image.new('RGB',(715,575),bg);d=ImageDraw.Draw(a)
        text(d,(14,10),f'{c:02d} | {names[c-1]}',29,True)
        text(d,(14,50),descs[c-1],16,c=muted)
        text(d,(14,78),f'Accelerated development preview  {f+1}/9',15,c=accent)
        progress(d,365,87,332,f)
        for r,s in enumerate(species_ids):
            x=111+r*198
            text(d,(x+94,118),species[r],18,True,anchor='ms')
            source,seq=data[c,s]
            control,styled=tile(source,seq[f],188)
            a.paste(control,(x,126));a.paste(styled,(x,328))
        text(d,(14,220),'Source art',14,True,anchor='lm')
        text(d,(14,422),'Cultivar',14,True,c=accent,anchor='lm')
        text(d,(14,548),'RimWorld native GPU material readback | same pixel scale | not a map screenshot',13,c=muted)
        frames.append(a)
    manifest.append(gif(frames,f'{c:02d}-{slugs[c-1]}.gif'))
    frames[-1].save(out/f'{c:02d}-{slugs[c-1]}-mature.png')
frames=[]
for f in range(9):
    a=Image.new('RGB',(1130,706),bg);d=ImageDraw.Draw(a)
    text(d,(14,10),'Botanical Sculpture | five experimental growth rigs',29,True)
    text(d,(14,50),f'Native GPU preview {f+1}/9 | same specimens, shared materials',16,c=muted)
    progress(d,14,79,1102,f)
    left,top,cw,ch=109,130,167,176
    for c,n in enumerate(['Source art']+names):text(d,(left+c*cw+cw/2,top-8),n,15,True,anchor='ms')
    for r,s in enumerate(species_ids):
        y=top+r*ch
        text(d,(12,y+78),species[r],18,True,anchor='lm')
        for c in range(6):
            src,seq=data[max(1,c),s]
            ctrl,styled=tile(src,seq[f],160)
            a.paste(ctrl if c==0 else styled,(left+c*cw+3,y))
    text(d,(14,680),'Native material readbacks | no interpolated frames | controls are actual source sprites',13,c=muted)
    frames.append(a)
manifest.append(gif(frames,'all-five.gif'))
frames[-1].save(out/'mature-gallery.png')
(out/'gif-manifest.json').write_text(json.dumps(manifest,indent=2),encoding='utf-8')
review=frames[-1].copy();review.thumbnail((850,535));review.save(p/'sculpture-review.jpg',quality=65)
print('review bytes', (p/'sculpture-review.jpg').stat().st_size,flush=True)
