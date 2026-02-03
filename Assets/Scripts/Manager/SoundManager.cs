using UnityEngine;

public class SoundManager : MonoBehaviour
{
    public static SoundManager Instance { get; private set; }

    [Header("Audio Source")]
    [SerializeField] AudioSource musicSource;
    [SerializeField] AudioSource SFXSource;

    [Header("Audio Clip")]
    public AudioClip mainBgm;
    public AudioClip tutorialBgm;
    public AudioClip btnClick;
    public AudioClip gunPickup;
    public AudioClip Rifle;
    public AudioClip Bazooka;
    public AudioClip flameThrower;
    public AudioClip explosion;
    public AudioClip reload;
    public AudioClip zombieChase;
    public AudioClip generateOn;
    public AudioClip elevatorAmbience;
    public AudioClip elevatorDing;
    public AudioClip footStep;
    public AudioClip gunHit;

    private void Awake()
    {
        if (Instance == null) { Instance = this; DontDestroyOnLoad(gameObject); }
        else { Destroy(gameObject); return; }
    }
    private void Start()
    {
        musicSource.clip=tutorialBgm;
        musicSource.Play();
    }

    public void PlaySFX(AudioClip clip)
    {
        SFXSource.PlayOneShot(clip);
    }
}
