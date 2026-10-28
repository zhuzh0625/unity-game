using UnityEngine;
using UnityEngine.InputSystem;

namespace CloudSprout
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class SproutPlayer : MonoBehaviour
    {
        public Transform visual;
        public float walkSpeed = 5.4f, runSpeed = 7.6f, jumpSpeed = 11.8f, gravity = 27f;
        public bool automation;
        public float autoMove;
        public bool autoJump, autoHold, autoRun;
        public float VerticalSpeed => velocity.y;
        public float PreviousFeetY {get;private set;}
        public bool Grounded => grounded;
        public int JumpCount { get; private set; }
        public Vector3 Velocity => velocity;
        CharacterController controller;
        Vector3 velocity;
        bool grounded, wasGrounded;
        float coyote, buffer, facing = 1, animTime, squash, invincibleUntil;
        Transform legL, legR, armL, armR;
        Renderer[] renderers;
        Quaternion visualRest;
        MovingPlatform ride;
        Vector3 rideLast;
        public bool Invincible => Time.time < invincibleUntil;

        void Awake()
        {
            controller = GetComponent<CharacterController>();
            renderers = visual.GetComponentsInChildren<Renderer>();
            visualRest=visual.localRotation;
            foreach (var t in visual.GetComponentsInChildren<Transform>())
            {
                if(t.name=="LegL") legL=t; if(t.name=="LegR") legR=t;
                if(t.name=="ArmL") armL=t; if(t.name=="ArmR") armR=t;
            }
        }
        void Update()
        {
            var game = GameSession.Instance;
            if (game == null || game.State != GameState.Playing) return;
            float dt = Time.deltaTime;
            var k = Keyboard.current;
            float input = automation ? autoMove : k == null ? 0 : ((k.dKey.isPressed||k.rightArrowKey.isPressed?1:0)-(k.aKey.isPressed||k.leftArrowKey.isPressed?1:0));
            bool press = automation ? autoJump : k != null && (k.spaceKey.wasPressedThisFrame||k.wKey.wasPressedThisFrame||k.upArrowKey.wasPressedThisFrame);
            bool hold = automation ? autoHold : k != null && (k.spaceKey.isPressed||k.wKey.isPressed||k.upArrowKey.isPressed);
            bool run = automation ? autoRun : k != null && (k.leftShiftKey.isPressed||k.rightShiftKey.isPressed);
            autoJump = false;
            wasGrounded=grounded;
            grounded=controller.isGrounded;
            // Read grounded before carry Move, which can clear Below on an ascending platform.
            if(ride != null) { controller.Move(ride.transform.position-rideLast); rideLast=ride.transform.position; }
            if(grounded && velocity.y<=0) { coyote=.12f; velocity.y=-2; } else coyote-=dt;
            if(press) buffer=.13f; else buffer-=dt;
            velocity.x=Mathf.MoveTowards(velocity.x,input*(run?runSpeed:walkSpeed),(grounded?44:29)*dt);
            if(buffer>0 && coyote>0)
            {
                velocity.y=jumpSpeed;buffer=0;coyote=0;grounded=false;ride=null;JumpCount++;
                game.Sound("jump", .48f);squash=-.12f;
            }
            if(!hold && velocity.y>4.5f) velocity.y=4.5f;
            velocity.y=Mathf.Max(-24,velocity.y-gravity*dt);
            PreviousFeetY=transform.position.y;
            var flags=controller.Move(velocity*dt);
            if((flags&CollisionFlags.Above)!=0 && velocity.y>0)velocity.y=0;
            grounded=(flags&CollisionFlags.Below)!=0;
            if(grounded && !wasGrounded && velocity.y < -4) { squash=.22f;game.Sound("land",.22f); }
            if(!grounded && velocity.y<0) ride=null;
            var pos=transform.position;pos.z=0;transform.position=pos;
            if(pos.y < -8) game.Die();
            if(Mathf.Abs(input)>.01f)facing=Mathf.Sign(input);
            Animate(dt);
        }
        void Animate(float dt)
        {
            animTime+=dt*Mathf.Abs(velocity.x)*2.3f;
            float swing=grounded?Mathf.Sin(animTime)*Mathf.Min(Mathf.Abs(velocity.x)/5,1)*32: -24;
            if(legL)legL.localRotation=Quaternion.Euler(swing,0,0);
            if(legR)legR.localRotation=Quaternion.Euler(-swing,0,0);
            if(armL)armL.localRotation=Quaternion.Euler(-swing*.85f,0,grounded?0:-25);
            if(armR)armR.localRotation=Quaternion.Euler(swing*.85f,0,grounded?0:25);
            visual.localRotation=Quaternion.Slerp(visual.localRotation,Quaternion.Euler(0,-facing*58,0)*visualRest,dt*14);
            squash=Mathf.MoveTowards(squash,0,dt*1.6f);
            visual.localScale=new Vector3(1+squash*.35f,1-squash,1+squash*.35f);
            visual.localPosition=new Vector3(0,grounded?Mathf.Abs(Mathf.Sin(animTime))*.035f:0,0);
            bool show=!Invincible || Mathf.FloorToInt(Time.time*14)%2==0;
            foreach(var r in renderers)r.enabled=show;
        }
        void OnControllerColliderHit(ControllerColliderHit hit)
        {
            if(hit.normal.y>.5f)
            {
                var platform=hit.collider.GetComponent<MovingPlatform>();
                ride=platform;
                if(ride)rideLast=ride.transform.position;
            }
            if(hit.normal.y<-.5f && velocity.y>0)hit.collider.GetComponent<RewardBlock>()?.Hit();
        }
        public void Bounce() { velocity.y=9.5f;coyote=0;grounded=false;ride=null; }
        public void Respawn(Vector3 position)
        {
            controller.enabled=false;transform.position=position;PreviousFeetY=position.y;controller.enabled=true;
            velocity=Vector3.zero;ride=null;coyote=buffer=0;grounded=false;
            invincibleUntil=Time.time+1.5f;
            foreach(var r in renderers)r.enabled=true;
        }
        public void Hurt()
        {
            if(Invincible || GameSession.Instance.State!=GameState.Playing)return;
            GameSession.Instance.Damage();
            invincibleUntil=Time.time+1.7f;velocity.y=7;velocity.x=-facing*3;
        }
    }
}
