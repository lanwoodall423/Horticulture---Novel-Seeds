from pathlib import Path
from PIL import Image,ImageDraw,ImageFont,ImageStat
import sys,csv,json,hashlib
p=Path(sys.argv[1]);out=p/'adaptive-gallery';out.mkdir(exist_ok=True)
rows=list(csv.DictReader((p/'HNS-garden-adaptive.tsv').open(encoding='utf-8-sig'),delimiter='\t'))
stages=['01-young','02-mature','03-supplied','04-regrowth','05-recovered']
ev={(r['Phase'],int(r['Column'])):r for r in rows}
bg='#17221b';ink='#e8f0e3';muted='#b8c9b6';accent='#c0d8aa';gold='#e5bf7c'
fonts={}
def f(n,b=False):
    if (n,b) not in fonts:fonts[n,b]=ImageFont.truetype(r'C:\Windows\Fonts\segoeuib.ttf' if b else r'C:\Windows\Fonts\segoeui.ttf',n)
    return fonts[n,b]
def text(d,xy,t,n=18,b=False,c=ink,anchor=None):d.text(xy,str(t),font=f(n,b),fill=c,anchor=anchor)
def load(name):
    im=Image.open(p/f'HNS-garden-adaptive-{name}.png').convert('RGB')
    assert max(ImageStat.Stat(im).stddev)>5,name
    return im
def save(name,images,durations):
    thumbs=[a.resize((300,round(a.height*300/a.width))) for a in images]
    w,h=thumbs[0].size;cols=3
    pal=Image.new('RGB',(w*cols,h*((len(thumbs)+cols-1)//cols)),bg)
    for k,a in enumerate(thumbs):pal.paste(a,(k%cols*w,k//cols*h))
    pal=pal.quantize(colors=256,method=Image.Quantize.MEDIANCUT)
    q=[a.quantize(palette=pal,dither=Image.Dither.NONE) for a in images]
    q[0].save(out/name,save_all=True,append_images=q[1:],duration=durations,loop=0,optimize=True,disposal=1)
    with Image.open(out/name) as a:assert a.n_frames==len(images),(name,a.n_frames,len(images))
    print(name,len(images),(out/name).stat().st_size)
    return dict(file=name,frames=len(images),bytes=(out/name).stat().st_size)
def banner(im,title,sub,foot):
    a=Image.new('RGB',(im.width,im.height+107),bg);d=ImageDraw.Draw(a)
    text(d,(14,6),title,25,True);text(d,(15,40),sub,16,c=muted)
    a.paste(im,(0,68));text(d,(15,im.height+78),foot,15,c=muted)
    return a
def gpu(s,i):
    return Image.open(p/f'HNS-garden-adaptive-gpu-{s}-{i:02d}.png').convert('RGB')
def square(im,n,ref):
    a=Image.new('RGB',(ref,ref),'black');a.paste(im,((ref-im.width)//2,(ref-im.height)//2))
    return a.resize((n,n),Image.Resampling.NEAREST)
def hashim(im):return hashlib.sha256(im.tobytes()).hexdigest()
manifest=[]
# All real map frames, with explicit labels about controlled growth.
present=[s for s in stages if (p/f'HNS-garden-adaptive-{s}.png').exists()]
labels={'01-young':'Early growth; resource-dependent seedlings remain at 25%','02-mature':'Mature crop specimens; resource-dependent seedlings still gated','03-supplied':'Actual colonist delivered required wood to corn; same growth stage','04-regrowth':'Actual perennial corn harvested and retained at regrowth stage','05-recovered':'Controlled return to maturity; paid resource and recorded harvest state'}
assert len(present)>=2
imgs=[banner(load(s),'Adaptive Cultivation | Native colony map',labels[s],'Corn and cotton: vanilla, high yield, cold, heat, resource, perennial, mixed appearance + utility.') for s in present]
manifest.append(save('adaptive-map.gif',imgs,[2400]*len(imgs)))
closeups=[banner(load(s).crop((250,290,1230,620)).resize((1176,396),Image.Resampling.NEAREST),'Adaptive Cultivation | closer native map view',labels[s],'First column: control; then high yield, cold, heat, resource, perennial, mixed traits.') for s in present]
manifest.append(save('adaptive-map-closeup.gif',closeups,[2500]*len(closeups)))
# Gallery uses actual accepted native shader readbacks; early missing source
# anatomy is deliberately not substituted with an invented preview.
specs=[1,2,3,4,5,6,8,9,10,11,12,13]
titles=['High yield','Cold resistant','Heat resistant','Resource','Perennial','Yield + visual traits']
mature='02-mature'
if mature in present:
    ref=max(max(gpu(mature,i).size) for i in specs if (p/f'HNS-garden-adaptive-gpu-{mature}-{i:02d}.png').exists())
    seq=[]
    for s in present:
        a=Image.new('RGB',(1320,618),bg);d=ImageDraw.Draw(a)
        text(d,(16,8),'Adaptive Cultivation | functional cue families',28,True)
        text(d,(16,48),labels[s]+'  |  native material readbacks',17,c=muted)
        for r in range(2):
            y=105+r*240;text(d,(14,y+94),['Corn','Cotton'][r],20,True)
            for c in range(6):
                i=r*7+c+1;x=108+c*200; row=ev.get((s,i),{})
                if r==0:text(d,(x+96,92),titles[c],15,True,accent,'ms')
                d.rounded_rectangle((x,y,x+188,y+218),radius=6,fill='#29382b')
                path=p/f'HNS-garden-adaptive-gpu-{s}-{i:02d}.png'
                if path.exists():a.paste(square(gpu(s,i),180,ref),(x+4,y+5))
                else:text(d,(x+94,y+95),'Source fallback',15,c=muted,anchor='mm')
                growth=float(row.get('Growth',0))*100
                status=('Blocked' if row.get('Resource blocked')=='True' else 'Supplied') if c==3 else ('Regrown' if row.get('Regrown')=='True' else 'First cycle') if c==4 else row.get('Cues','none')
                text(d,(x+94,y+195),f'{growth:.0f}% | {status}',12,c=ink,anchor='mm')
        text(d,(15,598),'Same captured source scale; not a colony map recording. Missing source anatomy is deferred.',14,c=muted)
        seq.append(a)
    manifest.append(save('functional-cultivars.gif',seq,[2300]*len(seq)))
    seq[-1].save(out/'functional-gallery.png')
def focus(name,indices,ss,headline,subtitles):
    if not all((p/f'HNS-garden-adaptive-gpu-{s}-{i:02d}.png').exists() for s,i in zip(ss,indices)):return
    ref=max(max(gpu(s,i).size) for s,i in zip(ss,indices))
    images=[]
    for s,i,subtitle in zip(ss,indices,subtitles):
        row=ev[s,i];a=Image.new('RGB',(760,520),bg);d=ImageDraw.Draw(a)
        text(d,(17,8),headline,29,True)
        text(d,(17,49),subtitle,19,c=accent)
        a.paste(square(gpu(s,i),400,ref),(16,87))
        x=437;text(d,(x,108),'Same live corn plant',19,True)
        text(d,(x,146),'ID: '+row['Plant ID'],17,c=muted)
        text(d,(x,181),'Growth: '+f"{float(row['Growth'])*100:.1f}%",19)
        text(d,(x,216),'Resource blocked: '+row['Resource blocked'],17)
        text(d,(x,251),'Harvest cycle: '+row['Harvest cycle'],17)
        text(d,(x,286),'Regrown: '+row['Regrown'],17)
        text(d,(x,321),'Yield factor: '+row['Yield factor'],17)
        text(d,(x,356),'Growth factor: '+row['Growth factor'],17)
        text(d,(17,497),'Native GPU readback of the actual accepted material. No generated intermediate frames.',14,c=muted)
        images.append(a)
    return save(name,images,[2500]*len(images))
if '03-supplied' in present:
    a,b=gpu('02-mature',4),gpu('03-supplied',4)
    assert hashim(a)!=hashim(b),'resource material did not change'
    assert ev['02-mature',4]['Plant ID']==ev['03-supplied',4]['Plant ID']
    assert ev['02-mature',4]['Growth']==ev['03-supplied',4]['Growth']
    manifest.append(focus('resource-delivery.gif',[4,4],['02-mature','03-supplied'],'Resource-dependent foliage',['Before: growth gate blocked','After: actual fertilizer job completed']))
if '05-recovered' in present and '04-regrowth' in present:
    assert ev['02-mature',5]['Plant ID']==ev['05-recovered',5]['Plant ID']
    assert ev['02-mature',5]['Harvest cycle']=='0' and int(ev['05-recovered',5]['Harvest cycle'])>0
    manifest.append(focus('perennial-harvest.gif',[5,5,5],['02-mature','04-regrowth','05-recovered'],'Perennial harvest and regrowth',['Before: first mature crop','After: real harvest; retained plant','Controlled return to maturity']))
if (p/'HNS-garden-adaptive-06-phenotype.png').exists():
    load('06-phenotype').save(out/'phenotype-tab.png');load('06-phenotype').crop((0,180,570,900)).save(out/'phenotype-closeup.png')
(out/'gif-manifest.json').write_text(json.dumps([m for m in manifest if m],indent=2),encoding='utf-8')
review=load(present[-1]);review.thumbnail((850,500));review.save(p/'adaptive-review.jpg',quality=49)
print('review bytes',(p/'adaptive-review.jpg').stat().st_size)
