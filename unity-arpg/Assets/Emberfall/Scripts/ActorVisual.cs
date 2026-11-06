using TMPro;
using UnityEngine;
namespace Emberfall
{
    public class ActorVisual:MonoBehaviour
    {
        Transform skin;SpriteRenderer renderer2d;Vector3 rest,last;float hit;
        void Start(){skin=transform.Find("Body");renderer2d=skin.GetComponent<SpriteRenderer>();rest=skin.localPosition;last=transform.position;}
        public void Hit(){hit=.16f;}
        void LateUpdate()
        {
            if(!skin)return;
            bool moving=(transform.position-last).sqrMagnitude>.00001f;last=transform.position;
            skin.localPosition=rest+Vector3.up*(moving?Mathf.Abs(Mathf.Sin(Time.time*13))*.08f:Mathf.Sin(Time.time*2)*.015f);
            skin.localRotation=Quaternion.Euler(0,0,moving?Mathf.Sin(Time.time*13)*2:0);
            renderer2d.sortingOrder=100-Mathf.RoundToInt(transform.position.y*4);
            hit-=Time.deltaTime;renderer2d.color=hit>0?new Color(1,.45f,.35f):Color.white;
        }
    }
    public class DamageNumber:MonoBehaviour
    {
        float life=.8f;TMP_Text label;
        public static void Show(Transform parent,Vector2 point,float value,Color color)
        {
            var go=new GameObject("Damage");go.transform.SetParent(parent,false);go.transform.position=point+Vector2.up*.9f;
            var text=go.AddComponent<TextMeshPro>();text.font=Resources.Load<TMP_FontAsset>("Fonts/EmberfallChinese");text.fontSize=3.6f;text.alignment=TextAlignmentOptions.Center;text.color=color;text.text=Mathf.CeilToInt(value).ToString();text.GetComponent<MeshRenderer>().sortingOrder=700;
            go.AddComponent<DamageNumber>().label=text;
        }
        void Update(){life-=Time.deltaTime;transform.position+=Vector3.up*Time.deltaTime*.8f;if(label){var c=label.color;c.a=Mathf.Min(1,life*3);label.color=c;}if(life<=0)Destroy(gameObject);}
    }
}
