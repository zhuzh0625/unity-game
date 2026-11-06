using UnityEngine;

namespace Emberfall
{
    // Presentation has its own pooled hierarchy; the missile root remains the collision point.
    public sealed class ProjectileVisual : MonoBehaviour
    {
        Transform picture;
        SpriteRenderer body,halo;
        readonly SpriteRenderer[] trail=new SpriteRenderer[4];
        float age;
        Vector2 heading;
        public ProjectileKind Kind { get; private set; }
        public SpriteRenderer Body => body;
        public Vector2 Heading => heading;
        public float Age => age;
        public static readonly Vector2 HeightOffset=new Vector2(0,.32f);

        public void Initialize()
        {
            picture=new GameObject("Projectile art").transform;picture.SetParent(transform,false);picture.localPosition=HeightOffset;
            halo=Layer("Glow",399);body=Layer("Core",402);
            for(int i=0;i<trail.Length;i++)trail[i]=Layer("Trail "+i,400);
        }
        SpriteRenderer Layer(string label,int order)
        {
            var go=new GameObject(label);go.transform.SetParent(picture,false);
            var sr=go.AddComponent<SpriteRenderer>();sr.sortingOrder=order;return sr;
        }
        public void Begin(ProjectileKind kind,Vector2 position,Vector2 velocity)
        {
            Kind=kind;age=0;heading=velocity.normalized;
            transform.SetPositionAndRotation(position,Quaternion.identity);
            picture.localRotation=Quaternion.Euler(0,0,Mathf.Atan2(heading.y,heading.x)*Mathf.Rad2Deg);
            halo.sprite=kind==ProjectileKind.Hex?WorldArt.Ring:WorldArt.Glow;
            gameObject.name="Projectile - "+kind;
            gameObject.SetActive(true);Step(0);
        }
        public void Step(float dt)
        {
            age+=dt;bool arrow=Kind==ProjectileKind.Arrow;
            Color tint=ProjectileArt.Tint(Kind);
            body.sprite=ProjectileArt.Frame(Kind,(int)(age*18));body.color=Color.white;
            float size=arrow?1.65f:Kind==ProjectileKind.Hex?.9f:Kind==ProjectileKind.Warden?1.38f:1.6f;
            float flutter=arrow?1:1+Mathf.Sin(age*26)*.045f;
            body.transform.localScale=Vector3.one*size*flutter;
            halo.enabled=!arrow;halo.color=new Color(tint.r,tint.g,tint.b,Kind==ProjectileKind.Hex?.4f:.52f);
            halo.transform.localScale=Vector3.one*(Kind==ProjectileKind.Hex?.74f:1.25f)*flutter;
            halo.transform.localRotation=Quaternion.Euler(0,0,age*100);
            for(int i=0;i<trail.Length;i++)
            {
                var sr=trail[i];sr.enabled=!arrow||i==0;
                float reveal=Mathf.Clamp01(age*14-i*.5f);
                sr.sprite=arrow?WorldArt.Square:WorldArt.Glow;
                sr.color=new Color(tint.r,tint.g,tint.b,(arrow?.24f:.65f)*(1-i*.2f)*reveal);
                sr.transform.localPosition=new Vector3(-.28f-i*.2f,arrow?0:Mathf.Sin(age*17-i)*.08f,0);
                sr.transform.localScale=arrow?new Vector3(.8f,.025f,1):new Vector3(.44f-i*.06f,.3f-i*.035f,1);
            }
        }
    }
}
