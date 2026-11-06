using UnityEngine;

namespace Emberfall
{
    public partial class Adventure
    {
        int pendingAttack = -1;
        int queuedAttack = -1;
        float queueLife;
        float attackWindup, dodgeCooldown, dodgeTime, hitStop;
        Vector2 attackAim, dodgeDirection;

        public bool CanSee(Vector2 from, Vector2 to) => navigation.Visible(from, to);
        Foe FindTarget(float range)
        {
            Foe best = null; float score = float.MaxValue;
            foreach (var f in enemies)
            {
                if (!f.go) continue;
                Vector2 to = (Vector2)f.go.transform.position - Position;
                float distance = to.magnitude;
                if (distance > range || !CanSee(Position, f.go.transform.position)) continue;
                float value = distance + (1 - Vector2.Dot(facing, to.normalized)) * .35f - (f == CurrentTarget ? .45f : 0);
                if (value < score) { score = value; best = f; }
            }
            return best;
        }
        public bool Dodge(Vector2 direction)
        {
            if (Modal || dodgeCooldown > 0) return false;
            dodgeDirection = direction.sqrMagnitude > .01f ? direction.normalized : facing;
            dodgeTime = AdventureRules.DodgeDuration; dodgeCooldown = AdventureRules.DodgeCooldown;
            invulnerable = Mathf.Max(invulnerable, dodgeTime); pendingAttack = -1; queuedAttack = -1;
            playerVisual.Dodge(dodgeDirection); feedback.Play(CombatSound.Dodge);
            return true;
        }
        void TickEnemies(float dt)
        {
            // No membership changes here; attacks remove enemies in a separate phase.
            for (int i = 0; i < enemies.Count; i++)
            {
                var f = enemies[i]; if (!f.go) continue;
                Vector2 p = f.go.transform.position, to = Position - p;
                float distance = to.magnitude;
                f.bar.gameObject.SetActive(distance < 12 && (f.hp < f.max || f == CurrentTarget || f.kind == 3));
                f.fill.localScale = new Vector3(1.2f * Mathf.Clamp01(f.hp / f.max), .07f, 1);
                f.fill.localPosition = new Vector3(-.6f * (1 - Mathf.Clamp01(f.hp / f.max)), 0, 0);
                f.marker.SetActive(f == CurrentTarget);
                f.attack -= dt; f.slow -= dt;
                if (f.kind == 3 && !f.enraged && f.hp <= f.max * .5f)
                {
                    f.enraged = true; message = "守墓者进入狂怒！预警更快，冲击后会释放六道余烬。";
                    feedback.Play(CombatSound.Boss); f.visual.Enrage();
                }
                if (f.windup > 0)
                {
                    f.windup -= dt;
                    if (f.windup <= 0)
                    {
                        f.visual.ReleaseAttack();f.recovery=ActorVisual.RecoveryDuration(f.kind);
                        if (f.kind == 3)
                        {
                            if (Vector2.Distance(Position, f.target) < 2.6f && CanSee(p, Position)) Hurt((f.enraged ? 34 : 30) * (1 + data.ExpeditionRank * .08f));
                            Flash(f.target, 5, new Color(1, .25f, .1f, .5f));
                            if (f.enraged) for (int j = 0; j < 6; j++)
                            {
                                float angle = j * Mathf.PI / 3;
                                Launch(p, new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 4, true, 12, ProjectileKind.Warden);
                            }
                            feedback.Play(CombatSound.Boss, .45f);
                        }
                        else if (f.kind == 1 || f.kind == 2)
                        {
                            Launch(p, (f.target - p).normalized * (f.kind == 1 ? 7 : 4.5f), true, f.kind == 1 ? 10 : 15,
                                f.kind == 1 ? ProjectileKind.Arrow : ProjectileKind.Hex);
                        }
                        else
                        {
                            feedback.EnemySwing(p,(f.target-p).normalized);
                            if (distance < 1.65f && CanSee(p, Position)) Hurt(10 * (1 + data.ExpeditionRank * .08f));
                        }
                        if (f.telegraph) { feedback.Release(f.telegraph); f.telegraph = null; }
                    }
                    continue;
                }
                if(f.recovery>0){f.recovery-=dt;continue;}
                if (distance > 14) continue;
                bool ranged = f.kind == 1 || f.kind == 2, visible = CanSee(p, Position);
                float speed = (f.kind == 3 ? (f.enraged ? 2.3f : 1.9f) : f.kind == 0 ? 2.8f : 1.65f) * (f.slow > 0 ? .3f : 1);
                f.visual.Face(to);
                if (!visible || distance > (ranged ? 5 : 1.15f)) Move(f.go.transform, navigation.Direction(p, Position) * speed * dt);
                else if (ranged && distance < 3) Move(f.go.transform, -to.normalized * speed * dt);
                if (f.attack > 0 || !visible) continue;
                if (f.kind == 3 && distance < 7)
                {
                    f.target = Position; f.windup = f.enraged ? .72f : .95f; f.attack = f.enraged ? 2.1f : 3;
                    f.telegraph = feedback.Take(WorldArt.Ring, f.target, Vector2.one * 5, new Color(1, .16f, .08f, .7f), 4);
                    f.visual.Strike(to, f.windup); feedback.Play(CombatSound.Warning, .45f);
                }
                else if ((ranged && distance <= 12) || distance < 1.5f)
                {
                    f.target = Position; f.windup = ranged ? .35f : .24f;
                    f.attack = ranged ? (f.kind == 1 ? 1.6f : 2.4f) : 1;
                    f.visual.Strike(to, f.windup);
                }
            }
        }
        public bool UseAttack(int type)
        {
            if (type < 0 || type >= 4 || Modal || IsDodging || pendingAttack >= 0 || cooldown[type] > 0) return false;
            int cost = AdventureRules.Cost[type];
            if (mana < cost) { message = "Not enough focus."; return false; }
            mana -= cost;
            cooldown[type] = type == 0 ? data.AttackInterval : AdventureRules.Cooldown[type] * data.SkillCooldownFactor;
            CurrentTarget = FindTarget(type == 3 ? 10 : Mathf.Max(4, AdventureRules.Range[type]));
            attackAim = CurrentTarget != null ? ((Vector2)CurrentTarget.go.transform.position - Position).normalized : facing;
            facing = attackAim; pendingAttack = type; attackWindup = AdventureRules.Windup[type];
            playerVisual.Strike(attackAim, attackWindup + .14f);
            feedback.Play(type <= 1 ? CombatSound.Swing : CombatSound.Cast);
            return true;
        }
        void RequestAttack(int type)
        {
            if (UseAttack(type)) { queuedAttack = -1; return; }
            if (!Modal && !IsDodging && pendingAttack >= 0 && cooldown[type] <= 0 && mana >= AdventureRules.Cost[type])
            { queuedAttack = type; queueLife = .25f; }
        }
        void TickBufferedAttack(float dt)
        {
            if (queuedAttack < 0) return;
            queueLife -= dt;
            if (queueLife <= 0) { queuedAttack = -1; return; }
            if (pendingAttack < 0 && UseAttack(queuedAttack)) queuedAttack = -1;
        }
        void TickAttack(float dt)
        {
            if (pendingAttack < 0) return;
            attackWindup -= dt; if (attackWindup > 0) return;
            int type = pendingAttack; pendingAttack = -1;
            if (type == 3) { Launch(Position, attackAim * 11, false, data.Attack * 1.8f, ProjectileKind.Ember); return; }
            float radius = AdventureRules.Range[type];
            var effect = feedback.Take(type == 0 ? WorldArt.Slash : WorldArt.Ring, Position, Vector2.one * radius * 2,
                type == 2 ? new Color(.35f, .75f, 1, .7f) : new Color(1, .82f, .4f, .85f), 450);
            effect.transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(attackAim.y, attackAim.x) * Mathf.Rad2Deg);
            pulses.Add(new Pulse { go = effect, life = .2f, max = .2f });
            for (int i = enemies.Count - 1; i >= 0; i--)
            {
                var f = enemies[i]; Vector2 to = (Vector2)f.go.transform.position - Position;
                if (to.sqrMagnitude > radius * radius || (type == 0 && Vector2.Dot(to.normalized, attackAim) < .1f) || !CanSee(Position, f.go.transform.position)) continue;
                if (type == 2) f.slow = 4;
                Damage(f, data.Attack * (type == 1 ? 1.8f : type == 2 ? .9f : 1));
            }
        }
        public void Damage(Foe f, float amount)
        {
            if (f == null || !f.go || !enemies.Contains(f)) return;
            float dealt = Mathf.Min(f.hp, amount); f.visual.Hit();
            feedback.Number(f.go.transform.position, amount, new Color(1, .85f, .45f));
            feedback.Play(CombatSound.Hit, .65f); f.hp -= amount;
            if (data.Leech > 0) health = Mathf.Min(data.MaxHealth, health + dealt * data.Leech);
            Flash(f.go.transform.position, .7f, new Color(1, 1, 1, .65f)); hitStop = .025f;
            if (f.hp > 0) { if (f.kind != 3) Move(f.go.transform, ((Vector2)f.go.transform.position - Position).normalized * .14f); return; }
            Vector2 p = f.go.transform.position;
            if (f.telegraph) feedback.Release(f.telegraph);
            if (CurrentTarget == f) CurrentTarget = null;
            f.visual.Die(); Destroy(f.go, .28f); enemies.Remove(f); kills++; data.gold += f.kind == 3 ? 50 : 5;
            if (f.kind == 3)
            {
                data.bossDefeated = true; data.victories++;
                message = "WARDEN DEFEATED / Collect the relic [E], then return to camp through the western gate.";
                Marker(new Vector2(14, 0), new Color(.9f, .68f, .3f)); Save(false);
            }
            if (kills == 1 || f.kind == 3 || UnityEngine.Random.value < .8f) AddDrop(p, Gear.Roll(f.kind == 3 ? 2 : 0));
        }
        public void AddDrop(Vector2 p, Gear gear)
        {
            var go = WorldArt.Shape(gear.name, world, p, Vector2.one * .65f, GearColor(gear), 7);
            go.GetComponent<SpriteRenderer>().sprite = WorldArt.GearIcon(gear.slot);
            WorldArt.Shape("Glow", go.transform, Vector2.zero, Vector2.one * 1.8f, GearColor(gear) * new Color(1, 1, 1, .45f), 5, true);
            drops.Add(new Drop { go = go, gear = gear });
        }
        public static Color GearColor(Gear gear) => gear.rarity == 2 ? new Color(1, .7f, .23f) : gear.rarity == 1 ? new Color(.35f, .7f, 1) : new Color(.8f, .8f, .72f);
        void Launch(Vector2 p, Vector2 velocity, bool hostile, float damage, ProjectileKind kind)
        {
            var visual=feedback.TakeProjectile(kind,p,velocity);feedback.Muzzle(p,kind,velocity.normalized);
            missiles.Add(new Missile { go = visual.gameObject, visual=visual, velocity = velocity, hostile = hostile, damage = damage, life = 3 });
        }
        static float SegmentHit(Vector2 from, Vector2 to, Vector2 center, float radius)
        {
            Vector2 delta = to - from, offset = from - center;
            float c = offset.sqrMagnitude - radius * radius;
            if (c <= 0) return 0;
            float a = delta.sqrMagnitude, b = Vector2.Dot(offset, delta), discriminant = b * b - a * c;
            if (a < .000001f || discriminant < 0) return 2;
            float t = (-b - Mathf.Sqrt(discriminant)) / a;
            return t >= 0 && t <= 1 ? t : 2;
        }
        void TickMissiles(float dt)
        {
            for (int i = missiles.Count - 1; i >= 0; i--)
            {
                var m = missiles[i]; m.life -= dt;
                Vector2 from = m.go.transform.position, to = from + m.velocity * dt;
                float wall = navigation.FirstWall(from, to, .08f), first = wall;
                Foe victim = null; bool hitPlayer = false;
                if (m.hostile)
                {
                    float t = SegmentHit(from, to, Position, .5f);
                    if (t < first) { first = t; hitPlayer = true; }
                }
                else for (int j = 0; j < enemies.Count; j++)
                {
                    var f = enemies[j]; float t = SegmentHit(from, to, f.go.transform.position, f.kind == 3 ? .85f : .55f);
                    if (t < first) { first = t; victim = f; }
                }
                m.go.transform.position = Vector2.Lerp(from, to, Mathf.Clamp01(first));
                m.visual.Step(dt);
                if (hitPlayer) Hurt(m.damage);
                if (victim != null) Damage(victim, m.damage);
                if(first<=1)feedback.Impact(m.go.transform.position,m.visual.Kind,m.velocity.normalized);
                if (first <= 1 || Mathf.Abs(to.x) > 21 || Mathf.Abs(to.y) > 11) m.life = 0;
                if (m.life <= 0) { feedback.ReleaseProjectile(m.visual); missiles.RemoveAt(i); }
                else missiles[i] = m;
            }
        }
        void Flash(Vector2 p, float size, Color color)
        {
            pulses.Add(new Pulse { go = feedback.Take(WorldArt.Ring, p, Vector2.one * size, color, 450), life = .22f, max = .22f });
        }
        void TickPulses(float dt)
        {
            for (int i = pulses.Count - 1; i >= 0; i--)
            {
                var p = pulses[i]; p.life -= dt;
                if (p.life <= 0) { feedback.Release(p.go); pulses.RemoveAt(i); }
                else { feedback.Fade(p.go, p.life / p.max); pulses[i] = p; }
            }
        }
        public void Hurt(float amount)
        {
            if (invulnerable > 0 || dead || zone == 0) return;
            float loss = Mathf.Max(1, amount - data.armor.power * .45f);
            playerVisual.Hit(); feedback.Number(Position, loss, new Color(1, .35f, .25f)); feedback.Play(CombatSound.Hurt);
            health -= loss; invulnerable = .35f;
            if (health <= 0) { health = 0; dead = true; pendingAttack = -1; playerVisual.Die(); message = "You fell. Enter to return to camp (lose 10 gold)."; }
        }
        public void Respawn()
        {
            dead = false; data.gold = Mathf.Max(0, data.gold - 10); health = data.MaxHealth; mana = 100;
            for (int i = 0; i < cooldown.Length; i++) cooldown[i] = 0;
            dodgeCooldown = 0; invulnerable = 0; EnterZone(0); Save();
        }
        public void Drink()
        {
            if (Modal || potionCooldown > 0 || health >= data.MaxHealth || data.potions == 0) return;
            health = Mathf.Min(data.MaxHealth, health + 55); data.potions--; potionCooldown = 1;
            message = "Restored 55 health."; feedback.Play(CombatSound.Heal);
        }
    }
}
