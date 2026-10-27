using UnityEngine;

namespace IronBastion
{
    public sealed class BattlePickup : MonoBehaviour
    {
        public PickupKind Kind { get; private set; }
        Transform graphic;
        float born;
        public void Configure(PickupKind kind)
        {
            Kind=kind;born=Time.time;
            Color[] colors={new(1,.75f,.2f),new(.2f,.9f,.8f),new(1,.3f,.2f),new(.35f,.65f,1),new(.6f,1,.4f)};
            graphic=WorldBuilder.Cube("Supply crate",transform,Vector3.up*.6f,Vector3.one*.6f,WorldBuilder.Material("Pickup "+kind,colors[(int)kind],.3f,1)).transform;
            WorldBuilder.Cube("Cross vertical",graphic,new(0,.51f,0),new(.14f,.035f,.65f),WorldBuilder.Material("Pickup marking",Color.white));
            WorldBuilder.Cube("Cross horizontal",graphic,new(0,.51f,0),new(.65f,.035f,.14f),WorldBuilder.Material("Pickup marking",Color.white));
        }
        void Update()
        {
            var g=BattleGame.Instance;if(g.State!=BattleState.Playing)return;
            graphic.localPosition=Vector3.up*(.65f+Mathf.Sin(Time.time*3)*.12f); graphic.Rotate(Vector3.up,60*Time.deltaTime);
            if(g.Player&&Vector3.Distance(g.Player.transform.position,transform.position)<1.1f) {g.ApplyPickup(Kind);Destroy(gameObject);}
            else if(Time.time-born>18) Destroy(gameObject);
        }
    }
}
