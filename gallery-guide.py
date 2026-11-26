from PIL import Image, ImageDraw, ImageFont, ImageStat
from pathlib import Path
import sys, json, hashlib, csv
p=Path(sys.argv[1]); out=p/'guide-gallery'; out.mkdir(exist_ok=True)
sources=sorted(p.glob('HNS-garden-guide-*.png'))
assert len(sources)==10, len(sources)
frames={}
for i,path in enumerate(sources,1):
    im=Image.open(path).convert('RGB')
    assert im.size==(1600,900), (path,im.size)
    assert max(ImageStat.Stat(im).stddev)>5, path
    frames[i]=im
assert len(set(hashlib.sha256(im.tobytes()).hexdigest() for im in frames.values()))==10
def font(size,bold=False):
    return ImageFont.truetype(r'C:\Windows\Fonts\segoeuib.ttf' if bold else r'C:\Windows\Fonts\segoeui.ttf',size)
titles={
1:('A new colony','Empty field guide: no discovered cultivars or pending specimens.'),
2:('Botanical field guide','Documented cultivars, recorded species and discoveries needing attention.'),
3:('Discovery inbox','Locate a specimen, preserve mature seeds or document a recovered pack.'),
4:('Cultivar dossier','Documented traits, genetic modifiers and a qualified mature preview.'),
5:('Compare cultivars','Side-by-side documented genetics, appearance and agricultural modifiers.'),
6:('Breeding and cultivation','Actual growing-zone assignment with two documented cultivars in its mix.'),
7:('Preservation requested','The inbox action created the existing vanilla harvest designation.'),
8:('Novel seeds recovered','A real colonist harvested the specimen and recovered its seed pack.'),
9:('Name the cultivar','The actual documentation job opens this naming window; hidden genes remain undisclosed.'),
10:('Discovery documented','The newly named cultivar is registered and its documented traits are visible.')}
def frame(i):
    im=frames[i].crop((0,150,1190,870))
    a=Image.new('RGB',(1190,im.height+76),'#17231b')
    d=ImageDraw.Draw(a);title,sub=titles[i]
    d.text((15,5),title,font=font(28,True),fill='#e8f0e3')
    d.text((16,39),sub,font=font(17),fill='#b7c9b8')
    a.paste(im,(0,76));return a
manifest=[]
def save(name,ids,durations):
    ims=[frame(i) for i in ids]
    # Shared palette avoids color flicker between screen states.
    small=[a.resize((297,round(a.height*297/a.width))) for a in ims]
    w,h=small[0].size; cols=3
    palette=Image.new('RGB',(w*cols,h*((len(ims)+cols-1)//cols)),'#17231b')
    for k,a in enumerate(small):palette.paste(a,(k%cols*w,k//cols*h))
    palette=palette.quantize(colors=256,method=Image.Quantize.MEDIANCUT)
    q=[a.quantize(palette=palette,dither=Image.Dither.NONE) for a in ims]
    q[0].save(out/name,save_all=True,append_images=q[1:],duration=durations,loop=0,optimize=True,disposal=1)
    with Image.open(out/name) as im:assert im.n_frames==len(ids),(name,im.n_frames)
    manifest.append(dict(file=name,frames=len(ids),source_capture_indices=ids,bytes=(out/name).stat().st_size))
    print(name,len(ids),(out/name).stat().st_size)
save('field-guide-tour.gif',[1,2,3,4,5,6],[2300,3500,3200,3500,3200,3200])
save('discovery-to-cultivar.gif',[3,7,8,9,10],[2800,2800,2800,3500,3500])
save('collection-and-breeding.gif',[2,4,5,6],[3500,3500,3500,3500])
for i,n in [(2,'overview'),(3,'inbox'),(4,'cultivar'),(5,'comparison'),(6,'breeding'),(10,'documented')]:
    frames[i].crop((0,150,1190,870)).save(out/(n+'.png'))
(out/'gif-manifest.json').write_text(json.dumps(manifest,indent=2),encoding='utf-8')
review=frames[2].crop((0,150,1190,870));review.thumbnail((870,540));review.save(p/'overview-review.jpg',quality=57)
print('review bytes',(p/'overview-review.jpg').stat().st_size)
