using System.Collections.Generic;
using UnityEngine;

namespace IronBastion
{
    public static class BattleEffects
    {
        static AudioSource audio;
        static readonly Dictionary<string,AudioClip> clips=new();
        public static void Initialize()
        {
            if(audio)return;
            var go=new GameObject("Arcade audio");audio=go.AddComponent<AudioSource>();audio.spatialBlend=0;
        }
        public static void Tone(float hz,float duration,float volume=.22f)
        {
            if(!audio)return;
            string key=hz+"/"+duration;
            if(!clips.TryGetValue(key,out var clip)||!clip)
            {
                int count=Mathf.CeilToInt(22050*duration);var data=new float[count];
                for(int i=0;i<count;i++) {float t=(float)i/count;data[i]=Mathf.Sin(i/22050f*hz*(1-t*.4f)*Mathf.PI*2)*(1-t)*.7f;}
                clip=AudioClip.Create("Synth "+key,count,1,22050,false);clip.SetData(data,0);clips[key]=clip;
            }
            audio.PlayOneShot(clip,volume);
        }
        public static void Spark(Vector3 p,bool muzzle) => Emit(p,muzzle?4:7,muzzle?.32f:.46f,new Color(1,.68f,.2f),muzzle?1.4f:2.6f,.08f);
        public static void Dust(Vector3 p) => Emit(p+Vector3.up*.1f,1,.55f,new Color(.5f,.49f,.39f),.4f,.10f);
        public static void Explode(Vector3 p,float size)
        {
            Emit(p,18,.7f,new Color(.96f,.39f,.13f),size*3,.12f*size);
            Emit(p,9,1.15f,new Color(.16f,.20f,.20f),size*2,.23f*size);
            Tone(65,.25f,.45f);
        }
        static void Emit(Vector3 p,int count,float life,Color color,float velocity,float size)
        {
            if(!Application.isPlaying||!BattleGame.Instance||!BattleGame.Instance.DynamicRoot)return;
            var go=new GameObject("Burst");go.transform.SetParent(BattleGame.Instance.DynamicRoot);go.transform.position=p;
            var ps=go.AddComponent<ParticleSystem>();ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main=ps.main; main.duration=life;main.loop=false;main.startLifetime=life;main.startSpeed=velocity;main.startSize=size;main.startColor=color;main.gravityModifier=.7f;main.simulationSpace=ParticleSystemSimulationSpace.World;main.maxParticles=32;main.stopAction=ParticleSystemStopAction.Destroy;
            var emission=ps.emission;emission.rateOverTime=0;emission.SetBursts(new[]{new ParticleSystem.Burst(0,(short)count)});
            var shape=ps.shape;shape.shapeType=ParticleSystemShapeType.Sphere;shape.radius=.09f;
            var scale=ps.sizeOverLifetime;scale.enabled=true;scale.size=new ParticleSystem.MinMaxCurve(1,AnimationCurve.Linear(0,1,1,0));
            var render=ps.GetComponent<ParticleSystemRenderer>();render.sharedMaterial=WorldBuilder.Material("Particle "+color,color,0,.3f);
            ps.Play();
        }
    }
}
