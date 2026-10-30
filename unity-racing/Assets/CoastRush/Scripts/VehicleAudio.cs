using UnityEngine;
namespace CoastRush
{
    [RequireComponent(typeof(VehicleController))]
    public sealed class VehicleAudio : MonoBehaviour
    {
        VehicleController car;
        AudioSource engine,tires;
        static AudioClip engineClip,skidClip;
        void Awake()
        {
            car=GetComponent<VehicleController>();
            if(!engineClip)
            {
                const int n=44100;var wave=new float[n];var noise=new float[n];
                var random=new System.Random(64);float filtered=0;
                for(int i=0;i<n;i++)
                {
                    float t=i/(float)n;
                    wave[i]=(Mathf.Sin(t*80*Mathf.PI*2)*.35f+Mathf.Sin(t*160*Mathf.PI*2)*.2f+Mathf.Sin(t*240*Mathf.PI*2)*.1f)*(1+.12f*Mathf.Sin(t*20*Mathf.PI*2));
                    filtered=Mathf.Lerp(filtered,(float)random.NextDouble()*2-1,.3f);noise[i]=filtered*.5f;
                }
                engineClip=AudioClip.Create("Original synthesized GT engine",n,1,n,false);engineClip.SetData(wave,0);
                skidClip=AudioClip.Create("Original synthesized tire friction",n,1,n,false);skidClip.SetData(noise,0);
            }
            engine=gameObject.AddComponent<AudioSource>();engine.clip=engineClip;engine.loop=true;
            engine.spatialBlend=car.isPlayer?0:1;engine.minDistance=4;engine.maxDistance=55;engine.dopplerLevel=.25f;engine.volume=0;engine.Play();
            tires=gameObject.AddComponent<AudioSource>();tires.clip=skidClip;tires.loop=true;tires.spatialBlend=engine.spatialBlend;tires.minDistance=3;tires.maxDistance=30;tires.volume=0;tires.Play();
        }
        void Update()
        {
            if(!RaceDirector.Instance)return;
            bool active=RaceDirector.Instance.State==RaceState.Racing||RaceDirector.Instance.State==RaceState.Countdown;
            int gear=Mathf.Clamp(1+(int)(car.SpeedKph/42),1,5);
            float rpm=.65f+(car.SpeedKph-(gear-1)*42)/42;
            engine.pitch=Mathf.Lerp(engine.pitch,rpm+Mathf.Abs(car.throttle)*.4f,Time.deltaTime*4);
            engine.volume=active?(car.isPlayer?.12f:.07f)+Mathf.Abs(car.throttle)*.055f:0;
            float slip=Mathf.Abs(car.transform.InverseTransformDirection(car.Body.linearVelocity).x);
            tires.volume=active&&car.Grounded&&car.SpeedKph>30?Mathf.Clamp01((slip-2)/8+(car.handbrake?.4f:0))*.10f:0;
        }
    }
}
