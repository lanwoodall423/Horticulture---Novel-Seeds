from pathlib import Path
from PIL import Image, ImageDraw, ImageFont, ImageStat
import sys, json, hashlib
p=Path(sys.argv[1]); out=p/'production-gallery'; out.mkdir(exist_ok=True)
names=['Corn','Cotton','Daylily']; stages=['showcase-%02d'%i for i in range(1,7)]
growth=[40,55,65,80,90,100]
ids=[i for i in range(18) if i%6!=0]
def font(size,bold=False): return ImageFont.truetype(r'C:\Windows\Fonts\segoeuib.ttf' if bold else r'C:\Windows\Fonts\segoeui.ttf',size)
def text(d,x,y,s,size=20,bold=False,fill='#eaf1e7'): d.text((x,y),s,font=font(size,bold),fill=fill)
def image(stage,i):
    a=Image.open(p/f'HNS-garden-production-gpu-{stage}-{i:02d}.png').convert('RGB')
    assert max(ImageStat.Stat(a).stddev)>5, (stage,i,'blank')
    return a
def card(stage,title,subtitle,rows):
    w=1180; tile=208; h=140+len(rows)*228
    a=Image.new('RGB',(w,h),'#17221c'); d=ImageDraw.Draw(a)
    text(d,24,17,title,30,True); text(d,24,56,subtitle,18,False,'#bbcdbb')
    for j,s in enumerate(['Sibling 1','Sibling 2','Sibling 3','Comparison 1','Comparison 2']):
        text(d,126+j*tile,98,s,18,True,'#b5c9ad')
    for r,rr in enumerate(rows):
        y=132+r*228; text(d,24,y+94,names[rr],21,True)
        for c in range(5):
            im=image(stage,rr*6+c+1); im.thumbnail((190,198),Image.Resampling.NEAREST)
            # Enlarge the native material output without generating new details.
            k=min(190/im.width,198/im.height); im=im.resize((max(1,round(im.width*k)),max(1,round(im.height*k))),Image.Resampling.NEAREST)
            x=126+c*tile; d.rounded_rectangle((x-3,y,x+198,y+216),radius=8,fill='#0b100d',outline='#354b38')
            a.paste(im,(x+(190-im.width)//2,y+(216-im.height)//2))
    return a
def save(frames,name,durations):
    small=[f.resize((236,round(f.height*236/f.width))) for f in frames]
    h=small[0].height; palette=Image.new('RGB',(236*len(small),h))
    for j,i in enumerate(small):palette.paste(i,(j*236,0))
    palette=palette.quantize(colors=256)
    q=[f.quantize(palette=palette,dither=Image.Dither.NONE) for f in frames]
    q[0].save(out/name,save_all=True,append_images=q[1:],loop=0,duration=durations,optimize=True,disposal=1)
    with Image.open(out/name) as a: print(name,a.n_frames,(out/name).stat().st_size)
    return {'file':name,'frames':len(frames),'bytes':(out/name).stat().st_size}
present=[s for s in stages if all((p/f'HNS-garden-production-gpu-{s}-{i:02d}.png').exists() for i in ids)]
assert len(present)>1, present
manifest=[]
for r in range(3):
    frames=[card(s,f'{names[r]} | inherited botanical expression',f'Actual plant growth: {growth[stages.index(s)]}%  |  GPU readback of live materials (not map screenshot)',[r]) for s in present]
    manifest.append(save(frames+frames[-2:0:-1],names[r].lower()+'-growth.gif',[780]*(2*len(frames)-2)))
frames=[card(s,'Crossbred cultivar garden',f'Actual growth: {growth[stages.index(s)]}%  |  Native material readbacks from the live test map',[0,1,2]) for s in present]
manifest.append(save(frames+frames[-2:0:-1],'all-crossbred-growth.gif',[780]*(2*len(frames)-2)))
frames[-1].save(out/'mature-cultivars.png')
if all((p/f'HNS-garden-production-gpu-field-{s}-{i:02d}.png').exists() for s in ['before-save','after-reload'] for i in ids):
    frames=[card('field-'+s,'Save / reload | 300-plant field',label+'  |  Same 15 representative plants, native GPU readbacks',[0,1,2]) for s,label in [('before-save','Before saving'),('after-reload','After full colony reload')]]
    manifest.append(save(frames,'save-reload.gif',[1800,1800]))
    frames[-1].save(out/'reloaded-cultivars.png')
(out/'gif-manifest.json').write_text(json.dumps(manifest,indent=2),encoding='utf-8')
review=Image.open(out/'mature-cultivars.png');review.thumbnail((800,600));review.save(p/'review.jpg',quality=65)
print('review bytes:',(p/'review.jpg').stat().st_size)
