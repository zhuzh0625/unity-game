using System.Collections;
using UnityEngine;
namespace CloudSprout
{
    public sealed class RewardBlock:MonoBehaviour
    {
        public Transform visual;
        public GameObject rewardCoin;
        public bool Used {get;private set;}
        float bump;
        public void Hit()
        {
            var game=GameSession.Instance;
            if(Used||!game||game.State!=GameState.Playing)return;
            Used=true;bump=.18f;game.BlocksHit++;
            game.CollectBlockReward(transform.position+new Vector3(0,.8f,-.7f));
            foreach(var r in visual.GetComponentsInChildren<Renderer>())r.material.color=new Color(.42f,.31f,.19f);
            if(rewardCoin)StartCoroutine(PopCoins());
        }
        IEnumerator PopCoins()
        {
            // These are reward feedback, not collectible items: the five coins are credited once on impact.
            var group=new GameObject("Reward Coins");
            var coins=new Transform[5];
            var rest=new Quaternion[5];
            for(int i=0;i<coins.Length;i++)
            {
                coins[i]=Instantiate(rewardCoin,group.transform).transform;
                rest[i]=coins[i].localRotation;
                coins[i].gameObject.SetActive(false);
            }
            const float lifetime=1.1f,delay=.1f;
            float elapsed=0;
            while(elapsed<lifetime+delay*4)
            {
                for(int i=0;i<coins.Length;i++)
                {
                    float age=elapsed-delay*i;
                    bool visible=age>=0&&age<lifetime;
                    coins[i].gameObject.SetActive(visible);
                    if(!visible)continue;
                    float t=age/lifetime;
                    coins[i].position=transform.position+new Vector3((i-2)*.18f*t,.62f+2.7f*t-1.6f*t*t,-.8f);
                    coins[i].localRotation=Quaternion.Euler(0,age*540,0)*rest[i];
                    coins[i].localScale=Vector3.one*(t<.8f?1:Mathf.Max(0,(1-t)/.2f));
                }
                elapsed+=Time.deltaTime;
                yield return null;
            }
            Destroy(group);
        }
        void Update(){bump=Mathf.MoveTowards(bump,0,Time.deltaTime);visual.localPosition=Vector3.up*bump;}
    }
}
