using UnityEngine;

public class Phase1Music : MonoBehaviour
{
    [SerializeField] AudioClip soundtrack;
    [SerializeField, Range(0f, 1f)] float volume = 0.6f;

    void Awake()
    {
        if (soundtrack == null)
            return;

        AudioSource source = gameObject.AddComponent<AudioSource>();
        source.clip = soundtrack;
        source.loop = true;
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        source.volume = volume;
        source.Play();
    }
}
