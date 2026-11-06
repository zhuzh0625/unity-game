using UnityEngine;
namespace Emberfall
{
    public class ActorVisual:MonoBehaviour
    {
        Transform skin;SpriteRenderer renderer2d,charge;Vector3 rest,last;
        Vector2 direction=Vector2.right;
        float hit,clock,strike,strikeDuration,dodge,death,phaseTime,windupDuration;
        bool hero,dying,enraged;
        int kind=-1;
        public int AttackFrame { get; private set; }
        public static float RecoveryDuration(int enemyKind) => enemyKind==3?.42f:enemyKind==2?.34f:enemyKind==1?.3f:.28f;
        void Awake()
        {
            skin=transform.Find("Body");renderer2d=skin.GetComponent<SpriteRenderer>();rest=skin.localPosition;last=transform.position;hero=name=="Wanderer";
            if(!hero)
            {
                kind=name=="Ravager"?0:name=="Bone Archer"?1:name=="Mire Hexer"?2:3;
                if(kind>=2)
                {
                    var go=new GameObject("Attack charge");go.transform.SetParent(transform,false);
                    charge=go.AddComponent<SpriteRenderer>();charge.sprite=WorldArt.Glow;charge.enabled=false;
                }
            }
        }
        public void Face(Vector2 value){if(value.sqrMagnitude>.01f)direction=value.normalized;}
        public void Strike(Vector2 aim,float duration)
        {
            Face(aim);if(hero){strike=strikeDuration=duration;return;}
            AttackFrame=1;phaseTime=0;windupDuration=duration;
        }
        // Combat triggers this at the actual damage / projectile release event.
        public void ReleaseAttack(){if(dying||hero)return;AttackFrame=2;phaseTime=0;}
        public void Dodge(Vector2 aim){Face(aim);dodge=.2f;strike=0;}
        public void Enrage(){enraged=true;}
        public void Die(){dying=true;death=.28f;AttackFrame=0;if(charge)charge.enabled=false;}
        public void Hit(){hit=.16f;}
        void LateUpdate()
        {
            if(!skin)return;
            var game=Adventure.Instance;if(game&&game.SimulationPaused&&!dying)return;
            float dt=Mathf.Min(Time.deltaTime,.05f);clock+=dt;
            bool moving=(transform.position-last).sqrMagnitude>.00001f;last=transform.position;
            strike=Mathf.Max(0,strike-dt);dodge=Mathf.Max(0,dodge-dt);hit=Mathf.Max(0,hit-dt);
            float swing=strike>0?Mathf.Sin((1-strike/strikeDuration)*Mathf.PI):0;
            renderer2d.sortingOrder=100-Mathf.RoundToInt(transform.position.y*4);
            Color color=hit>0?new Color(1,.48f,.34f):enraged?new Color(1,.73f,.57f):Color.white;
            if(dying){death-=dt;color.a=Mathf.Clamp01(death/.28f);}else if(dodge>0)color.a=.55f;
            renderer2d.color=color;
            if(hero)
            {
                skin.localPosition=rest+Vector3.up*(moving?Mathf.Abs(Mathf.Sin(clock*13))*.045f:Mathf.Sin(clock*2)*.012f)+(Vector3)(direction*swing*.12f);
                skin.localRotation=Quaternion.Euler(0,0,dying?65:dodge>0?-direction.x*18:0);
                int row=Mathf.Abs(direction.x)>Mathf.Abs(direction.y)?(direction.x>0?1:3):direction.y>0?2:0;
                int step=moving?(int)(clock*9)%4:0;
                int frame=strike>0&&strike<.15f?3:step==1?1:step==3?2:0;
                renderer2d.sprite=WorldArt.HeroFrame(row,frame);
            }
            else
            {
                phaseTime+=dt;
                float recovery=RecoveryDuration(kind),release=recovery*.5f;
                if(AttackFrame==2&&phaseTime>=release)AttackFrame=3;
                if(AttackFrame==3&&phaseTime>=recovery){AttackFrame=0;phaseTime=0;}
                float anticipation=AttackFrame==1?Mathf.Clamp01(phaseTime/Mathf.Max(.01f,windupDuration)):0;
                float follow=AttackFrame>=2?Mathf.Max(0,1-phaseTime/recovery):0;
                float bob=AttackFrame==0?(moving?Mathf.Abs(Mathf.Sin(clock*12))*.035f:Mathf.Sin(clock*2)*.007f):0;
                skin.localPosition=rest+Vector3.up*bob+(Vector3)(direction*(-anticipation*.055f+follow*(kind==0?.18f:.09f)));
                skin.localRotation=Quaternion.Euler(0,0,dying?65:0);
                renderer2d.sprite=WorldArt.EnemyFrame(kind,AttackFrame);renderer2d.flipX=direction.x<0;
                if(charge)
                {
                    charge.enabled=!dying&&AttackFrame==1;charge.sortingOrder=renderer2d.sortingOrder+1;
                    float scale=kind==3?1.7f:1;
                    charge.transform.localPosition=new Vector2((direction.x<0?-.3f:.3f)*scale,.65f*scale);
                    charge.transform.localScale=Vector3.one*(.35f+anticipation*.45f)*scale;
                    var tint=kind==3?new Color(1,.25f,.06f):new Color(.65f,.25f,1);tint.a=.3f+anticipation*.4f;charge.color=tint;
                }
            }
        }
    }
}
