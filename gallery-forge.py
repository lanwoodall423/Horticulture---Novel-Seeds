from pathlib import Path
from PIL import Image,ImageDraw,ImageFont
import csv,json,sys,hashlib,re
p=Path(sys.argv[1]);out=p/'forge-gallery';out.mkdir(exist_ok=True)
rows=list(csv.DictReader((p/'HNS-garden-sculpture-gpu.tsv').open(encoding='utf-8-sig'),delimiter='\t'))
assert len(rows)==540,len(rows)
names=['Banyan Props','Candelabra Tips','Stolon Daughters','Braided Leaders','Winged Petioles','Pitcher Leaves','Hooded Spathes','Epiphyllous Bloom','Scale Phyllotaxis','Serrated Lamina','Dehiscent Capsules','Winged Samaras','Anther Emergence','Petaloid Doubling','Seed Shedding','Prismatic Lamina','Rolling Lamina','Amber Resin','Exfoliating Bark','Self-Shadow Lace']
slugs=[re.sub(r'[^a-z0-9]+','-',s.lower()).strip('-') for s in names]
descs=['Descending aerial support roots.','Sweeping arms with upright reproductive apices.','Creeping runners carrying miniature daughter plants.','Intertwining leafy leaders.','Paired blade extensions along the petiole.','Tubular leaf organs with shaded throats.','Leaf hoods part around reproductive tissue.','Reproductive organs form directly on leaf blades.','Overlapping scale leaves form a spiral rosette.','Compound leaves develop toothed margins.','Pod valves part to reveal a seed column.','Paired winged seeds unfold.','Anther filaments extend from reproductive organs.','Double whorls of source-derived organs.','Decorative seeds descend from attached structures.','Localized thin-film-like iridescence.','Rolled blades open to reveal their broad faces.','Glossy amber droplets extend from petioles.','Curling basal bark-like ribbons.','Moving silhouette-derived local foliage shadows.']
species=['Corn','Cotton','Daylily'];ids=['Plant_'+s for s in species]
groups=['Unusual growth architectures','Novel botanical organs','Reproductive mechanisms','Unusual tissue properties']
bg='#171f1a';ink='#f2f5ed';muted='#aabbb0';accent='#bcd89f'
fonts={}
def font(n,b=False):
    if (n,b) not in fonts:fonts[n,b]=ImageFont.truetype(r'C:\Windows\Fonts\segoeui'+('b' if b else '')+'.ttf',n)
    return fonts[n,b]
def text(d,xy,s,n=16,b=False,c=ink,anchor=None):d.text(xy,s,font=font(n,b),fill=c,anchor=anchor)
def progress(d,x,y,w,f):
    d.rounded_rectangle((x,y,x+w,y+7),radius=3,fill='#3b493d')
    if f:d.rounded_rectangle((x,y,x+w*f/8,y+7),radius=3,fill=accent)
def data_for(i,s):
    mode=26+i;start=(i//5)*9+1
    tag=s+'-'+str(mode)
    path=p/f'HNS-garden-sculpture-control-{tag}.png'
    src=Image.open(path).convert('RGB')
    seq=[Image.open(p/f'HNS-garden-sculpture-gpu-{f:02d}-{tag}.png').convert('RGB') for f in range(start,start+9)]
    unique=len({hashlib.sha256(a.tobytes()).hexdigest() for a in seq})
    assert unique>=2,(i,s,unique)
    return src,seq,unique
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
data={(i,s):data_for(i,s) for i in range(20) for s in ids}
manifest=[]
for i in range(20):
    frames=[]
    for f in range(9):
        a=Image.new('RGB',(715,575),bg);d=ImageDraw.Draw(a)
        text(d,(14,10),f'{i+1:02d} | {names[i]}',29,True)
        text(d,(14,50),descs[i],16,c=muted)
        text(d,(14,78),f'Controlled phase preview  {f+1}/9',15,c=accent)
        progress(d,365,87,332,f)
        for r,s in enumerate(ids):
            x=111+r*198
            text(d,(x+94,118),species[r],18,True,anchor='ms')
            source,seq,_=data[i,s]
            control,styled=tile(source,seq[f],188)
            a.paste(control,(x,126));a.paste(styled,(x,328))
        text(d,(14,220),'Source art',14,True,anchor='lm')
        text(d,(14,422),'Cultivar',14,True,c=accent,anchor='lm')
        text(d,(14,548),'RimWorld native GPU material readback | same pixel scale | not a map screenshot',13,c=muted)
        frames.append(a)
    manifest.append(gif(frames,f'{i+1:02d}-{slugs[i]}.gif'))
    frames[-1].save(out/f'{i+1:02d}-{slugs[i]}-final.png')
for g in range(4):
    frames=[]
    for f in range(9):
        a=Image.new('RGB',(1130,706),bg);d=ImageDraw.Draw(a)
        text(d,(14,10),f'Botanical Forge | {groups[g]}',29,True)
        text(d,(14,50),f'Native GPU preview {f+1}/9 | same specimens, shared materials',16,c=muted)
        progress(d,14,79,1102,f)
        left,top,cw,ch=109,130,167,176
        for c,n in enumerate(['Source art']+names[g*5:g*5+5]):
            text(d,(left+c*cw+cw/2,top-8),n,14,True,anchor='ms')
        for r,s in enumerate(ids):
            y=top+r*ch
            text(d,(12,y+78),species[r],18,True,anchor='lm')
            for c in range(6):
                i=g*5+max(0,c-1)
                src,seq,_=data[i,s]
                ctrl,styled=tile(src,seq[f],160)
                a.paste(ctrl if c==0 else styled,(left+c*cw+3,y))
        text(d,(14,680),'Native material readbacks | no interpolated frames | controls are actual source sprites',13,c=muted)
        frames.append(a)
    manifest.append(gif(frames,f'group-{g+1:02d}.gif'))
    frames[-1].save(out/f'group-{g+1:02d}-final.png')
    review=frames[-1].copy();review.thumbnail((750,470));review.save(p/f'forge-review-{g+1}.jpg',quality=60)
# All-twenty GIF, three species in each feature card.
frames=[]
for f in range(9):
    a=Image.new('RGB',(1290,785),bg);d=ImageDraw.Draw(a)
    text(d,(14,10),'Botanical Forge | 20 native visual experiments',29,True)
    text(d,(14,49),f'Controlled phase {f+1}/9 | corn, cotton, daylily in each card',17,c=muted)
    progress(d,14,79,1260,f)
    cw,ch=252,168
    for i in range(20):
        x=14+(i%5)*cw;y=103+(i//5)*ch
        d.rectangle((x,y,x+cw-6,y+ch-6),fill='#222e25')
        text(d,(x+7,y+5),f'{i+1:02d} | {names[i]}',17,True)
        for j,s in enumerate(ids):
            src,seq,_=data[i,s];_,styled=tile(src,seq[f],78)
            xx=x+6+j*80;a.paste(styled,(xx,y+37))
            text(d,(xx+39,y+121),species[j],12,anchor='mt',c=muted)
    frames.append(a)
manifest.append(gif(frames,'all-twenty.gif'))
frames[-1].save(out/'all-twenty-final.png')
(out/'gif-manifest.json').write_text(json.dumps(manifest,indent=2),encoding='utf-8')
(out/'distinct-frames.json').write_text(json.dumps([dict(effect=names[i],species=s,unique_frames=data[i,s][2]) for i in range(20) for s in ids],indent=2),encoding='utf-8')
print('review sizes',[(i,(p/f'forge-review-{i}.jpg').stat().st_size) for i in range(1,5)],flush=True)
print('total GIF bytes',sum(m['bytes'] for m in manifest),flush=True)
