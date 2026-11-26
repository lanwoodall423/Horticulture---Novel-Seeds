from PIL import Image,ImageDraw,ImageFont
from pathlib import Path
import sys,re,json
p=Path(sys.argv[1]);o=Path(sys.argv[2]);o.mkdir(exist_ok=True)
names=['Photosynthetic Pulse','Nyctinastic Folding','Hydraulic Wilt','Crystal Dew Lensing','Pollination Cloud','Mosaic Organism','Seasonal Color Front','Subsurface Root Halo','Leaf Skeletonization','Bioluminescent Signaling','Wind Differential Anatomy','Fractal Venation Bloom','Translucent Petal Tissue','Canopy Volume Parallax','Micro-Habitat Response','Fruit Ripening Cascade','Frost Etching','Mycelial Relay','Morphogenetic Reconfiguration','Event-Driven Expression']
species=['Corn','Cotton','Daylily']; titles=['Tissue and weather','Seasonal and ground expression','Anatomical motion and habitat','Growth and connected ecology']
states=[('Morning | heat input','Midnight | cool input'),('Noon | autumn input','Midnight | late-autumn input'),('Morning | open plots','Afternoon | western companions'),('72% growth | dry','Mature | rain and frost input')]
xs=[475,598,718,840,965,1085];ys=[278,447,612]
fonts={}
def font(n,b=False):
    k=(n,b)
    if k not in fonts:fonts[k]=ImageFont.truetype(r'C:\Windows\Fonts\segoeui'+('b' if b else '')+'.ttf',n)
    return fonts[k]
def txt(d,xy,s,n=16,b=False,c='#f0f4e9',anchor=None):d.text(xy,s,font=font(n,b),fill=c,anchor=anchor)
def wrap(d,s,x,y,width,n=15):
    lines=['']
    for w in s.split():
        t=(lines[-1]+' '+w).strip()
        if font(n,True).getlength(t)>width and lines[-1]:lines.append(w)
        else:lines[-1]=t
    for j,l in enumerate(lines):txt(d,(x,y+(j-len(lines)+1)*17),l,n,True,anchor='ms')
def save(frames,path):
    pal=Image.new('RGB',(frames[0].width,frames[0].height*2))
    for i,im in enumerate(frames):pal.paste(im,(0,i*im.height))
    pal=pal.quantize(colors=256)
    q=[im.quantize(palette=pal,dither=Image.Dither.NONE) for im in frames]
    q[0].save(path,save_all=True,append_images=q[1:],duration=[1400,1400],loop=0,optimize=True,disposal=1)
    with Image.open(path) as im:assert im.n_frames==2
    print(path.name,path.stat().st_size,flush=True)
    return dict(file=path.name,frames=2,bytes=path.stat().st_size)
manifest=[]
for g in range(4):
    group=[]; singles=[[] for _ in range(5)]
    for phase in range(2):
        im=Image.open(p/f'panel-{2*g+phase+1}.png').convert('RGB')
        assert im.size==(1600,900)
        crops=[[im.crop((x-56,y-56,x+56,y+56)) for x in xs] for y in ys]
        left,top,cw,ch=106,108,148,144
        a=Image.new('RGB',(left+6*cw+10,top+3*ch+40),'#171e1a');d=ImageDraw.Draw(a)
        txt(d,(12,10),f'{g+1:02d} | {titles[g]}',26,True)
        txt(d,(12,48),states[g][phase],17,c='#c1d6a2')
        for c,n in enumerate(['Vanilla control']+names[g*5:g*5+5]):wrap(d,n,left+c*cw+cw/2,top-8,cw-6)
        for r,s in enumerate(species):
            y=top+r*ch;txt(d,(10,y+66),s,17,True,anchor='lm')
            for c in range(6):a.paste(crops[r][c].resize((140,140),Image.Resampling.NEAREST),(left+c*cw+4,y))
        txt(d,(12,top+3*ch+8),f'Actual native capture {phase+1}/2 | no interpolated motion',15,c='#aebdaf')
        group.append(a)
        for c in range(1,6):
            idx=g*5+c;b=Image.new('RGB',(552,416),'#171e1a');dr=ImageDraw.Draw(b)
            txt(dr,(10,8),f'{idx:02d} | {names[idx-1]}',22,True)
            txt(dr,(10,42),states[g][phase],16,c='#c1d6a2')
            for r,s in enumerate(species):
                x=91+r*152;txt(dr,(x+72,78),s,16,True,anchor='ms')
                b.paste(crops[r][0].resize((144,144),Image.Resampling.NEAREST),(x,84))
                b.paste(crops[r][c].resize((144,144),Image.Resampling.NEAREST),(x,234))
            txt(dr,(10,156),'Control',14,True,anchor='lm')
            txt(dr,(10,306),'Cultivar',14,True,c='#c1d6a2',anchor='lm')
            txt(dr,(10,390),f'Actual game capture {phase+1}/2 | paired-state loop',13,c='#aebdaf')
            singles[c-1].append(b)
    manifest.append(save(group,o/f'group-{g+1:02d}.gif'))
    for c,frames in enumerate(singles):
        i=g*5+c;slug=re.sub(r'[^a-z0-9]+','-',names[i].lower()).strip('-')
        manifest.append(save(frames,o/f'{i+1:02d}-{slug}.gif'))
(o/'gif-manifest.json').write_text(json.dumps(manifest,indent=2),encoding='utf-8')
