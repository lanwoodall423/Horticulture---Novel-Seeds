#version 450
layout(location=0) in vec4 vColor;
layout(location=1) in vec2 vUv;
layout(location=2) flat in float vFallSeed;
layout(location=0) out vec4 outColor;
layout(set=2,binding=0) uniform MaterialUniforms
{
    vec4 _Color; // @color @default = vec4(1.0,1.0,1.0,1.0)
    vec4 _MainTex_ST; // @default = vec4(0.0,0.0,1.0,1.0)
    float _SwayHead; // @default = 0.0
    vec4 _HnsLife; // @default = vec4(1.0,0.0,0.0,0.0)
    vec4 _HnsLifeEnv; // @default = vec4(1.0,0.0,0.5,0.0)
} uMaterial;
layout(set=2,binding=1) uniform sampler2D _MainTex;
layout(set=2,binding=2) uniform sampler2D _HnsAnatomy;
layout(set=2,binding=3) uniform sampler2D _HnsLifeAtlas;
float hash(vec2 p){return fract(sin(dot(p,vec2(127.1,311.7)))*43758.5453);}
float sat(float v){return clamp(v,0.,1.);}
bool outside(vec2 p){return any(lessThan(p,vec2(0)))||any(greaterThan(p,vec2(1)));}
vec4 anatomy(vec2 uv){return texture(_HnsAnatomy,vec2(uv.x,1.-uv.y));}
vec4 atlas(vec2 uv,float frame)
{
    vec2 pad=1.5/vec2(textureSize(_HnsLifeAtlas,0));
    vec2 local=clamp(vec2(uv.x,1.-uv.y),pad,1.-pad);
    vec2 cell=vec2(mod(frame,3.),2.-floor(frame/3.));
    return texture(_HnsLifeAtlas,(cell+local)/3.);
}
void main()
{
    int mode=int(uMaterial._HnsLife.x+.5),role=int(uMaterial._HnsLife.y+.5);
    vec4 e=uMaterial._HnsLifeEnv;
    float light=e.x,wet=e.y,day=e.z,season=e.w;
    float growth=vColor.r,stress=vColor.g;
    int bits=int(vColor.b*3.+.5);
    float seed=fract(sin(vFallSeed*12.9898)*43758.5453);
    float time=day*6.2831853;
    float phase=.5+.5*sin(uMaterial._SwayHead*.61+seed*3.14+time*2.);
    vec2 uv=vUv;
    if(role==1)
    {
        if(mode==5)
        {
            if(growth<.65||light<.08||wet>.65)discard;
            // Discrete sparse drifting pollen grains, no plant-wide halo.
            vec2 q=(uv-vec2(.5,.61))*vec2(1.1,1.4);
            if(length(q)>.46||uv.y<.43)discard;
            vec2 p=uv*vec2(15,13)+vec2(time*.7,-time*.18),id=floor(p);
            vec2 center=vec2(.20+.60*hash(id+seed),.20+.60*hash(id+vec2(9,2)+seed));
            float dust=1.-smoothstep(.065,.13,length(fract(p)-center));
            if(hash(id+vec2(8,1)+seed)<.75||dust<.55)discard;
            outColor=vec4(vec3(.94,.82,.46)*(.72+.28*dust),dust);return;
        }
        if(mode==8||mode==18)
        {
            vec2 p=(uv-.5)*2.;float r=length(p);
            if(mode==8)
            {
                if(r>.94)discard;
                float a=atan(p.y,p.x),wave=sin(a*7.+seed*6.+r*12.);
                float roots=1.-smoothstep(.09,.25,abs(wave));
                roots*=smoothstep(.13,.30,r)*(1.-smoothstep(.67,.95,r));
                float branch=(1.-smoothstep(.05,.18,abs(sin(a*14.-r*17.+seed*5.))))*smoothstep(.35,.63,r)*(1.-smoothstep(.75,.94,r));
                float m=max(roots*.86,branch*.43);
                if(m<.38)discard;
                outColor=vec4(mix(vec3(.23,.17,.11),vec3(.54,.43,.26),m),1);return;
            }
            if(bits==0)discard;
            bool side=p.x<0.?(bits&1)!=0:(bits&2)!=0;
            if(!side||abs(p.x)>.93)discard;
            float wave=sin(abs(p.x)*13.+seed*2.)*.10;
            float line=1.-smoothstep(.014,.033,abs(p.y-wave));
            float thread=(1.-smoothstep(.008,.025,abs(p.y-wave*.5-.045*sin(p.x*28.))))*.8;
            float dotpos=fract(abs(p.x)*3.8-time*.6+seed);
            float pulse=(1.-smoothstep(.04,.20,abs(dotpos-.5)))*line;
            if(max(line,thread)<.45)discard;
            outColor=vec4(mix(vec3(.40,.55,.28),vec3(.88,.84,.48),pulse),1);return;
        }
        if(mode==10)
        {
            vec4 s=texture(_MainTex,uv),a=anatomy(uv);
            if(s.a<.35||light>.5)discard;
            float l=step(.75,a.a)*smoothstep(.015,.10,s.g-max(s.r,s.b));
            float o=step(.32,a.a)*(1.-step(.75,a.a));
            float signal=(.58+.42*sin(time*3.+a.b*8.+seed*6.));
            float intensity=(l*a.r*.61+o*(.24+.46*a.g))*signal*(1.-light*2.);
            if(intensity<.012)discard;
            outColor=vec4(mix(vec3(.36,.95,.56),vec3(.94,.69,.30),o)*intensity,intensity);return;
        }
        discard;
    }
    // Analytic inverse deformation is anchored at the bottom of the native
    // sprite, and cannot sample past its source bounds.
    if(role==0)
    {
        float y=uv.y;
        if(mode==2){uv.x=.5+(uv.x-.5)/(1.-.32*(1.-light)*y);}
        if(mode==3){uv.y/=1.-.33*stress;uv.x=.5+(uv.x-.5)/(1.-.24*stress*sat(uv.y));}
        if(mode==11){uv.x-=.095*(phase-.5)*y*y;}
        if(mode==15){float d=bits==1?1.:bits==2?-1.:0.;uv.x-=.25*d*y*y;}
        if(mode==20){uv.x=.5+(uv.x-.5)/(1.-.29*(1.-wet)*y);uv.y/=1.-.11*(1.-wet);}
    }
    if(outside(uv))discard;
    vec4 s=texture(_MainTex,uv*uMaterial._MainTex_ST.zw+uMaterial._MainTex_ST.xy);
    if(role==0&&uMaterial._HnsLife.z>.5)
    {
        float f=mode>=21?sat(e.w):mode==2?light:mode==11?phase:sat((growth-.60)/.40);
        s=atlas(uv,floor(f*8.+.5));
    }
    if(s.a<.35)discard;
    vec4 a=anatomy(uv);
    float green=smoothstep(.0,.065,s.g-s.r)*smoothstep(.015,.085,s.g-s.b);
    float leaf=step(.75,a.a)*green;
    float organ=step(.32,a.a)*(1.-step(.75,a.a))*(1.-green);
    float vein=a.r,thin=a.g,territory=a.b;
    float tone=clamp(max(s.r,max(s.g,s.b))*1.22+.08,0.,1.);
    vec3 c=s.rgb;
    if(role==2)
    {
        if(leaf<.15||territory<.27)discard;
        outColor=vec4(s.rgb*vec3(.57,.72,.49),s.a);return;
    }
    if(mode==1)
    {
        float front=fract(day*3.+seed),wave=1.-smoothstep(.05,.24,abs(fract(uv.y+.2*territory)-front));
        c=mix(c,vec3(.87,1.,.39)*tone,leaf*light*(.22+.78*wave)*(.28+.72*vein));
    }
    else if(mode==3)c=mix(c,vec3(.68,.58,.26)*tone,leaf*stress*(.30+.50*thin));
    else if(mode==4)
    {
        float dew=max(wet,smoothstep(.18,.24,day)*(1.-smoothstep(.30,.36,day)));
        vec2 p=uv*vec2(12,12),id=floor(p),d=fract(p)-vec2(.24+.52*hash(id),.24+.52*hash(id+4.));
        float radius=length(d),bead=1.-smoothstep(.10,.22,radius);
        float spark=1.-smoothstep(.025,.10,length(d+vec2(.065,.07)));
        vec3 lens=texture(_MainTex,clamp(uv-d*.013,0.,1.)).rgb;
        c=mix(c,lens*1.16+vec3(.08,.11,.12),leaf*dew*bead*.8);
        c=mix(c,vec3(.91,.98,1.),leaf*dew*spark*.85);
    }
    else if(mode==6)
    {
        float choice=fract(territory*.83+seed*.62);
        vec3 palette=choice<.35?vec3(.92,.87,.54):choice<.64?vec3(.43,.82,.44):vec3(.62,.29,.43);
        c=mix(c,palette*tone,leaf*(.65+.17*thin));
        c=mix(c,vec3(.94,.49,.54)*tone,organ*smoothstep(.52,.78,fract(territory+seed))*.8);
    }
    else if(mode==7)
    {
        float front=smoothstep(.95-season,.99-season,uv.y+territory*.17);
        vec3 fall=mix(vec3(.97,.68,.18),vec3(.70,.27,.13),smoothstep(.35,.85,territory));
        c=mix(c,fall*tone,leaf*front*season*.94);
    }
    else if(mode==9)
    {
        float aging=sat(season*1.2);
        // Recession is intentional and limited to the lamina; stems,
        // veins, original reproductive pixels, and unknown tissue stay.
        if(leaf>.55&&vein<.22&&thin>.36&&aging>.48&&territory<aging*.78)discard;
        c=mix(c,vec3(.71,.51,.25)*tone,leaf*aging*(.35+.55*vein));
    }
    else if(mode==12)
    {
        float reveal=smoothstep(.0,.18,fract(day*2.+seed)-uv.y*.65+territory*.20);
        c=mix(c,vec3(.96,.66,.24)*tone,leaf*light*reveal*smoothstep(.19,.72,vein)*.96);
    }
    else if(mode==13)
    {
        float m=organ*light*(.27+.73*thin);
        c=mix(c,s.rgb*vec3(1.34,1.18,1.05)+vec3(.13,.10,.06),m*.80);
    }
    else if(mode==16)
    {
        float ripeness=smoothstep(territory*.52+.42,territory*.52+.68,growth);
        vec3 ripe=mix(vec3(.49,.77,.29),vec3(.98,.61,.20),ripeness);
        c=mix(c,ripe*tone,organ*.83);
        c+=organ*ripeness*thin*.09;
    }
    else if(mode==17)
    {
        float frost=leaf*stress*(.15+.85*max(thin,vein*.8));
        c=mix(c,vec3(.83,.93,.96)*tone,frost*.96);
        c=mix(c,vec3(.95,.98,1.),frost*smoothstep(.86,.97,hash(floor(uv*75.)))*.66);
    }
    else if(mode==20)
    {
        c=mix(c,s.rgb*vec3(.60,.86,.72)+vec3(.01,.05,.04),wet*leaf*.92);
        c=mix(c,vec3(.78,.96,.69)*tone,wet*leaf*thin*.30);
    }
    outColor=vec4(clamp(c,0.,1.),s.a);
}
