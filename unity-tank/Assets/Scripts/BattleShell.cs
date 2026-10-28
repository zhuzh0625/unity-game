using System;
using UnityEngine;

namespace IronBastion
{
    public sealed class BattleShell : MonoBehaviour
    {
        public TankActor Owner { get; private set; }
        public bool Friendly { get; private set; }
        Vector3 direction;
        int power;
        float speed, age;
        public void Configure(TankActor owner, Vector3 heading, int strength, float velocity)
        {
            Owner=owner; Friendly=owner.IsPlayer; direction=heading; power=strength; speed=velocity;
            var mesh=WorldBuilder.Cube("Tracer",transform,Vector3.zero,new(.12f,.12f,.43f),WorldBuilder.Material(Friendly?"Gold tracer":"Red tracer",Friendly?new Color(1,.76f,.25f):new Color(1,.28f,.13f),0,3));
            mesh.transform.rotation=Quaternion.LookRotation(direction);
            var trail=gameObject.AddComponent<TrailRenderer>(); trail.time=.07f; trail.startWidth=.09f; trail.endWidth=0;
            trail.sharedMaterial=mesh.GetComponent<Renderer>().sharedMaterial;
        }
        void Update()
        {
            if(BattleGame.Instance.State!=BattleState.Playing) return;
            Physics.SyncTransforms();
            float distance=speed*Time.deltaTime;
            var hits=Physics.SphereCastAll(transform.position,.09f,direction,distance,WorldBuilder.ShotMask,QueryTriggerInteraction.Ignore);
            Array.Sort(hits,(a,b)=>a.distance.CompareTo(b.distance));
            foreach(var hit in hits)
            {
                var tank=hit.collider.GetComponent<TankActor>();
                if(tank==Owner || (tank && tank.IsPlayer==Friendly)) continue;
                if(tank) tank.Hit();
                else if(hit.collider.TryGetComponent<BattleBlock>(out var block)) block.Hit(power);
                BattleEffects.Spark(hit.point,false); Destroy(gameObject); return;
            }
            // Opposing shells cancel each other, as in the arcade game.
            foreach(var other in FindObjectsByType<BattleShell>(FindObjectsSortMode.None))
            {
                if(other==this||other.Friendly==Friendly) continue;
                var closest=transform.position+direction*Mathf.Clamp(Vector3.Dot(other.transform.position-transform.position,direction),0,distance);
                if(Vector3.Distance(closest,other.transform.position)<.3f) { BattleEffects.Spark(closest,false); Destroy(other.gameObject);Destroy(gameObject);return; }
            }
            transform.position+=direction*distance; age+=Time.deltaTime;
            if(age>3||Mathf.Abs(transform.position.x)>15||Mathf.Abs(transform.position.z)>15) Destroy(gameObject);
        }
        void OnDestroy() { if(Owner) Owner.ActiveShells=Mathf.Max(0,Owner.ActiveShells-1); }
    }
}
