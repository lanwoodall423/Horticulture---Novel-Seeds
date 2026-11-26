from pathlib import Path
from PIL import Image,ImageDraw,ImageFont
import csv,json,sys,hashlib
p=Path(sys.argv[1]);out=p/'expression-gallery';out.mkdir(exist_ok=True)
rows=list(csv.DictReader((p/'HNS-garden-expression.tsv').open(encoding='utf-8-sig'),delimiter='\t'))
assert len(rows)==90,len(rows)
ev={(int(r['Phase']),r['Species'],int(r['Column'])):r for r in rows}
species=['Corn','Cotton','Daylily'];ids=['Plant_'+s for s in species]
names=[['Bronze Wing','Ivory Candelabra','Crimson Pendant'],['Rose Pendant','Ivory Umbel','Bronze Double'],['Ivory Umbel','Crimson Hood','Bronze Double']]
bg='#17211b';ink='#f2f5ed';muted='#afc0b0';accent='#c4dda9';tile='#27342b';gold='#e1bd7e'
fonts={}
def font(n,b=False):
    if (n,b) not in fonts:fonts[n,b]=ImageFont.truetype(r'C:\Windows\Fonts\segoeui'+('b' if b else '')+'.ttf',n)
    return fonts[n,b]
def text(d,xy,s,n=15,b=False,c=ink,anchor=None):d.text(xy,str(s),font=font(n,b),fill=c,anchor=anchor)
def tilepic(im,side,reference):
    # Constant pixel scale across all phases and matched sources.
    src=Image.new('RGB',(reference,reference),'black')
    src.paste(im,((reference-im.width)//2,(reference-im.height)//2))
    src=src.resize((side,side),Image.Resampling.NEAREST)
    return src
data={}
for phase in range(1,7):
    for s in ids:
        for col in range(2,7):
            tag=s+'-'+str(col);f=p/f'HNS-garden-expression-{phase:02d}-{tag}.png'
            c=p/f'HNS-garden-expression-control-{phase:02d}-{tag}.png'
            data[phase,s,col]=(Image.open(c).convert('RGB'),Image.open(f).convert('RGB'))
reference=max(max(a.size) for pair in data.values() for a in pair)
assert reference>=64
def badge(d,x,y,w,label,c=accent):
    d.rounded_rectangle((x,y,x+w,y+25),radius=4,fill=tile)
    text(d,(x+w/2,y+12),label,14,True,c,anchor='mm')
def state(row):
    n=len([a for a in row['Accepted'].split(',') if a])
    return f"{n} channels" if row['Mesh payload']=='True' else 'Source fallback'
def save(frames,name):
    thumbs=[]
    for a in frames:
        t=a.copy();t.thumbnail((330,220));thumbs.append(t)
    w,h=thumbs[0].size;pal=Image.new('RGB',(3*w,2*h))
    for i,a in enumerate(thumbs):pal.paste(a,(i%3*w,i//3*h))
    pal=pal.quantize(colors=256,method=Image.Quantize.MEDIANCUT)
    q=[a.quantize(palette=pal,dither=Image.Dither.NONE) for a in frames]
    order=list(range(6))+list(range(4,0,-1));dur=[650]+[370]*4+[950]+[370]*4
    q[0].save(out/name,save_all=True,append_images=[q[i] for i in order[1:]],duration=dur,loop=0,optimize=True,disposal=1)
    with Image.open(out/name) as im:
        assert im.n_frames==10,(name,im.n_frames)
        print(name,im.n_frames,(out/name).stat().st_size,flush=True)
        return dict(file=name,frames=im.n_frames,source_frames=6,bytes=(out/name).stat().st_size)
def title(d,headline,sub,phase,width):
    text(d,(15,10),headline,28,True)
    text(d,(15,48),sub,16,c=muted)
    growth=float(ev[phase,ids[0],2]['Growth'])
    text(d,(15,77),f"Actual growth {growth*100:.0f}%  |  Native material readback  |  Phase {phase}/6",15,True,accent)
    d.rounded_rectangle((15,105,width-15,111),radius=3,fill='#415345')
    d.rounded_rectangle((15,105,15+(width-30)*(phase/6),111),radius=3,fill=accent)
manifest=[]
for j,s in enumerate(ids):
    seq=[]
    for phase in range(1,7):
        a=Image.new('RGB',(1120,647),bg);d=ImageDraw.Draw(a)
        title(d,f'{species[j]} | coordinated cultivar identities','Three individuals share the first registered cultivar. Other two carry different coordinated genetics.',phase,a.width)
        left=102;cw=200;side=185
        badge(d,left,125,3*cw-9,names[j][0]+'  |  SAME GENETICS')
        badge(d,left+3*cw,125,cw-9,names[j][1],gold);badge(d,left+4*cw,125,cw-9,names[j][2],gold)
        for col in range(2,7):
            x=left+(col-2)*cw
            r=ev[phase,s,col];source,styled=data[phase,s,col]
            text(d,(x+side/2,178),f"Individual {col-1}  /  seed {r['Seed']}",14,True,anchor='ms')
            a.paste(tilepic(source,side,reference),(x,185))
            a.paste(tilepic(styled,side,reference),(x,390))
            text(d,(x+side/2,585),state(r),14,True,c=accent if r['Mesh payload']=='True' else gold,anchor='mt')
            text(d,(x+side/2,606),f"History {r['History code']}  |  bias {float(r['Bias']):+.2f}",13,c=muted,anchor='mt')
        text(d,(12,278),'Source',14,True,anchor='lm')
        text(d,(12,480),'Composed',14,True,c=accent,anchor='lm')
        text(d,(15,628),'Actual native shader previews at equal pixel scale; no generated intermediate images or colony-map capture.',13,c=muted)
        seq.append(a)
    manifest.append(save(seq,f'{species[j].lower()}-identities.gif'))
    seq[-1].save(out/f'{species[j].lower()}-mature.png')
seq=[]
for phase in range(1,7):
    a=Image.new('RGB',(1190,795),bg);d=ImageDraw.Draw(a)
    title(d,'Botanical Expression Engine | living cultivar identities','Nine registered recipes across three species. First three columns share their respective cultivar.',phase,a.width)
    left=118;cw=210;side=182
    badge(d,left,124,3*cw-10,'SAME CULTIVAR WITHIN EACH SPECIES')
    badge(d,left+3*cw,124,cw-10,'OTHER CULTIVAR',gold);badge(d,left+4*cw,124,cw-10,'OTHER CULTIVAR',gold)
    for j,s in enumerate(ids):
        y=164+j*204
        text(d,(10,y+86),species[j],19,True,anchor='lm')
        for col in range(2,7):
            x=left+(col-2)*cw;source,styled=data[phase,s,col];r=ev[phase,s,col]
            a.paste(tilepic(styled,side,reference),(x+9,y))
            text(d,(x+100,y+185),f"{state(r)}  |  seed {r['Seed']}",13,c=muted,anchor='mt')
    text(d,(15,778),'Off-screen native material render; actual growth and recorded neighboring conditions, accelerated in test fixture.',12,c=muted)
    seq.append(a)
manifest.append(save(seq,'all-identities.gif'))
seq[-1].save(out/'mature-gallery.png')
# A focused GIF on same-cultivar individuality and persistent histories.
seq=[]
for phase in range(1,7):
    a=Image.new('RGB',(880,770),bg);d=ImageDraw.Draw(a)
    title(d,'One cultivar, three individual histories','Each row has one genotype. Growth conditions differ across the three specimens.',phase,a.width)
    left=142;cw=236;side=190
    for c,label in enumerate(['Western neighbors','Eastern neighbors','Mixed history']):
        badge(d,left+c*cw,125,cw-9,label)
    for j,s in enumerate(ids):
        y=165+j*195
        text(d,(13,y+82),species[j],18,True,anchor='lm')
        for c in range(3):
            col=c+2;x=left+c*cw;source,styled=data[phase,s,col];r=ev[phase,s,col]
            a.paste(tilepic(styled,side,reference),(x+20,y))
            text(d,(x+115,y+172),f"Seed {r['Seed']}  |  History {r['History code']}  |  {float(r['Bias']):+.2f}",13,c=accent,anchor='mt')
    text(d,(15,752),'Mature samples have no immediate lateral neighbors in these columns; earlier growth observations persist.',12,c=muted)
    seq.append(a)
manifest.append(save(seq,'individual-histories.gif'))
(out/'gif-manifest.json').write_text(json.dumps(manifest,indent=2),encoding='utf-8')
review=Image.open(out/'mature-gallery.png');review.thumbnail((850,570));review.save(p/'expression-review.jpg',quality=61)
print('review bytes',(p/'expression-review.jpg').stat().st_size,flush=True)
print('accepted',sum(r['Mesh payload']=='True' for r in rows),'mature accepted',sum(r['Phase']=='6' and r['Mesh payload']=='True' for r in rows),flush=True)
