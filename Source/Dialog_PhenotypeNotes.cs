using UnityEngine;
using Verse;

namespace HorticultureNovelSeeds
{
    internal sealed class Dialog_PhenotypeNotes : Window
    {
        private readonly CompPlantVariety specimen;
        private Vector2 scroll;
        internal Dialog_PhenotypeNotes(CompPlantVariety specimen)
        {
            this.specimen=specimen;
            doCloseX=true;doCloseButton=true;closeOnClickedOutside=true;absorbInputAroundWindow=true;
        }
        public override Vector2 InitialSize=>new(650f,510f);
        public override void DoWindowContents(Rect rect)
        {
            var oldFont=Text.Font;
            try
            {
                Text.Font=GameFont.Medium;
                Widgets.Label(new Rect(rect.x,rect.y,rect.width,36f),"Botanical field notes");
                Text.Font=GameFont.Small;
                Widgets.Label(new Rect(rect.x,rect.y+40f,rect.width,32f),specimen?.parent?.LabelCap.ToString()??"Plant specimen");
                string notes=PhenotypeObservation.Notes(specimen);
                Rect outer=new(rect.x,rect.y+80f,rect.width,Mathf.Max(1f,rect.height-126f));
                float width=Mathf.Max(1f,outer.width-20f);
                Rect view=new(0,0,width,Mathf.Max(outer.height,Text.CalcHeight(notes,width)+12f));
                Widgets.BeginScrollView(outer,ref scroll,view);
                try{Widgets.Label(view,notes);}finally{Widgets.EndScrollView();}
            }
            finally{Text.Font=oldFont;}
        }
    }
}
