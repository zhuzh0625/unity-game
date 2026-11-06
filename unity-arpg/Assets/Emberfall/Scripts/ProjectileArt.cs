using UnityEngine;

namespace Emberfall
{
    public enum ProjectileKind { Ember, Arrow, Hex, Warden }

    // Small, cached effect textures follow the existing procedural effects palette.
    public static class ProjectileArt
    {
        static readonly Sprite[,] frames = new Sprite[4, 4];
        public static Color Tint(ProjectileKind kind) => kind == ProjectileKind.Hex ? new Color(.68f,.34f,1) :
            kind == ProjectileKind.Arrow ? new Color(.85f,.75f,.48f) : kind == ProjectileKind.Warden ? new Color(1,.23f,.06f) : new Color(1,.61f,.12f);

        public static Sprite Frame(ProjectileKind kind, int frame)
        {
            frame &= 3;
            if (frames[(int)kind,frame]) return frames[(int)kind,frame];
            const int width = 128;
            int height = kind == ProjectileKind.Hex ? 128 : 64;
            var texture = new Texture2D(width,height,TextureFormat.RGBA32,false) { name = kind + " projectile " + frame, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color[width*height];
            float phase = frame*Mathf.PI*.5f;
            for (int y=0;y<height;y++) for (int x=0;x<width;x++)
            {
                float u=(x+.5f)/width, v=((y+.5f)/height-.5f)*2;
                Color color=Color.clear;
                if (kind==ProjectileKind.Arrow)
                {
                    if(u>.12f&&u<.82f&&Mathf.Abs(v)<.045f) color=v>0?new Color(.76f,.57f,.3f):new Color(.3f,.17f,.1f);
                    if(u>.73f&&u<.97f&&Mathf.Abs(v)<(.97f-u)*1.2f) color=v>0?new Color(.95f,.96f,.88f):new Color(.45f,.55f,.62f);
                    if(u>.1f&&u<.34f&&Mathf.Abs(v)>.04f&&Mathf.Abs(v)<(.36f-u)*.8f)
                        color=(x+y)%9<3?new Color(.43f,.35f,.24f):new Color(.87f,.82f,.62f);
                    if(u>.68f&&u<.76f&&Mathf.Abs(v)<.065f)color=new Color(.56f,.62f,.64f);
                }
                else if(kind==ProjectileKind.Hex)
                {
                    float px=u*2-1, r=Mathf.Sqrt(px*px+v*v), a=Mathf.Atan2(v,px);
                    float ring=Mathf.Clamp01(1-Mathf.Abs(r-(.55f+Mathf.Sin(a*5-phase)*.05f))*32);
                    float core=Mathf.Pow(Mathf.Clamp01(1-r/.48f),.6f);
                    float spiral=Mathf.Clamp01(1-Mathf.Abs(Mathf.Sin(a*3+r*9-phase)) * 8)*Mathf.Clamp01((.62f-r)*6);
                    float alpha=Mathf.Max(core,Mathf.Max(ring*.85f,spiral*.9f));
                    color=Color.Lerp(new Color(.35f,.08f,.65f),new Color(.92f,.76f,1),Mathf.Max(core,spiral*.8f));color.a=alpha;
                }
                else
                {
                    float center=.71f, wave=Mathf.Sin(u*15-phase)*.12f*(1-u);
                    float r=Mathf.Sqrt(Mathf.Pow((u-center)/.21f,2)+Mathf.Pow(v/.54f,2));
                    float core=Mathf.Clamp01((1-r)*5);
                    float widthTail=(.12f+u*.34f)*(1+Mathf.Sin(u*27+phase)*.16f);
                    float tail=Mathf.Clamp01((1-Mathf.Abs(v-wave)/widthTail)*4)*Mathf.Clamp01(u*3)*Mathf.Clamp01((.8f-u)*9);
                    if(kind==ProjectileKind.Warden)
                    {
                        float edge=1-Mathf.Abs((u-.7f)/.26f);
                        core=Mathf.Clamp01((edge-Mathf.Abs(v)*2.2f)*18);
                    }
                    float heat=Mathf.Max(Mathf.Clamp01(1-r)*1.4f,tail*u*.7f);
                    color=Color.Lerp(kind==ProjectileKind.Warden?new Color(.8f,.09f,.01f):new Color(1,.28f,.015f),new Color(1,.96f,.63f),heat);
                    color.a=Mathf.Max(core,tail*.86f)*Mathf.Clamp01((1-u)*35);
                }
                pixels[y*width+x]=color;
            }
            texture.SetPixels(pixels);texture.Apply(false,true);
            var pivot=kind==ProjectileKind.Hex?Vector2.one*.5f:new Vector2(kind==ProjectileKind.Arrow?.82f:.71f,.5f);
            return frames[(int)kind,frame]=Sprite.Create(texture,new Rect(0,0,width,height),pivot,width,0,SpriteMeshType.FullRect);
        }
    }
}
