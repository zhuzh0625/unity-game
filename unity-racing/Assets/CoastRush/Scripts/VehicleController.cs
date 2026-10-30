using UnityEngine;
using UnityEngine.InputSystem;

namespace CoastRush
{
    [RequireComponent(typeof(Rigidbody))]
    public sealed class VehicleController : MonoBehaviour
    {
        public bool isPlayer;
        public string driverName;
        public float aiPace = 1;
        public float aiLane;
        public CoastTrack track;
        public WheelCollider[] wheels;
        public Transform[] wheelVisuals;
        public Renderer[] paintRenderers;
        public Renderer[] brakeRenderers;
        public Color paintColor = new Color(.96f,.27f,.08f);
        public ParticleSystem[] tireSmoke;
        public RaceProgress progress = new RaceProgress();
        public bool useAutopilot;
        public bool overrideControls;
        public Vector3 testInput;
        public float throttle,steering,braking;
        public bool handbrake;
        public float SpeedKph => Body ? Body.linearVelocity.magnitude*3.6f : 0;
        public float ForwardSpeed => Body ? Vector3.Dot(Body.linearVelocity,transform.forward) : 0;
        public Rigidbody Body { get; private set; }
        public bool Grounded { get; private set; }
        public int respawns;
        Quaternion[] wheelOffsets;
        float smoothSteer,stuckTime,offRoadTime,respawnCooldown;
        MaterialPropertyBlock paintBlock, brakeBlock;
        public float SteeringAngle => Mathf.Lerp(30,12,Mathf.Clamp01(Mathf.Abs(ForwardSpeed)/48));

        void Awake()
        {
            Body=GetComponent<Rigidbody>(); Body.centerOfMass=new Vector3(0,.18f,0);
            Body.maxAngularVelocity=4; Body.solverIterations=12;
            wheelOffsets=new Quaternion[wheelVisuals.Length];
            for(int i=0;i<wheelVisuals.Length;i++) wheelOffsets[i]=Quaternion.Inverse(transform.rotation)*wheelVisuals[i].rotation;
            paintBlock=new MaterialPropertyBlock();brakeBlock=new MaterialPropertyBlock();Paint(paintColor);
        }

        public void Paint(Color color)
        {
            paintColor=color;
            if(paintBlock==null) paintBlock=new MaterialPropertyBlock();
            paintBlock.SetColor("_BaseColor",color);
            foreach(var renderer in paintRenderers) renderer.SetPropertyBlock(paintBlock);
        }

        void ReadControls()
        {
            throttle=steering=braking=0;handbrake=false;
            var race=RaceDirector.Instance;
            if(race==null || race.State!=RaceState.Racing || progress.finished) {braking=1;return;}
            if(overrideControls){throttle=testInput.x;steering=testInput.y;braking=testInput.z;return;}
            if(!isPlayer || useAutopilot){DriveAI();return;}
            var k=Keyboard.current;if(k==null)return;
            bool gas=k.wKey.isPressed||k.upArrowKey.isPressed;
            bool slow=k.sKey.isPressed||k.downArrowKey.isPressed;
            steering=(k.dKey.isPressed||k.rightArrowKey.isPressed?1:0)-(k.aKey.isPressed||k.leftArrowKey.isPressed?1:0);
            if(gas){if(ForwardSpeed < -1)braking=1;else throttle=1;}
            if(slow){if(ForwardSpeed > 1.5f)braking=1;else throttle=-.55f;}
            handbrake=k.spaceKey.isPressed;
        }

        void DriveAI()
        {
            float distance=track.Project(transform.position,out float lateral,out float separation);
            float speed=Mathf.Max(0,ForwardSpeed);
            float lookAhead=9+speed*.65f;
            Vector3 target=track.Position(distance+lookAhead,aiLane);
            float angle=Vector3.SignedAngle(transform.forward,Vector3.ProjectOnPlane(target-transform.position,Vector3.up),Vector3.up);
            steering=Mathf.Clamp(angle/(SteeringAngle*.78f),-1,1);
            float curve=Vector3.Angle(track.Forward(distance+8),track.Forward(distance+52));
            float desired=Mathf.Clamp(45-curve*.62f,15,45)*aiPace;
            desired*=Mathf.Lerp(1,.62f,Mathf.Clamp01(Mathf.Abs(lateral-aiLane)/5));
            if(Physics.SphereCast(transform.position+transform.up*.7f, .65f,transform.forward,out var hit,5+speed*.35f,1<<9,QueryTriggerInteraction.Ignore))
            { if(hit.rigidbody!=Body){desired=Mathf.Min(desired,Mathf.Max(7,speed-5));steering=Mathf.Clamp(steering+Mathf.Sign(aiLane+.2f)*.18f,-1,1);} }
            throttle=speed<desired ? Mathf.Clamp01((desired-speed)*.24f+.2f) : .07f;
            braking=speed>desired+1 ? Mathf.Clamp01((speed-desired)*.18f) : 0;
            if(Mathf.Abs(angle)>80 && speed<2){throttle=-.45f;steering=-steering;}
            stuckTime=speed<1.5f ? stuckTime+Time.fixedDeltaTime : 0;
            if(stuckTime>4 || separation>20) Respawn();
        }

        void FixedUpdate()
        {
            ReadControls();
            if(Body.isKinematic)return;
            Grounded=false;
            smoothSteer=Mathf.MoveTowards(smoothSteer,steering,Time.fixedDeltaTime*2.8f);
            float speed=Mathf.Abs(ForwardSpeed);
            for(int i=0;i<wheels.Length;i++)
            {
                var w=wheels[i]; bool front=i<2;
                w.steerAngle=front?smoothSteer*SteeringAngle:0;
                w.motorTorque=throttle*1050f*Mathf.Clamp01(1-speed/(throttle<0?11:53));
                w.brakeTorque=braking*2800+(handbrake&&!front?1200:0);
                var side=w.sidewaysFriction;side.stiffness=handbrake&&!front?.72f:2.0f;w.sidewaysFriction=side;
                Grounded|=w.isGrounded;
            }
            if(Grounded)
            {
                Body.AddForce(-transform.up*(speed*.65f),ForceMode.Acceleration);
                var local=transform.InverseTransformDirection(Body.linearVelocity);
                Body.AddForce(-transform.right*local.x*(handbrake?.4f:1.25f),ForceMode.Acceleration);
                Body.AddTorque(Vector3.Cross(transform.up,Vector3.up)*8,ForceMode.Acceleration);
            }
            respawnCooldown=Mathf.Max(0,respawnCooldown-Time.fixedDeltaTime);
            if(RaceDirector.Instance && RaceDirector.Instance.State==RaceState.Racing)
            {
                track.Project(transform.position,out float side,out float separation);
                bool lost=transform.position.y < -3 || Vector3.Dot(transform.up,Vector3.up)<.25f || separation>25;
                offRoadTime=lost?offRoadTime+Time.fixedDeltaTime:0;
                if(offRoadTime>2.2f)Respawn();
            }
        }

        void LateUpdate()
        {
            if(wheelOffsets==null)return;
            for(int i=0;i<wheels.Length;i++)
            {wheels[i].GetWorldPose(out var p,out var q);wheelVisuals[i].SetPositionAndRotation(p,q*wheelOffsets[i]);}
            brakeBlock.SetColor("_EmissionColor",new Color(1,.035f,.005f)*(braking>.1f||handbrake?4f:.3f));
            foreach(var r in brakeRenderers)r.SetPropertyBlock(brakeBlock);
            bool smoke=Grounded&&SpeedKph>35&&(handbrake||Mathf.Abs(transform.InverseTransformDirection(Body.linearVelocity).x)>5);
            foreach(var ps in tireSmoke){var em=ps.emission;em.rateOverTime=smoke?22:0;}
        }

        public void Place(float distance,float lane,bool kinematic)
        {
            if(!Body)Body=GetComponent<Rigidbody>();
            Body.isKinematic=false;Body.linearVelocity=Vector3.zero;Body.angularVelocity=Vector3.zero;
            transform.SetPositionAndRotation(track.Position(distance,lane)+Vector3.up*(kinematic?0:.22f),track.Rotation(distance));
            Body.position=transform.position;Body.rotation=transform.rotation;Body.isKinematic=kinematic;
            smoothSteer=stuckTime=offRoadTime=0;
            progress.previousPosition=transform.position;
        }

        public void Respawn()
        {
            if(respawnCooldown>0)return;
            float distance=progress.started?(progress.nextGate-1)*track.length/CoastTrack.GateCount+4:-12;
            // Never advance beyond the next mandatory checkpoint, even after a shortcut.
            Place(distance,isPlayer?-1.7f:aiLane,false);
            respawnCooldown=2;respawns++;
            if(isPlayer && RaceDirector.Instance)RaceDirector.Instance.ShowMessage("已返回安全路段");
        }
    }
}
