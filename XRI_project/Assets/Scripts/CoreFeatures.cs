using UnityEngine;

public enum FeatureUsage
{
    Once,
    Toggle
}

public class CoreFeatures : MonoBehaviour
{
    public bool AudioSFXSourceCreated { get; set; }

    [field: Header("CoreAudio")]
    //Audio plays when something opens
    [field: SerializeField] public AudioClip AudioClipOnStart { get; set; }

    //Audio plays on exit
    [field: SerializeField] public AudioClip AudioClipOnEnd { get; set; }

    private AudioSource audioSource;

    public FeatureUsage featureUsage = FeatureUsage.Once;

    protected virtual void Awake()
    {
        MakeSFXAudioSource();
    }

    private void MakeSFXAudioSource()
    {
        audioSource = GetComponent<AudioSource>();

        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }

        AudioSFXSourceCreated = true;
    }

    protected void PlayOnStart()
    {
        if (AudioSFXSourceCreated && AudioClipOnStart != null)
        {
            audioSource.clip = AudioClipOnStart;
            audioSource.Play();
        }
    }

    protected void PlayOnEnd()
    {
        if (AudioSFXSourceCreated && AudioClipOnEnd != null)
        {
            audioSource.clip = AudioClipOnEnd;
            audioSource.Play();
        }
    }
}