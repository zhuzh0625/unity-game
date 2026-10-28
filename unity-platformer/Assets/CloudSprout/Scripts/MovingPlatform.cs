using UnityEngine;
namespace CloudSprout
{
    [DefaultExecutionOrder(-50)]
    public sealed class MovingPlatform : MonoBehaviour
    {
        public Vector3 travel = new Vector3(4,0,0);
        public float period=4,phase;
        Vector3 origin;
        void Awake(){origin=transform.position;}
        void Update(){if(GameSession.Instance.State==GameState.Playing)transform.position=origin+travel*(.5f-.5f*Mathf.Cos((Time.time/period+phase)*Mathf.PI*2));}
    }
}
