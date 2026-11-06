using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Emberfall
{
    public partial class Adventure : MonoBehaviour
    {
        public static Adventure Instance;
        public AdventureData data;
        public int zone; // 0 camp, 1 wilderness, 2 crypt
        public float health, mana = 100;
        public bool inventory, paused, dead, help;
        public int selection;
        public string message = "Welcome, wanderer. E at the eastern gate to begin.";
        public Transform player;
        public Vector2 facing = Vector2.right;
        public int kills;
        public float[] cooldown = new float[4];
        public List<Foe> enemies = new List<Foe>();
        public List<Drop> drops = new List<Drop>();
        public List<Rect> walls = new List<Rect>();
        readonly List<Missile> missiles = new List<Missile>();
        readonly List<Pulse> pulses = new List<Pulse>();
        Transform world;
        AdventureHud hud;
        Camera view;
        float invulnerable, potionCooldown, timeInZone;
        Transform campfire;
        public bool Modal => paused || inventory || dead || help;
        public Vector2 Position => player.position;
        public class Foe
        {
            public GameObject go, telegraph, marker;
            public Transform bar, fill;
            public ActorVisual visual;
            public int kind;
            public float hp, max, attack, slow, windup, recovery;
            public Vector2 target;
            public bool enraged;
        }
        public class Drop { public GameObject go; public Gear gear; }
        struct Missile { public GameObject go; public ProjectileVisual visual; public Vector2 velocity; public float life; public bool hostile; public float damage; }
        struct Pulse { public GameObject go; public float life, max; }
        ZoneNavigation navigation;
        CombatFeedback feedback;
        ActorVisual playerVisual;
        Transform facingMarker;
        public int InventoryRevision { get; private set; }
        public Foe CurrentTarget { get; private set; }
        public float DodgeRemaining => dodgeCooldown;
        public bool IsDodging => dodgeTime > 0;
        public bool IsAttacking => pendingAttack >= 0;
        public bool SimulationPaused => Modal || hitStop > 0;

        void Awake()
        {
            Instance = this;
            Application.runInBackground = true;
            Debug.Log($"DISPLAY_BEFORE refresh={Screen.currentResolution.refreshRateRatio.value:0.##} vsync={QualitySettings.vSyncCount} target={Application.targetFrameRate} renderInterval={UnityEngine.Rendering.OnDemandRendering.renderFrameInterval}");
            // Keep desktop play independent of a remote/virtual display's refresh rate.
            QualitySettings.vSyncCount=0;
            Application.targetFrameRate=60;
            UnityEngine.Rendering.OnDemandRendering.renderFrameInterval=1;
            data = AdventureSave.Read(); health = data.MaxHealth;
            navigation = new ZoneNavigation(walls);
            feedback = gameObject.AddComponent<CombatFeedback>();
            feedback.Initialize(data.muted);
            view = Camera.main;
            if (!view) view = new GameObject("Main Camera", typeof(Camera)).GetComponent<Camera>();
            view.tag = "MainCamera"; view.orthographic = true; view.orthographicSize = 7;
            // URP 2D leaves stale pixels outside a partial viewport on this driver.
            // Clear the full target; opaque HUD bands cover the reserved screen edges.
            view.rect=new Rect(0,0,1,1);
            view.clearFlags = CameraClearFlags.SolidColor; view.backgroundColor = new Color(.025f,.035f,.045f);
            hud = gameObject.AddComponent<AdventureHud>(); hud.game = this;
            EnterZone(0, -1);
        }
        static bool Press(Key key) => Keyboard.current != null && Keyboard.current[key].wasPressedThisFrame;
        static bool Hold(Key key) => Keyboard.current != null && Keyboard.current[key].isPressed;
        void Update()
        {
            if(Press(Key.F1)&&!dead){help=!help;if(help)inventory=false;}
            if(Press(Key.M)){data.muted=!data.muted;feedback.SetMuted(data.muted);Save(false);}
            if (Press(Key.Escape)) { if(help)help=false; else if (inventory) inventory = false; else if (!dead) paused = !paused; }
            if(Modal)queuedAttack=-1;
            if(help){hud.Refresh();return;}
            if (Press(Key.B) && !paused && !dead) { inventory = !inventory; selection = Mathf.Clamp(selection, 0, data.bag.Count - 1); }
            if (dead) { if (Press(Key.Enter)) Respawn(); hud.Refresh(); return; }
            if (paused) { if (Press(Key.F5)) Save(); if (Press(Key.Enter)) paused = false; hud.Refresh(); return; }
            if (inventory)
            {
                if (Press(Key.W) || Press(Key.UpArrow)) selection--;
                if (Press(Key.S) || Press(Key.DownArrow)) selection++;
                selection = Mathf.Clamp(selection, 0, Mathf.Max(0, data.bag.Count - 1));
                if (Press(Key.Enter)) EquipSelected();
                if (Press(Key.Delete)) DiscardSelected();
                if (Press(Key.V)) SellSelected();
                if (Press(Key.R)) UpgradeSelected();
                hud.Refresh(); return;
            }
            float dt = Mathf.Min(Time.deltaTime, .05f); timeInZone += dt;
            if(hitStop>0)
            {
                if(Press(Key.U))RequestAttack(1);else if(Press(Key.I))RequestAttack(2);else if(Press(Key.O))RequestAttack(3);
                if(Press(Key.K))Dodge(facing);
                if(Press(Key.H))Drink();
                if(Press(Key.E))Interact();
                if(Press(Key.F5))Save();
                hitStop-=dt;hud.Refresh();return;
            }
            if(campfire)campfire.localScale=Vector3.one*(2.1f+Mathf.Sin(timeInZone*5)*.08f);
            for (int i = 0; i < cooldown.Length; i++) cooldown[i] = Mathf.Max(0, cooldown[i] - dt);
            invulnerable -= dt; potionCooldown -= dt; mana = Mathf.Min(100, mana + dt * data.FocusRecovery);
            dodgeCooldown=Mathf.Max(0,dodgeCooldown-dt);
            Vector2 input = new Vector2((Hold(Key.D) || Hold(Key.RightArrow) ? 1 : 0) - (Hold(Key.A) || Hold(Key.LeftArrow) ? 1 : 0),
                (Hold(Key.W) || Hold(Key.UpArrow) ? 1 : 0) - (Hold(Key.S) || Hold(Key.DownArrow) ? 1 : 0));
            if(input.sqrMagnitude>0&&!IsAttacking&&!IsDodging)facing=input.normalized;
            if(Press(Key.K)) Dodge(input.sqrMagnitude>0?input.normalized:facing);
            if(IsDodging){Move(player,dodgeDirection*14*dt);dodgeTime=Mathf.Max(0,dodgeTime-dt);}
            else if(input.sqrMagnitude>0)Move(player,input.normalized*(Hold(Key.LeftShift)?7:4.8f)*(IsAttacking?.55f:1)*dt);
            facingMarker.localPosition=facing*.7f;
            playerVisual.Face(facing);
            CurrentTarget=FindTarget(10);
            TickBufferedAttack(dt);
            if (Press(Key.U)) RequestAttack(1);
            else if (Press(Key.I)) RequestAttack(2);
            else if (Press(Key.O)) RequestAttack(3);
            else if (Hold(Key.J) || Hold(Key.Space) || (Mouse.current != null && Mouse.current.leftButton.isPressed && !UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())) UseAttack(0);
            if (Press(Key.H)) Drink();
            if (Press(Key.E)) Interact();
            if (Press(Key.F5)) Save();
            TickAttack(dt); TickEnemies(dt); TickMissiles(dt); TickPulses(dt);
            FollowCamera();
            hud.Refresh();
        }
        public void Move(Transform actor, Vector2 delta)
        {
            Vector2 p = actor.position;
            int steps=Mathf.Max(1,Mathf.CeilToInt(delta.magnitude/.15f));delta/=steps;
            for(int i=0;i<steps;i++)
            {
                var x = new Vector2(Mathf.Clamp(p.x + delta.x, -21, 21), p.y);
                if (Walkable(x)) p = x;
                var y = new Vector2(p.x, Mathf.Clamp(p.y + delta.y, -11, 11));
                if (Walkable(y)) p = y;
            }
            actor.position = p;
        }
        bool Walkable(Vector2 p)
        {
            return navigation.Walkable(p);
        }
        public void EnterZone(int target, int from = -1)
        {
            zone = target; timeInZone = 0;
            if (world) Destroy(world.gameObject);
            world = new GameObject("World").transform;
            walls.Clear(); enemies.Clear(); drops.Clear(); missiles.Clear(); pulses.Clear();
            feedback.Clear();pendingAttack=-1;queuedAttack=-1;dodgeTime=0;CurrentTarget=null;campfire=null;
            var random = UnityEngine.Random.state; UnityEngine.Random.InitState(400 + zone);
            Color ground = zone == 2 ? new Color(.12f,.14f,.18f) : zone == 0 ? new Color(.17f,.20f,.18f) : new Color(.16f,.21f,.19f);
            for(int i=0;i<1056;i++)UnityEngine.Random.Range(.82f,1.18f); // Keep the existing seeded obstacle layout.
            WorldArt.Ground(world,Vector2.zero,zone);
            WorldArt.Landscape(world,zone);
            WorldArt.Shape("North wall",world,new Vector2(0,12),new Vector2(45,1),new Color(.08f,.1f,.12f),2);
            WorldArt.Shape("South wall",world,new Vector2(0,-12),new Vector2(45,1),new Color(.08f,.1f,.12f),2);
            WorldArt.Shape("West wall",world,new Vector2(-22,0),new Vector2(1,25),new Color(.08f,.1f,.12f),2);
            WorldArt.Shape("East wall",world,new Vector2(22,0),new Vector2(1,25),new Color(.08f,.1f,.12f),2);
            if (zone == 0)
            {
                Obstacle(new Rect(-8,4,5,3), new Color(.24f,.29f,.29f));
                Obstacle(new Rect(2,5,5,3), new Color(.26f,.23f,.20f));
                campfire=WorldArt.Prop(7,"Bonfire",world,new Vector2(-4,0),Vector2.one*2.1f,6).transform;
                WorldArt.Shape("Glow",world,new Vector2(-4,0),Vector2.one*6,new Color(1,.42f,.10f,.25f),-15,true);
                Marker(new Vector2(18,0), new Color(.35f,.8f,.6f));
                message = data.returned ? "Contract complete. E at the fire to prepare another expedition." : "E by the fire: heal & refill. East gate: wilderness [E].";
            }
            else if (zone == 1)
            {
                for (int i = 0; i < 26; i++)
                {
                    var p = new Vector2(UnityEngine.Random.Range(-19,20),UnityEngine.Random.Range(-10,11));
                    if (Mathf.Abs(p.y) < 2 || p.x < -16 || p.x > 16) continue;
                    Obstacle(new Rect(p.x,p.y,1.3f,1.4f),new Color(.11f,.24f,.20f));
                }
                Marker(new Vector2(-19,0),new Color(.35f,.8f,.6f)); Marker(new Vector2(19,0),new Color(.65f,.45f,.9f));
                Spawn(0,new Vector2(-10,0)); Spawn(0,new Vector2(-3,3)); Spawn(1,new Vector2(2,-3));
                Spawn(2,new Vector2(7,1)); Spawn(1,new Vector2(12,4)); Spawn(0,new Vector2(14,-3));
                message = "ASHEN ROAD / Follow the eastern trail to the crypt. J to strike; E to collect.";
            }
            else
            {
                // Two rooms with an opening in the dividing wall: a fixed, reproducible dungeon.
                Obstacle(new Rect(-1,-11,2,8),new Color(.23f,.23f,.29f));
                Obstacle(new Rect(-1,3,2,8),new Color(.23f,.23f,.29f));
                foreach (var p in new[] {new Vector2(-10,5),new Vector2(-10,-5),new Vector2(10,5),new Vector2(10,-5)})
                    Obstacle(new Rect(p.x,p.y,1.2f,1.2f),new Color(.28f,.27f,.32f));
                Marker(new Vector2(-19,0),new Color(.4f,.8f,.6f));
                Spawn(0,new Vector2(-10,1)); Spawn(1,new Vector2(-5,-3)); Spawn(2,new Vector2(5,2));
                if (!data.bossDefeated) Spawn(3,new Vector2(14,0));
                else Marker(new Vector2(14,0),new Color(.9f,.68f,.3f));
                message = "HOLLOW CRYPT / The Warden waits in the eastern chamber. Avoid the red warning circles.";
            }
            UnityEngine.Random.state = random;
            WorldArt.Decorate(world,zone);
            navigation.Rebuild();
            Vector2 entry=zone==0?(from==1?new Vector2(16,0):new Vector2(-1,0)):zone==1&&from==2?new Vector2(17,0):new Vector2(-17,0);
            player = WorldArt.Actor("Wanderer",world,entry,new Color(.32f,.66f,.73f)).transform;
            playerVisual=player.GetComponent<ActorVisual>();
            facing=from>target?Vector2.left:Vector2.right;
            facingMarker=WorldArt.Shape("Facing",player,new Vector2(.7f,0),Vector2.one*.13f,new Color(1,.85f,.4f),13,true).transform;
            WorldArt.Shape("Player marker",player,new Vector2(0,-.4f),new Vector2(.85f,.32f),new Color(.35f,.85f,.85f,.6f),9,true).GetComponent<SpriteRenderer>().sprite=WorldArt.Ring;
            FollowCamera();
        }
        void FollowCamera()
        {
            view.orthographicSize=zone==0?8.5f:7;
            float limitX=Mathf.Max(0,23-view.orthographicSize*view.aspect);
            view.transform.position=new Vector3(Mathf.Clamp(Position.x,-limitX,limitX),Mathf.Clamp(Position.y+(zone==0?3.3f:1),-6,6),-10);
        }
        void Obstacle(Rect rect,Color color)
        {
            walls.Add(rect);
            if(zone==0)WorldArt.Prop(9,"Camp tent",world,rect.center+Vector2.up*.25f,new Vector2(rect.width+1,rect.height+1.2f),3);
            else if(zone==1)WorldArt.Prop(5,"Old evergreen",world,rect.center+Vector2.up*1.2f,new Vector2(3.2f,4.2f),75-Mathf.RoundToInt(rect.yMin*4));
            else if(rect.height>3)
            {
                WorldArt.Shape("Wall face",world,rect.center,rect.size,new Color(.38f,.38f,.42f),2);
                WorldArt.Shape("Rim",world,new Vector2(rect.center.x,rect.yMax),new Vector2(rect.width,.14f),new Color(.5f,.47f,.42f),3);
            }
            else WorldArt.Prop(14,"Ruined column",world,rect.center+Vector2.up*.5f,new Vector2(1.8f,2.4f),4);
        }
        void Marker(Vector2 p,Color color)
        {
            WorldArt.Shape("Glow",world,p,Vector2.one*4,new Color(color.r,color.g,color.b,.28f),-14,true);
            bool memorial=color.r>.8f&&color.g>.5f&&color.b<.4f;
            WorldArt.Prop(memorial?10:8,memorial?"Warden memorial":"Gateway",world,p+Vector2.up*.6f,memorial?new Vector2(1.8f,2.4f):new Vector2(2.7f,3.3f),4);
        }
        public Foe Spawn(int kind,Vector2 position)
        {
            if(!Walkable(position))
                for(int i=1;i<=20;i++)
                {
                    var candidate=position+Vector2.down*i*.5f;
                    if(Mathf.Abs(candidate.y)<10 && Walkable(candidate)){position=candidate;break;}
                }
            var color = new[] {new Color(.7f,.4f,.3f), new Color(.65f,.7f,.45f),new Color(.65f,.4f,.7f),new Color(.85f,.36f,.19f)}[kind];
            var f = new Foe {kind=kind,max=(kind==3?360:kind==1?58:40)*(1+data.ExpeditionRank*.12f),attack=.7f}; f.hp=f.max;
            f.go=WorldArt.Actor(new[]{"Ravager","Bone Archer","Mire Hexer","The Warden"}[kind],world,position,color,kind==3?1.7f:1);
            f.visual=f.go.GetComponent<ActorVisual>();
            f.bar=new GameObject("Enemy health").transform;f.bar.SetParent(f.go.transform,false);f.bar.localPosition=new Vector2(0,kind==3?2.2f:1.5f);
            WorldArt.Shape("Background",f.bar,Vector2.zero,new Vector2(1.2f,.09f),new Color(.05f,.02f,.02f,.9f),490);
            f.fill=WorldArt.Shape("Health",f.bar,Vector2.zero,new Vector2(1.2f,.07f),new Color(.8f,.2f,.12f),491).transform;
            f.marker=WorldArt.Shape("Target marker",f.go.transform,new Vector2(0,-.4f),new Vector2(1.1f,.36f),new Color(1,.75f,.35f,.8f),9,true);
            f.marker.GetComponent<SpriteRenderer>().sprite=WorldArt.Ring;f.marker.SetActive(false);f.bar.gameObject.SetActive(false);
            enemies.Add(f); return f;
        }
        public enum InteractionKind { None, Loot, Fire, Exit }
        public struct InteractionTarget { public InteractionKind kind; public Drop drop; public int destination; }
        public InteractionTarget ResolveInteraction()
        {
            // Entrances and the fire remain usable even with a full bag and loot underfoot.
            if(zone==0&&Vector2.Distance(Position,new Vector2(-4,0))<3)return new InteractionTarget{kind=InteractionKind.Fire};
            if(zone==0&&Vector2.Distance(Position,new Vector2(18,0))<3)return new InteractionTarget{kind=InteractionKind.Exit,destination=1};
            if(zone>0&&Vector2.Distance(Position,new Vector2(-19,0))<3)return new InteractionTarget{kind=InteractionKind.Exit,destination=zone-1};
            if(zone==1&&Vector2.Distance(Position,new Vector2(19,0))<3)return new InteractionTarget{kind=InteractionKind.Exit,destination=2};
            Drop nearest=null;float best=2.4f;
            foreach(var d in drops) {float dist=Vector2.Distance(Position,d.go.transform.position);if(dist<best&&CanSee(Position,d.go.transform.position)){best=dist;nearest=d;}}
            return nearest==null?default:new InteractionTarget{kind=InteractionKind.Loot,drop=nearest};
        }
        public void Interact()
        {
            if(Modal)return;
            var target=ResolveInteraction();
            if(target.kind==InteractionKind.Loot)
            {
                if(data.bag.Count>=16) {message="Pack full. B to equip or discard gear.";return;}
                data.bag.Add(target.drop.gear);message="Collected "+target.drop.gear.Description;Destroy(target.drop.go);drops.Remove(target.drop);
                InventoryRevision++;feedback.Play(CombatSound.Loot);Save(false);return;
            }
            if(target.kind==InteractionKind.Fire)
            {
                health=data.MaxHealth;mana=100;data.potions=5;
                if(data.bossDefeated){data.bossDefeated=false;data.returned=false;}
                message="Rested. Potions refilled. Ready for the eastern gate.";feedback.Play(CombatSound.Heal);Save(false);return;
            }
            if(target.kind==InteractionKind.Exit)
            {
                int previous=zone;if(target.destination==0&&data.bossDefeated)data.returned=true;
                EnterZone(target.destination,previous);Save(false);return;
            }
            message="Nothing nearby. E collects loot or uses a gateway.";
        }
        public void EquipSelected()
        {
            if(data.bag.Count==0) return;
            selection=Mathf.Clamp(selection,0,data.bag.Count-1);var gear=data.bag[selection];
            var previous=gear.slot==0?data.weapon:data.armor;
            if(gear.slot==0)data.weapon=gear;else data.armor=gear;
            data.bag[selection]=previous;health=Mathf.Min(health,data.MaxHealth);message="Equipped "+gear.DisplayName;InventoryRevision++;feedback.Play(CombatSound.Loot);Save(false);
        }
        public void DiscardSelected()
        {
            if(data.bag.Count==0)return;
            selection=Mathf.Clamp(selection,0,data.bag.Count-1);var gear=data.bag[selection];data.bag.RemoveAt(selection);
            AddDrop(Position,gear);selection=Mathf.Clamp(selection,0,Mathf.Max(0,data.bag.Count-1));message="Dropped "+gear.DisplayName;InventoryRevision++;Save(false);
        }
        public void SellSelected()
        {
            if(zone!=0){message="出售与强化只能在营地行囊中操作。";return;}
            if(!inventory||help||paused||data.bag.Count==0)return;
            selection=Mathf.Clamp(selection,0,data.bag.Count-1);var gear=data.bag[selection];
            data.gold+=gear.SellValue;data.bag.RemoveAt(selection);selection=Mathf.Clamp(selection,0,Mathf.Max(0,data.bag.Count-1));
            message=$"已出售 {gear.DisplayName}，获得 {gear.SellValue} 金币。";InventoryRevision++;feedback.Play(CombatSound.Loot);Save(false);
        }
        public void UpgradeSelected()
        {
            if(zone!=0){message="出售与强化只能在营地行囊中操作。";return;}
            if(!inventory||help||paused||data.bag.Count==0)return;
            selection=Mathf.Clamp(selection,0,data.bag.Count-1);var gear=data.bag[selection];
            if(gear.upgradeLevel>=3){message="这件装备已达到强化上限。";return;}
            if(data.gold<gear.UpgradeCost){message=$"强化需要 {gear.UpgradeCost} 金币。";return;}
            data.gold-=gear.UpgradeCost;gear.upgradeLevel++;gear.power+=2;gear.vitality+=2;
            message=$"强化完成：{gear.DisplayName}。";InventoryRevision++;feedback.Play(CombatSound.Heal);Save(false);
        }
        public void Save(bool notify=true)
        {
            try{AdventureSave.Write(data);if(notify)message="Saved. Continue resumes safely at camp.";}
            catch(Exception ex){message="Save failed: "+ex.Message;Debug.LogError(message);}
        }
        void OnApplicationQuit(){ if(data!=null)Save(false); }
        public string Context()
        {
            var target=ResolveInteraction();
            if(target.kind==InteractionKind.Loot)return "[E] "+target.drop.gear.Description;
            if(target.kind==InteractionKind.Fire)return "[E] REST / refill potions";
            if(target.kind==InteractionKind.Exit)return "[E] 前往"+AdventureRules.Zones[target.destination];
            return "";
        }
    }
}
