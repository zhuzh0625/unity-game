using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace IronBastion
{
    public sealed class TankActor : MonoBehaviour
    {
        public bool IsPlayer { get; private set; }
        public int Type { get; private set; }
        public bool Carrier { get; private set; }
        public PickupKind? FixedPickup;
        public int Upgrade;
        public float ShieldUntil;
        public bool IsProtected => Time.time < ShieldUntil;
        public int Health { get; private set; }
        public Vector3 Facing { get; private set; } = Vector3.forward;
        public int ActiveShells;
        public float Speed => IsPlayer ? 4.8f : (Type == 1 ? 3.25f : Type == 3 ? 1.85f : 2.45f) + (BattleGame.Instance.Stage - 1) * .18f;
        public Transform Visual { get; private set; }
        Transform shield;
        Vector3 waypoint;
        bool hasWaypoint, dead;
        float nextShot, nextPlan, jamTime, nextDust;
        Vector3 inputDirection = Vector3.forward;
        BoxCollider body;
        static readonly Vector2Int[] steps = { new(0,1), new(-1,0), new(1,0), new(0,-1) };

        public void Configure(bool player, int type, bool carrier)
        {
            IsPlayer = player; Type = type; Carrier = carrier; Health = type == 3 && !player ? 3 : 1;
            Visual = WorldBuilder.Model("Tank", transform, Vector3.zero).transform;
            Color color = player ? new Color(.95f,.66f,.19f) : type == 3 ? new Color(.58f,.49f,.69f) : type == 1 ? new Color(.40f,.66f,.65f) : new Color(.78f,.28f,.20f);
            foreach (var r in Visual.GetComponentsInChildren<Renderer>())
            {
                if (r.sharedMaterial && r.sharedMaterial.name.Contains("Paint"))
                { var block = new MaterialPropertyBlock(); block.SetColor("_BaseColor", color); r.SetPropertyBlock(block); }
            }
            body = gameObject.AddComponent<BoxCollider>(); body.center = Vector3.up * .62f; body.size = new(1.3f,1.24f,1.3f);
            Facing = player ? Vector3.forward : Vector3.back;
            Visual.rotation = Quaternion.LookRotation(Facing);
            var ring = new GameObject("Shield ring"); ring.transform.SetParent(transform, false); shield = ring.transform;
            var line = ring.AddComponent<LineRenderer>(); line.useWorldSpace = false; line.loop = true; line.positionCount = 40; line.widthMultiplier = .045f;
            line.sharedMaterial = WorldBuilder.Material("Shield glow", new Color(.32f,.92f,.86f),0,2);
            for (int i=0;i<40;i++) { float angle=i*Mathf.PI*2/40; line.SetPosition(i,new Vector3(Mathf.Cos(angle)*.98f,.16f,Mathf.Sin(angle)*.98f)); }
            if (carrier) WorldBuilder.Cube("Supply beacon", Visual, new(0,1.2f,0),new(.17f,.17f,.17f),WorldBuilder.Material("Supply light",new Color(1,.85f,.4f),0,2));
            nextShot = Time.time + 1.1f;
        }
        void Update()
        {
            if (!BattleGame.Instance || dead) return;
            shield.gameObject.SetActive(IsProtected);
            if(IsProtected)shield.localScale=Vector3.one*(1f+Mathf.Sin(Time.time*9f)*.07f);
            if (BattleGame.Instance.State != BattleState.Playing) return;
            if (IsPlayer) ReadInput();
            else if (Time.time >= BattleGame.Instance.FrozenUntil && Time.time >= ShieldUntil) Think();
            Visual.rotation = Quaternion.RotateTowards(Visual.rotation, Quaternion.LookRotation(Facing), Time.deltaTime * 1000);
        }
        void ReadInput()
        {
            var k = Keyboard.current; if (k == null) return;
            bool up=k.wKey.isPressed||k.upArrowKey.isPressed, down=k.sKey.isPressed||k.downArrowKey.isPressed;
            bool left=k.aKey.isPressed||k.leftArrowKey.isPressed, right=k.dKey.isPressed||k.rightArrowKey.isPressed;
            if (k.wKey.wasPressedThisFrame||k.upArrowKey.wasPressedThisFrame) inputDirection=Vector3.forward;
            if (k.sKey.wasPressedThisFrame||k.downArrowKey.wasPressedThisFrame) inputDirection=Vector3.back;
            if (k.aKey.wasPressedThisFrame||k.leftArrowKey.wasPressedThisFrame) inputDirection=Vector3.left;
            if (k.dKey.wasPressedThisFrame||k.rightArrowKey.wasPressedThisFrame) inputDirection=Vector3.right;
            bool held=inputDirection==Vector3.forward?up:inputDirection==Vector3.back?down:inputDirection==Vector3.left?left:right;
            if (!held) inputDirection=up?Vector3.forward:down?Vector3.back:left?Vector3.left:right?Vector3.right:Vector3.zero;
            if (inputDirection != Vector3.zero) Move(inputDirection, Time.deltaTime);
            if (k.spaceKey.isPressed||k.jKey.isPressed) Fire();
        }
        public bool Move(Vector3 direction, float dt)
        {
            if (direction == Vector3.zero || dead) return false;
            Facing = direction;
            float distance = Speed * Mathf.Min(dt,.1f);
            int count=Mathf.Max(1,Mathf.CeilToInt(distance/.12f));
            bool moved=false;
            for(int i=0;i<count;i++)
            {
                Vector3 next=transform.position + direction * (distance/count);
                if (!CanOccupy(next)) break;
                transform.position=next; moved=true;
            }
            if(moved && Time.time>nextDust) { nextDust=Time.time+.13f; BattleEffects.Dust(transform.position-Facing*.65f); }
            return moved;
        }
        public bool CanOccupy(Vector3 pos)
        {
            Physics.SyncTransforms();
            if (!WorldBuilder.Inside(pos)) return false;
            foreach (var hit in Physics.OverlapBox(pos+Vector3.up*.62f,new Vector3(.64f,.5f,.64f),Quaternion.identity,WorldBuilder.MovementMask,QueryTriggerInteraction.Ignore))
                if(hit!=body) return false;
            return true;
        }
        public bool Fire()
        {
            if (dead || BattleGame.Instance.State != BattleState.Playing || Time.time < nextShot || ActiveShells >= (IsPlayer && Upgrade>0?2:1)) return false;
            // Facing is the requested cardinal direction; the visible hull may still be turning.
            // Wait for the barrel to line up, without spending ammo or starting the cooldown.
            var shotRotation=Quaternion.LookRotation(Facing);
            if (Quaternion.Angle(Visual.rotation,shotRotation)>.1f) return false;
            Visual.rotation=shotRotation;
            nextShot=Time.time+(IsPlayer?(Upgrade>0?.24f:.42f):Type==2?.65f:1.1f);
            // Start inside the tank, then sweep outward so a close wall cannot be skipped by the muzzle.
            var go = new GameObject(IsPlayer?"Player shell":"Enemy shell"); go.transform.SetParent(BattleGame.Instance.DynamicRoot);
            go.transform.position=transform.position+Vector3.up*.78f+Facing*.45f;
            go.AddComponent<BattleShell>().Configure(this, Facing, IsPlayer&&Upgrade>=2?2:1, IsPlayer?20:13);
            ActiveShells++;
            BattleEffects.Spark(transform.position+Facing*1.15f+Vector3.up*.83f,true);
            BattleEffects.Tone(IsPlayer?155:105,.06f,.12f);
            return true;
        }
        public void Hit(bool bypassShield=false)
        {
            if (dead || (!bypassShield && IsProtected)) return;
            Health--;
            if (Health>0) { BattleEffects.Spark(transform.position+Vector3.up,true); return; }
            dead=true; body.enabled=false;
            BattleEffects.Explode(transform.position+Vector3.up*.5f,1.3f);
            BattleGame.Instance.TankDestroyed(this);
            gameObject.SetActive(false); Destroy(gameObject);
        }
        void Think()
        {
            var game = BattleGame.Instance;
            // Fire down a clear lane at the player; otherwise keep pressuring the headquarters.
            if(game.Player)
            {
                var delta=game.Player.transform.position-transform.position;
                if(delta.magnitude<10 && (Mathf.Abs(delta.x)<.6f||Mathf.Abs(delta.z)<.6f))
                {
                    Vector3 aim=Mathf.Abs(delta.x)<.6f?(delta.z>0?Vector3.forward:Vector3.back):(delta.x>0?Vector3.right:Vector3.left);
                    if(!Physics.Raycast(transform.position+Vector3.up*.78f+aim*.8f,aim,delta.magnitude-.8f,1<<WorldBuilder.SolidLayer))
                    {
                        Facing=aim;
                        Fire();
                        // Aiming owns this frame, including frames spent turning or reloading.
                        // Navigation must not overwrite the direction used by the shot.
                        return;
                    }
                }
            }
            if(!hasWaypoint || Time.time>nextPlan)
            { waypoint=FindNextWaypoint(); hasWaypoint=true; nextPlan=Time.time+1.3f; }
            Vector3 d=waypoint-transform.position; d.y=0;
            if(d.magnitude<.06f)
            {
                transform.position=waypoint; hasWaypoint=false;
                if(Vector3.Distance(transform.position,WorldBuilder.BasePosition)<2.2f) { Facing=Vector3.back; Fire(); }
                return;
            }
            Vector3 direction=Mathf.Abs(d.x)>.05f?(d.x>0?Vector3.right:Vector3.left):(d.z>0?Vector3.forward:Vector3.back);
            bool moved=Move(direction,Mathf.Min(Time.deltaTime,d.magnitude/Speed));
            if(!moved) { jamTime+=Time.deltaTime; Fire(); if(jamTime>1.8f) { nextPlan=0; jamTime=0; } }
            else { jamTime=0; if(Random.value<Time.deltaTime*.65f) Fire(); }
        }
        Vector3 FindNextWaypoint()
        {
            Physics.SyncTransforms();
            Vector2Int start=WorldBuilder.Grid(transform.position), goal=new(6,11);
            Vector3 center=WorldBuilder.Cell(start.x,start.y);
            // Always finish a cell before changing the route; this prevents diagonal corner clipping.
            if(Vector3.Distance(center,transform.position)>.08f && CanOccupy(center)) return center;
            const int n=169;
            var costs=new float[n]; var previous=new int[n]; var visited=new bool[n]; var weight=new float[n];
            for(int i=0;i<n;i++) { costs[i]=float.PositiveInfinity; previous[i]=-1; weight[i]=1; }
            for(int r=0;r<13;r++) for(int c=0;c<13;c++)
            {
                int i=r*13+c;
                foreach(var col in Physics.OverlapBox(WorldBuilder.Cell(c,r)+Vector3.up*.6f,new Vector3(.7f,.5f,.7f),Quaternion.identity,WorldBuilder.MovementMask))
                {
                    if(col==body) continue;
                    var b=col.GetComponent<BattleBlock>();
                    if(b && b.Kind==SurfaceKind.Brick) weight[i]=Mathf.Max(weight[i],4);
                    else if(col.GetComponent<TankActor>()) weight[i]=Mathf.Max(weight[i],7);
                    else weight[i]=9999;
                }
            }
            int from=start.y*13+start.x, target=goal.y*13+goal.x; costs[from]=0;
            for(int k=0;k<n;k++)
            {
                int current=-1; float best=float.PositiveInfinity;
                for(int i=0;i<n;i++) if(!visited[i]&&costs[i]<best) {best=costs[i];current=i;}
                if(current<0||current==target) break;
                visited[current]=true;
                foreach(var step in steps)
                {
                    int x=current%13+step.x,y=current/13+step.y;
                    if(x<0||x>12||y<0||y>12) continue;
                    int next=y*13+x; if(weight[next]>=9999) continue;
                    float cost=costs[current]+weight[next];
                    if(cost<costs[next]) {costs[next]=cost;previous[next]=current;}
                }
            }
            if(from==target) return center;
            if(previous[target]<0) return transform.position+Vector3.back*.3f;
            int at=target;
            while(previous[at]!=from&&previous[at]>=0) at=previous[at];
            return WorldBuilder.Cell(at%13,at/13);
        }
    }
}
