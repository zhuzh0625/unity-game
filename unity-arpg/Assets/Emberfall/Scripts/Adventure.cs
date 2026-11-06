using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Emberfall
{
    public class Adventure : MonoBehaviour
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
        public class Foe { public GameObject go; public int kind; public float hp, max, attack, slow, windup; public Vector2 target; public GameObject telegraph; }
        public class Drop { public GameObject go; public Gear gear; }
        class Missile { public GameObject go; public Vector2 velocity; public float life; public bool hostile; public float damage; }
        class Pulse { public GameObject go; public float life, max; }

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
            view = Camera.main;
            if (!view) view = new GameObject("Main Camera", typeof(Camera)).GetComponent<Camera>();
            view.tag = "MainCamera"; view.orthographic = true; view.orthographicSize = 7;
            // URP 2D leaves stale pixels outside a partial viewport on this driver.
            // Clear the full target; opaque HUD bands cover the reserved screen edges.
            view.rect=new Rect(0,0,1,1);
            view.clearFlags = CameraClearFlags.SolidColor; view.backgroundColor = new Color(.025f,.035f,.045f);
            hud = gameObject.AddComponent<AdventureHud>(); hud.game = this;
            EnterZone(0);
        }
        static bool Press(Key key) => Keyboard.current != null && Keyboard.current[key].wasPressedThisFrame;
        static bool Hold(Key key) => Keyboard.current != null && Keyboard.current[key].isPressed;
        void Update()
        {
            if(Press(Key.F1)&&!dead){help=!help;if(help)inventory=false;}
            if (Press(Key.Escape)) { if(help)help=false; else if (inventory) inventory = false; else if (!dead) paused = !paused; }
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
                hud.Refresh(); return;
            }
            float dt = Mathf.Min(Time.deltaTime, .05f); timeInZone += dt;
            if(campfire)campfire.localScale=Vector3.one*(2.1f+Mathf.Sin(timeInZone*5)*.08f);
            for (int i = 0; i < cooldown.Length; i++) cooldown[i] = Mathf.Max(0, cooldown[i] - dt);
            invulnerable -= dt; potionCooldown -= dt; mana = Mathf.Min(100, mana + dt * 9);
            Vector2 input = new Vector2((Hold(Key.D) || Hold(Key.RightArrow) ? 1 : 0) - (Hold(Key.A) || Hold(Key.LeftArrow) ? 1 : 0),
                (Hold(Key.W) || Hold(Key.UpArrow) ? 1 : 0) - (Hold(Key.S) || Hold(Key.DownArrow) ? 1 : 0));
            if (input.sqrMagnitude > 0) { facing = input.normalized; Move(player, facing * (Hold(Key.LeftShift) ? 7 : 4.8f) * dt); }
            player.Find("Facing").localPosition=facing*.7f;
            if (Hold(Key.J) || Hold(Key.Space) || (Mouse.current != null && Mouse.current.leftButton.isPressed && !UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())) UseAttack(0);
            if (Press(Key.U)) UseAttack(1);
            if (Press(Key.I)) UseAttack(2);
            if (Press(Key.O)) UseAttack(3);
            if (Press(Key.H)) Drink();
            if (Press(Key.E)) Interact();
            if (Press(Key.F5)) Save();
            TickEnemies(dt); TickMissiles(dt); TickPulses(dt);
            FollowCamera();
            hud.Refresh();
        }
        public void Move(Transform actor, Vector2 delta)
        {
            Vector2 p = actor.position;
            var x = new Vector2(Mathf.Clamp(p.x + delta.x, -21, 21), p.y);
            if (Walkable(x)) p = x;
            var y = new Vector2(p.x, Mathf.Clamp(p.y + delta.y, -11, 11));
            if (Walkable(y)) p = y;
            actor.position = p;
        }
        bool Walkable(Vector2 p)
        {
            foreach (var r in walls) if (new Rect(r.xMin - .35f, r.yMin - .4f, r.width + .7f, r.height + .8f).Contains(p)) return false;
            return true;
        }
        public void EnterZone(int target)
        {
            zone = target; timeInZone = 0;
            if (world) Destroy(world.gameObject);
            world = new GameObject("World").transform;
            walls.Clear(); enemies.Clear(); drops.Clear(); missiles.Clear(); pulses.Clear();
            var random = UnityEngine.Random.state; UnityEngine.Random.InitState(400 + zone);
            Color ground = zone == 2 ? new Color(.12f,.14f,.18f) : zone == 0 ? new Color(.17f,.20f,.18f) : new Color(.16f,.21f,.19f);
            for (int x = -22; x < 22; x++) for (int y = -12; y < 12; y++)
            {
                UnityEngine.Random.Range(.82f,1.18f); // Preserve the existing seeded obstacle layout.
                WorldArt.Ground(world,new Vector2(x+.5f,y+.5f),zone);
            }
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
            player = WorldArt.Actor("Wanderer",world,zone == 0 ? new Vector2(-1,0) : new Vector2(-17,0),new Color(.32f,.66f,.73f)).transform;
            WorldArt.Shape("Facing",player,new Vector2(.7f,0),Vector2.one*.13f,new Color(1,.85f,.4f),13,true);
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
            var f = new Foe {kind=kind,max=kind==3?360:kind==1?58:40,attack=.7f}; f.hp=f.max;
            f.go=WorldArt.Actor(new[]{"Ravager","Bone Archer","Mire Hexer","The Warden"}[kind],world,position,color,kind==3?1.7f:1);
            enemies.Add(f); return f;
        }
        void TickEnemies(float dt)
        {
            foreach (var f in enemies.ToArray())
            {
                if (!f.go) continue;
                Vector2 p=f.go.transform.position, to=Position-p; float distance=to.magnitude;
                f.attack-=dt; f.slow-=dt;
                if (f.windup>0)
                {
                    f.windup-=dt;
                    if (f.windup<=0)
                    {
                        if (Vector2.Distance(Position,f.target)<2.6f) Hurt(30);
                        if(f.telegraph) Destroy(f.telegraph);
                        Flash(f.target,5,new Color(1,.25f,.1f,.5f));
                    }
                    continue;
                }
                if(distance>12) continue;
                float speed=f.kind==3?1.9f:f.kind==0?2.8f:1.65f; if(f.slow>0) speed*=.3f;
                if(distance>(f.kind==1||f.kind==2?5:1.15f)) Move(f.go.transform,to.normalized*speed*dt);
                else if((f.kind==1||f.kind==2)&&distance<3) Move(f.go.transform,-to.normalized*speed*dt);
                if(f.attack>0) continue;
                if(f.kind==3 && distance<7)
                {
                    f.target=Position; f.windup=.95f; f.attack=3;
                    f.telegraph=WorldArt.Shape("Warden warning",world,f.target,Vector2.one*5,new Color(.9f,.15f,.12f,.4f),4,true);
                }
                else if(f.kind==1||f.kind==2)
                {
                    Launch(p,to.normalized*(f.kind==1?7:4.5f),true,f.kind==1?10:15,f.kind==1?new Color(.9f,.75f,.4f):new Color(.7f,.3f,.9f));
                    f.attack=f.kind==1?1.6f:2.4f;
                }
                else if(distance<1.5f) { Hurt(10); f.attack=1; Flash(Position,.8f,new Color(.9f,.2f,.1f,.6f)); }
            }
        }
        public bool UseAttack(int type)
        {
            if(Modal||cooldown[type]>0) return false;
            int cost=new[]{0,22,28,18}[type]; if(mana<cost) { message="Not enough focus."; return false; }
            mana-=cost; cooldown[type]=new[]{.38f,3.5f,6f,2f}[type];
            Vector2 aim=facing;
            Foe nearest=null; float best=type==3?10:4;
            foreach(var f in enemies) { float d=Vector2.Distance(Position,f.go.transform.position); if(d<best) {best=d;nearest=f;} }
            if(nearest!=null) aim=((Vector2)nearest.go.transform.position-Position).normalized;
            facing=aim;
            if(type==3) { Launch(Position,aim*11,false,data.Attack*1.8f,new Color(1,.6f,.15f)); return true; }
            float radius=type==2?4.5f:type==1?3.2f:1.9f;
            Flash(type==0?Position+aim*.8f:Position,radius*2,type==2?new Color(.35f,.75f,1,.45f):new Color(1,.75f,.3f,.45f));
            foreach(var f in enemies.ToArray())
            {
                Vector2 to=(Vector2)f.go.transform.position-Position;
                if(to.magnitude>radius || (type==0 && Vector2.Dot(to.normalized,aim)<.1f)) continue;
                if(type==2) f.slow=4;
                Damage(f,data.Attack*(type==1?1.8f:type==2?.9f:1));
            }
            return true;
        }
        public void Damage(Foe f,float amount)
        {
            f.go.GetComponent<ActorVisual>().Hit();
            DamageNumber.Show(world,f.go.transform.position,amount,new Color(1,.8f,.42f));
            f.hp-=amount; Flash(f.go.transform.position,.7f,new Color(1,1,1,.65f));
            if(f.hp>0) return;
            Vector2 p=f.go.transform.position;
            if(f.telegraph) Destroy(f.telegraph);
            Destroy(f.go); enemies.Remove(f); kills++; data.gold+=f.kind==3?50:5;
            if(f.kind==3) { data.bossDefeated=true; data.victories++; message="WARDEN DEFEATED / Collect the relic [E], then return to camp through the western gate."; Marker(new Vector2(14,0),new Color(.9f,.68f,.3f)); }
            if(kills==1||f.kind==3||UnityEngine.Random.value<.8f) AddDrop(p,Gear.Roll(f.kind==3?2:0));
        }
        public void AddDrop(Vector2 p,Gear gear)
        {
            var go=WorldArt.Prop(12,gear.name,world,p,Vector2.one*.7f,7);
            WorldArt.Shape("Glow",go.transform,Vector2.zero,Vector2.one*1.8f,GearColor(gear)*new Color(1,1,1,.5f),5,true);
            drops.Add(new Drop {go=go,gear=gear});
        }
        public static Color GearColor(Gear gear) => gear.rarity==2?new Color(1,.7f,.23f):gear.rarity==1?new Color(.35f,.7f,1):new Color(.8f,.8f,.72f);
        void Launch(Vector2 p,Vector2 velocity,bool hostile,float damage,Color color)
        {
            var go=WorldArt.Shape("Projectile",world,p,new Vector2(hostile?.65f:1,.23f),color,400,true);
            go.transform.rotation=Quaternion.Euler(0,0,Mathf.Atan2(velocity.y,velocity.x)*Mathf.Rad2Deg);
            missiles.Add(new Missile {go=go,velocity=velocity,hostile=hostile,damage=damage,life=3});
        }
        void TickMissiles(float dt)
        {
            for(int i=missiles.Count-1;i>=0;i--)
            {
                var m=missiles[i]; m.life-=dt; m.go.transform.position+=(Vector3)(m.velocity*dt); Vector2 p=m.go.transform.position;
                if(!Walkable(p)||Mathf.Abs(p.x)>21||Mathf.Abs(p.y)>11) m.life=0;
                if(m.hostile && Vector2.Distance(p,Position)<.65f) {Hurt(m.damage);m.life=0;}
                if(!m.hostile) foreach(var f in enemies.ToArray()) if(Vector2.Distance(p,f.go.transform.position)<(f.kind==3?1:.65f)) {Damage(f,m.damage);m.life=0;break;}
                if(m.life<=0) {Destroy(m.go);missiles.RemoveAt(i);}
            }
        }
        void Flash(Vector2 p,float size,Color color)
        {
            pulses.Add(new Pulse {go=WorldArt.Shape("Impact",world,p,Vector2.one*size,color,450,true),life=.22f,max=.22f});
        }
        void TickPulses(float dt)
        {
            for(int i=pulses.Count-1;i>=0;i--) {var p=pulses[i];p.life-=dt; if(p.life<=0) {Destroy(p.go);pulses.RemoveAt(i);} else {var sr=p.go.GetComponent<SpriteRenderer>();var c=sr.color;c.a=p.life/p.max*.5f;sr.color=c;} }
        }
        public void Hurt(float amount)
        {
            if(invulnerable>0||dead||zone==0) return;
            player.GetComponent<ActorVisual>().Hit();
            DamageNumber.Show(world,Position,Mathf.Max(1,amount-data.armor.power*.45f),new Color(1,.35f,.25f));
            health-=Mathf.Max(1,amount-data.armor.power*.45f); invulnerable=.35f;
            if(health<=0) {health=0;dead=true;message="You fell. Enter to return to camp (lose 10 gold).";}
        }
        public void Respawn() {dead=false;data.gold=Mathf.Max(0,data.gold-10);health=data.MaxHealth;mana=100;EnterZone(0);Save();}
        public void Drink()
        {
            if(Modal||potionCooldown>0||health>=data.MaxHealth||data.potions==0) return;
            health=Mathf.Min(data.MaxHealth,health+55);data.potions--;potionCooldown=1;message="Restored 55 health.";
        }
        public void Interact()
        {
            if(Modal)return;
            Drop nearest=null;float best=2.4f;
            foreach(var d in drops) {float dist=Vector2.Distance(Position,d.go.transform.position);if(dist<best){best=dist;nearest=d;}}
            if(nearest!=null)
            {
                if(data.bag.Count>=16) {message="Pack full. B to equip or discard gear.";return;}
                data.bag.Add(nearest.gear);message="Collected "+nearest.gear.Description;Destroy(nearest.go);drops.Remove(nearest);Save(false);return;
            }
            if(zone==0)
            {
                if(Vector2.Distance(Position,new Vector2(-4,0))<3) {health=data.MaxHealth;mana=100;data.potions=5;
                    if(data.returned) {data.bossDefeated=false;data.returned=false;} message="Rested. Potions refilled. Ready for the eastern gate.";Save(false);}
                else if(Vector2.Distance(Position,new Vector2(18,0))<3) {EnterZone(1);Save(false);}
                else message="Move near the campfire or eastern gate and press E.";
            }
            else if(Vector2.Distance(Position,new Vector2(-19,0))<3)
            {
                if(zone==1 && data.bossDefeated) data.returned=true;
                EnterZone(zone==1?0:1); Save(false);
            }
            else if(zone==1 && Vector2.Distance(Position,new Vector2(19,0))<3) {EnterZone(2);Save(false);}
            else message="Nothing nearby. E collects loot or uses a gateway.";
        }
        public void EquipSelected()
        {
            if(data.bag.Count==0) return;
            selection=Mathf.Clamp(selection,0,data.bag.Count-1);var gear=data.bag[selection];
            var previous=gear.slot==0?data.weapon:data.armor;
            if(gear.slot==0)data.weapon=gear;else data.armor=gear;
            data.bag[selection]=previous;health=Mathf.Min(health,data.MaxHealth);message="Equipped "+gear.name;Save(false);
        }
        public void DiscardSelected()
        {
            if(data.bag.Count==0)return;
            selection=Mathf.Clamp(selection,0,data.bag.Count-1);var gear=data.bag[selection];data.bag.RemoveAt(selection);
            AddDrop(Position,gear);selection=Mathf.Clamp(selection,0,Mathf.Max(0,data.bag.Count-1));message="Dropped "+gear.name;Save(false);
        }
        public void Save(bool notify=true)
        {
            try{AdventureSave.Write(data);if(notify)message="Saved. Continue resumes safely at camp.";}
            catch(Exception ex){message="Save failed: "+ex.Message;Debug.LogError(message);}
        }
        void OnApplicationQuit(){ if(data!=null)Save(false); }
        public string Context()
        {
            foreach(var d in drops) if(Vector2.Distance(Position,d.go.transform.position)<2.4f)return "[E] "+d.gear.Description;
            if(zone==0 && Vector2.Distance(Position,new Vector2(-4,0))<3)return "[E] REST / refill potions";
            if(zone==0 && Vector2.Distance(Position,new Vector2(18,0))<3)return "[E] 前往灰烬荒野";
            if(zone>0 && Vector2.Distance(Position,new Vector2(-19,0))<3)return zone==1?"[E] 返回余烬营地":"[E] 返回灰烬荒野";
            if(zone==1 && Vector2.Distance(Position,new Vector2(19,0))<3)return "[E] 进入幽寂古墓";
            return "";
        }
    }
}
