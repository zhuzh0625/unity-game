using UnityEngine;
namespace CloudSprout
{
    public sealed class PatrolEnemy : MonoBehaviour
    {
        public float left,right,speed=1.45f;
        public Transform visual;
        int direction=1;bool dead;
        Quaternion rest;
        void Awake(){rest=visual.localRotation;}
        public bool Dead=>dead;
        public string LastContact {get;private set;}="none";
        void Update()
        {
            if(dead||GameSession.Instance.State!=GameState.Playing)return;
            var p=transform.position;p.x+=speed*direction*Time.deltaTime;
            if(p.x>right){p.x=right;direction=-1;}if(p.x<left){p.x=left;direction=1;}transform.position=p;
            visual.localRotation=Quaternion.Euler(0,-direction*50,Mathf.Sin(Time.time*9)*3)*rest;
        }
        void OnTriggerEnter(Collider other){Contact(other);}
        void OnTriggerStay(Collider other){Contact(other);}
        void Contact(Collider other)
        {
            var p=other.GetComponent<SproutPlayer>();if(!p||dead||GameSession.Instance.State!=GameState.Playing)return;
            if(LastContact=="none")LastContact="feet="+p.transform.position.y.ToString("F2")+" previous="+p.PreviousFeetY.ToString("F2")+" enemy="+transform.position.y.ToString("F2")+" vy="+p.VerticalSpeed.ToString("F2");
            // Trigger callbacks arrive on the physics tick, after the controller has moved.
            // Use the previous feet sample and a forgiving upper-body threshold.
            if(p.VerticalSpeed < -1 && Mathf.Max(p.transform.position.y,p.PreviousFeetY)>transform.position.y+.25f)
            {
                dead=true;p.Bounce();GameSession.Instance.Stomps++;GameSession.Instance.Sound("land",.6f);
                visual.localScale=new Vector3(1.25f,.18f,1.1f);GetComponent<Collider>().enabled=false;Destroy(gameObject,.4f);
            }
            else p.Hurt();
        }
    }
}
