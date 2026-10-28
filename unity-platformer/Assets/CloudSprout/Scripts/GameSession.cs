using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace CloudSprout
{
    public enum GameState { Menu, Playing, Paused, Dying, Won }
    public sealed class GameSession : MonoBehaviour
    {
        public static GameSession Instance { get; private set; }
        public SproutPlayer player;
        public GameHud hud;
        public AudioClip jump, coin, hurt, checkpoint, win, land, music;
        public GameState State { get; private set; }=GameState.Menu;
        public int Coins { get; private set; }
        public int Seeds { get; private set; }
        public int Health { get; private set; }=3;
        public int Deaths { get; private set; }
        public int Stomps { get; set; }
        public int BlocksHit { get; set; }
        public int Checkpoints { get; private set; }
        public int TotalCoins;
        public float Elapsed { get; private set; }
        public Vector3 Spawn { get; private set; }=new Vector3(2,.2f,0);
        AudioSource sfx, song;
        float toastUntil;
        string toast="";
        bool muted;
        public string Zone => player.transform.position.x<92?"01  /  MEADOW TRAIL":player.transform.position.x<190?"02  /  WINDY GORGE":"03  /  SUNLIT RUINS";
        void Awake()
        {
            Instance=this; Time.timeScale=1; Application.runInBackground=true;
            sfx=gameObject.AddComponent<AudioSource>();sfx.spatialBlend=0;
            song=gameObject.AddComponent<AudioSource>();song.clip=music;song.loop=true;song.volume=.16f;song.Play();
        }
        void Start(){ hud.Refresh(); }
        void Update()
        {
            var k=Keyboard.current;
            if(k!=null)
            {
                if(k.enterKey.wasPressedThisFrame && State==GameState.Menu)Begin();
                if(k.escapeKey.wasPressedThisFrame && (State==GameState.Playing||State==GameState.Paused))TogglePause();
                if(k.mKey.wasPressedThisFrame)ToggleMute();
                if(k.rKey.wasPressedThisFrame && (State==GameState.Won||State==GameState.Paused))Restart();
            }
            if(State==GameState.Playing)Elapsed+=Time.deltaTime;
            hud.Refresh();
            hud.ShowToast(Time.unscaledTime<toastUntil?toast:"");
        }
        public void Begin(){ State=GameState.Playing;Time.timeScale=1;Message("Find the sun gate. Follow the golden trail!",4); }
        public void TogglePause(){ if(State==GameState.Playing){State=GameState.Paused;Time.timeScale=0;}else if(State==GameState.Paused){State=GameState.Playing;Time.timeScale=1;} }
        public void ToggleMute(){muted=!muted;song.mute=muted;sfx.mute=muted;Message(muted?"Sound off  /  M to unmute":"Sound on",2);}
        public void Restart(){Time.timeScale=1;SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);}
        public void Quit(){
            #if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying=false;
            #else
            Application.Quit();
            #endif
        }
        public void Collect(bool seed,Vector3 position)
        {
            if(State!=GameState.Playing)return;
            if(seed){Seeds++;Message("SUN SEED FOUND  "+Seeds+" / 3",3);}else Coins++;
            Sound("coin",seed?.7f:.4f);Burst(position,seed?new Color(1,.8f,.25f):new Color(1,.63f,.1f),seed?20:7);
        }
        public void CollectBlockReward(Vector3 position)
        {
            if(State!=GameState.Playing)return;
            Coins+=5;
            Sound("coin",.65f);Burst(position,new Color(1,.8f,.2f),18);
            Message("+5 COINS",2.4f);
            hud.Refresh();
        }
        public void Damage(){Health--;Sound("hurt",.65f);if(Health<=0)Die();else{Message("Ouch! Keep going — find a checkpoint.",2);Burst(player.transform.position+Vector3.up,Color.white,10);}}
        public void Die(){if(State!=GameState.Playing)return;State=GameState.Dying;Deaths++;Sound("hurt",.6f);StartCoroutine(RespawnRoutine());}
        IEnumerator RespawnRoutine(){yield return new WaitForSeconds(.5f);Health=3;player.Respawn(Spawn);State=GameState.Playing;Message("Back on the trail!",2);}
        public void SetCheckpoint(Vector3 position)
        {
            if(position.x<=Spawn.x)return;
            Spawn=position;Checkpoints++;Health=3;Sound("checkpoint",.7f);Message("CHECKPOINT  /  Hearts restored",3);Burst(position+Vector3.up, new Color(.4f,1,.6f),25);
        }
        public void Finish()
        {
            if(State!=GameState.Playing)return;
            State=GameState.Won;Sound("win",.8f);Burst(player.transform.position+Vector3.up*2,new Color(1,.8f,.2f),60);
            float best=PlayerPrefs.GetFloat("CloudSprout.Best",0);if(best==0||Elapsed<best){PlayerPrefs.SetFloat("CloudSprout.Best",Elapsed);PlayerPrefs.Save();}
        }
        public void Message(string value,float duration=3){toast=value;toastUntil=Time.unscaledTime+duration;}
        public void Sound(string name,float volume=1)
        {
            var clip=name=="jump"?jump:name=="coin"?coin:name=="hurt"?hurt:name=="checkpoint"?checkpoint:name=="win"?win:land;
            if(clip)sfx.PlayOneShot(clip,volume);
        }
        public void Burst(Vector3 position,Color color,int amount)
        {
            var go=new GameObject("Sparkles");go.transform.position=position;
            var ps=go.AddComponent<ParticleSystem>();ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);var main=ps.main;main.loop=false;main.duration=.7f;main.startLifetime=.5f;main.startSpeed=3;main.startSize=.10f;main.startColor=color;main.gravityModifier=.6f;main.simulationSpace=ParticleSystemSimulationSpace.World;
            var emission=ps.emission;emission.rateOverTime=0;emission.SetBursts(new[]{new ParticleSystem.Burst(0,(short)amount)});
            var render=ps.GetComponent<ParticleSystemRenderer>();render.sharedMaterial=hud.sparkMaterial;ps.Play();Destroy(go,1.5f);
        }
    }
}
