using UnityEngine;

namespace IronBastion
{
    public sealed class BattleBlock : MonoBehaviour
    {
        public SurfaceKind Kind;
        public int HitPoints = 1;
        public bool Hit(int power)
        {
            if (Kind == SurfaceKind.Steel && power < 2) { BattleEffects.Spark(transform.position + Vector3.up, false); return false; }
            if (--HitPoints > 0) return false;
            GetComponent<Collider>().enabled = false;
            BattleEffects.Explode(transform.position + Vector3.up * .5f, Kind == SurfaceKind.Base ? 2 : .65f);
            if (Kind == SurfaceKind.Base) BattleGame.Instance?.Finish(false, "指挥部被摧毁");
            Destroy(gameObject);
            return true;
        }
    }
}
