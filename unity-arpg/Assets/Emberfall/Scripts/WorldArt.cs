using UnityEngine;
namespace Emberfall
{
    public static class WorldArt
    {
        static Sprite square,disc,ring,glow,soilPatch,slash;static Texture2D atlas,heroAtlas,enemyAtlas;
        static readonly Sprite[] heroFrames=new Sprite[16],icons=new Sprite[7];
        static readonly Sprite[] enemyFrames=new Sprite[16];
        static readonly float[] enemyFeet={.09f,.155f,.195f,.255f},enemyPixels={.72f,.72f,.75f,.69f};
        static readonly Sprite[] sprites=new Sprite[16],tiles=new Sprite[4];
        public static Sprite Square=>square?square:square=Make(0);
        public static Sprite Disc=>disc?disc:disc=Make(1);
        public static Sprite Ring=>ring?ring:ring=Make(2);
        public static Sprite Glow=>glow?glow:glow=Make(3);
        public static Sprite Slash=>slash?slash:slash=Make(4);
        static Sprite Make(int type)
        {
            const int n=96;var tex=new Texture2D(n,n){filterMode=FilterMode.Bilinear};var px=new Color[n*n];
            for(int y=0;y<n;y++)for(int x=0;x<n;x++)
            {
                float d=Vector2.Distance(new Vector2(x+.5f,y+.5f),Vector2.one*n*.5f)/(n*.5f);
                float a=type==0?1:type==1?Mathf.Clamp01((1-d)*12):type==2?Mathf.Clamp01((1-d)*30)*Mathf.Clamp01((d-.83f)*25):Mathf.Pow(Mathf.Clamp01(1-d),2);
                if(type==4)a=Mathf.Clamp01((1-d)*18)*Mathf.Clamp01((d-.68f)*16)*Mathf.Clamp01((1.45f-Mathf.Abs(Mathf.Atan2(y-n*.5f,x-n*.5f)))*5);
                px[y*n+x]=new Color(1,1,1,a);
            }
            tex.SetPixels(px);tex.Apply();return Sprite.Create(tex,new Rect(0,0,n,n),Vector2.one*.5f,n);
        }
        public static Sprite Asset(int index)
        {
            if(sprites[index])return sprites[index];
            if(!atlas)atlas=Resources.Load<Texture2D>("Art/EmberfallAtlas");
            if(!atlas)throw new System.InvalidOperationException("Emberfall art atlas is missing.");
            float w=atlas.width/4f,h=atlas.height/4f;
            return sprites[index]=Sprite.Create(atlas,new Rect(index%4*w,(3-index/4)*h,w,h),Vector2.one*.5f,w);
        }
        public static Sprite HeroFrame(int row,int frame)
        {
            int index=row*4+frame;if(heroFrames[index])return heroFrames[index];
            if(!heroAtlas)heroAtlas=Resources.Load<Texture2D>("Art/WandererDirections");
            if(!heroAtlas)return Asset(0);
            float w=heroAtlas.width/4f,h=heroAtlas.height/4f;
            // The westward sword extends left into the transparent gutter; preserve its tip.
            float extra=row==3&&frame==3?w*.13f:0;
            return heroFrames[index]=Sprite.Create(heroAtlas,new Rect(frame*w-extra,(3-row)*h,w+extra,h),new Vector2((w*.5f+extra)/(w+extra),.5f),w);
        }
        public static Sprite GearIcon(int slot)=>Icon(slot==0?0:5);
        public static Sprite EnemyFrame(int kind,int frame)
        {
            int index=kind*4+frame;if(enemyFrames[index])return enemyFrames[index];
            if(!enemyAtlas)enemyAtlas=Resources.Load<Texture2D>("Art/EnemyAttacks");
            if(!enemyAtlas)throw new System.InvalidOperationException("Enemy attack atlas is missing.");
            float w=enemyAtlas.width/4f,h=enemyAtlas.height/4f;
            // Row-specific foot anchors keep all four poses planted at the same world position.
            var sprite=Sprite.Create(enemyAtlas,new Rect(frame*w,(3-kind)*h,w,h),new Vector2(.5f,enemyFeet[kind]),w*enemyPixels[kind]);
            sprite.name="Enemy "+kind+" attack frame "+frame;return enemyFrames[index]=sprite;
        }
        public static Sprite Icon(int index)
        {
            if(icons[index])return icons[index];const int n=64;
            var texture=new Texture2D(n,n){filterMode=FilterMode.Bilinear};var pixels=new Color[n*n];
            for(int y=0;y<n;y++)for(int x=0;x<n;x++)
            {
                float px=(x+ .5f)/n*2-1,py=(y+.5f)/n*2-1;bool on=false;
                if(index==0||index==1){on=(Mathf.Abs(px)<.09f&&py>-.55f&&py<.8f)||(Mathf.Abs(py+.35f)<.06f&&Mathf.Abs(px)<.35f);if(index==1)on|=Mathf.Abs(Mathf.Sqrt(px*px+py*py)-.68f)<.07f&&px>.1f;}
                else if(index==2){for(int a=0;a<3;a++){float angle=a*Mathf.PI/3;on|=Mathf.Abs(px*Mathf.Cos(angle)-py*Mathf.Sin(angle))<.045f&&px*px+py*py<.65f;}}
                else if(index==3)on=(px*px/.22f+(py+.2f)*(py+.2f)/.42f<1)||(py>.1f&&py<.85f&&Mathf.Abs(px-py*.18f)<(.85f-py)*.4f);
                else if(index==4)on=Mathf.Abs(Mathf.Abs(py)-(.55f-px*.65f))<.1f&&px>-.55f&&px<.65f;
                else if(index==5)on=(Mathf.Abs(px)<.42f&&py>-.65f&&py<.4f)||(Mathf.Abs(px)<.72f&&Mathf.Abs(px)>.3f&&py>.05f&&py<.55f);
                else on=(px*px/.3f+(py+.15f)*(py+.15f)/.4f<1)||(Mathf.Abs(px)<.2f&&py>.1f&&py<.7f);
                pixels[y*n+x]=on?Color.white:Color.clear;
            }
            texture.SetPixels(pixels);texture.Apply();return icons[index]=Sprite.Create(texture,new Rect(0,0,n,n),Vector2.one*.5f,n);
        }
        static Sprite Tile(int type)
        {
            if(tiles[type])return tiles[type];
            const int n=128;var tex=new Texture2D(n,n){filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Repeat};var px=new Color[n*n];
            for(int y=0;y<n;y++)for(int x=0;x<n;x++)
            {
                uint hash=(uint)(x*374761393+y*668265263+type*9127);hash=(hash^(hash>>13))*1274126177;
                float v=.72f+(hash%1000)/1000f*.28f;
                if(type==2||type==3)
                {
                    int row=y/32,xx=(x+(row%2)*32)%64;
                    if(xx<2||y%32<2)v*=.42f;else if(xx<4||y%32<4)v*=1.15f;
                    if(hash%137==0)v*=.35f;
                }
                else if(type==0&&hash%43<3)v*=1.15f;
                else if(type==1&&hash%31<2)v*=.55f;
                px[y*n+x]=new Color(v,v,v,1);
            }
            tex.SetPixels(px);tex.Apply();return tiles[type]=Sprite.Create(tex,new Rect(0,0,n,n),Vector2.one*.5f,n,0,SpriteMeshType.FullRect);
        }
        public static GameObject Shape(string name,Transform parent,Vector2 pos,Vector2 size,Color color,int order=0,bool circle=false)
        {
            var go=new GameObject(name);go.transform.SetParent(parent,false);go.transform.localPosition=pos;go.transform.localScale=size;
            var sr=go.AddComponent<SpriteRenderer>();sr.sprite=circle?Disc:Square;sr.color=color;sr.sortingOrder=order;
            if(name=="Ground"||name=="Camp path"||name=="Wall face")sr.sprite=Tile(name=="Wall face"?3:name=="Camp path"?1:0);
            if(name=="Wall face"){go.transform.localScale=Vector3.one;sr.drawMode=SpriteDrawMode.Tiled;sr.size=size;}
            if(name=="Impact"||name=="Warden warning")sr.sprite=Ring;
            if(name=="Glow")sr.sprite=Glow;
            return go;
        }
        public static GameObject Prop(int index,string name,Transform parent,Vector2 pos,Vector2 size,int order=3)
        {
            var go=Shape(name,parent,pos,size,Color.white,order);go.GetComponent<SpriteRenderer>().sprite=Asset(index);
            if(index>=5&&index!=13)Shape("Glow",go.transform,new Vector2(0,-.40f),new Vector2(.8f,.22f),new Color(0,0,0,.48f),order-1,true);
            return go;
        }
        static Sprite SoilPatch()
        {
            if(soilPatch)return soilPatch;
            const int n=128;var tex=new Texture2D(n,n){filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};var px=new Color[n*n];
            for(int y=0;y<n;y++)for(int x=0;x<n;x++)
            {
                float dx=(x+.5f)/n*2-1,dy=(y+.5f)/n*2-1;
                float noise=Mathf.PerlinNoise(x*.08f+3,y*.08f+7);
                float alpha=Mathf.Clamp01((1-Mathf.Sqrt(dx*dx+dy*dy)+(noise-.5f)*.25f)*3.5f);
                float grain=.76f+Mathf.PerlinNoise(x*.6f,y*.6f)*.24f;px[y*n+x]=new Color(grain,grain,grain,alpha);
            }
            tex.SetPixels(px);tex.Apply();return soilPatch=Sprite.Create(tex,new Rect(0,0,n,n),Vector2.one*.5f,n);
        }
        static void Patch(Transform parent,Vector2 pos,Vector2 size,Color color,int order,float angle=0)
        {
            var go=Shape("Terrain patch",parent,pos,size,color,order);go.GetComponent<SpriteRenderer>().sprite=SoilPatch();go.transform.rotation=Quaternion.Euler(0,0,angle);
        }
        static void Trail(Transform parent,Vector2 from,Vector2 to,float width)
        {
            Vector2 delta=to-from;int steps=Mathf.CeilToInt(delta.magnitude/.75f);
            for(int i=0;i<=steps;i++)
            {
                float t=i/(float)Mathf.Max(1,steps);Vector2 p=Vector2.Lerp(from,to,t);
                p+=new Vector2(-delta.normalized.y,delta.normalized.x)*Mathf.Sin(i*.77f)*.10f;
                Patch(parent,p,new Vector2(2.1f,width*(.95f+Mathf.Sin(i*1.8f)*.08f)),new Color(.37f,.32f,.24f,.48f),-24,Mathf.Atan2(delta.y,delta.x)*Mathf.Rad2Deg);
            }
        }
        public static void Landscape(Transform parent,int zone)
        {
            if(zone==2)return;
            for(int i=0;i<42;i++)
            {
                float x=(i*73%419)/10f-20.9f,y=(i*47%211)/10f-10.5f;
                var color=i%3==0?new Color(.36f,.31f,.21f,.28f):new Color(.12f,.20f,.11f,.26f);
                Patch(parent,new Vector2(x,y),new Vector2(4+i%4,2.7f+i%3),color,-29,i*37);
            }
            Trail(parent,new Vector2(zone==0?-11:-20,0),new Vector2(20,0),2.7f);
            if(zone==0)
            {
                Patch(parent,new Vector2(-4,0),new Vector2(7,5.2f),new Color(.32f,.27f,.20f,.48f),-25);
                Trail(parent,new Vector2(-4,0),new Vector2(-5.3f,4),1.5f);
                Trail(parent,new Vector2(-3,0),new Vector2(4,4.8f),1.4f);
                Trail(parent,new Vector2(-4,0),new Vector2(-8,1.7f),1.2f);
            }
        }
        public static void Ground(Transform parent,Vector2 pos,int zone)
        {
            Color color=zone==2?new Color(.29f,.30f,.32f):new Color(.27f,.31f,.22f);
            var go=Shape("Ground",parent,pos,Vector2.one,color,-30);var sr=go.GetComponent<SpriteRenderer>();sr.sprite=Tile(zone==2?2:0);sr.drawMode=SpriteDrawMode.Tiled;sr.size=new Vector2(44,24);
        }
        public static void Decorate(Transform parent,int zone)
        {
            int count=zone==2?50:150;
            for(int i=0;i<count;i++)
            {
                Vector2 anchor=new[]{new Vector2(-14,7),new Vector2(-12,-6),new Vector2(11,-6),new Vector2(14,7),new Vector2(0,-8),new Vector2(18,-3)}[i%6];
                float angle=i*2.399963f,radius=1+((i*31)%57)/10f;
                float x=zone==2?((i*71)%419)/10f-20.9f:anchor.x+Mathf.Cos(angle)*radius;
                float y=zone==2?((i*47)%211)/10f-10.5f:anchor.y+Mathf.Sin(angle)*radius*.55f;
                if(Mathf.Abs(x)>20.5f||Mathf.Abs(y)>10.7f||Mathf.Abs(y)<1.8f)continue;
                if(zone==0&&((x>-9&&x<-2&&y>3)||(x>1&&x<8&&y>4)))continue;
                int asset=zone==2?(i%4==0?10:14):(i%5==0?6:13);float size=zone==2?1.2f:.6f+(i%5)*.12f;
                Prop(asset,"Scenery",parent,new Vector2(x,y),Vector2.one*size,zone==2?1:-10);
            }
            if(zone==0)
            {
                Prop(11,"Camp chest",parent,new Vector2(-8,1.7f),Vector2.one*1.6f,4);
                Prop(15,"Trail sign",parent,new Vector2(13,2.3f),Vector2.one*2,4);
                foreach(var p in new[]{new Vector2(-18,7),new Vector2(-14,9),new Vector2(14,9),new Vector2(18,6),new Vector2(-19,-6),new Vector2(17,-7)})
                    Prop(5,"Camp perimeter",parent,p,Vector2.one*4.3f,2);
                WorldArt.Shape("Glow",parent,new Vector2(-5,4),Vector2.one*3,new Color(1,.55f,.20f,.12f),-12,true);
                WorldArt.Shape("Glow",parent,new Vector2(4,4.5f),Vector2.one*3,new Color(1,.55f,.20f,.12f),-12,true);
            }
        }
        public static GameObject Actor(string name,Transform parent,Vector2 pos,Color color,float scale=1)
        {
            var root=new GameObject(name);root.transform.SetParent(parent,false);root.transform.localPosition=pos;
            Shape("Shadow",root.transform,new Vector2(0,-.38f),new Vector2(1.1f,.32f)*scale,new Color(0,0,0,.55f),8,true);
            int index=name=="Wanderer"?0:name=="Ravager"?1:name=="Bone Archer"?2:name=="Mire Hexer"?3:4;
            var body=Prop(index,"Body",root.transform,new Vector2(0,index==0?.35f:-.43f)*scale,Vector2.one*1.75f*scale,10);
            if(index>0)body.GetComponent<SpriteRenderer>().sprite=EnemyFrame(index-1,0);
            root.AddComponent<ActorVisual>();return root;
        }
    }
}
