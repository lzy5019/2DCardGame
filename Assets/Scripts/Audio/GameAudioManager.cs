using UnityEngine;

[DisallowMultipleComponent]
public class GameAudioManager : MonoBehaviour
{
    public static GameAudioManager Instance;

    [Header("Audio Sources")]
    [SerializeField] private AudioSource bgmSource;
    [SerializeField] private AudioSource voiceSource;
    [SerializeField] private AudioSource sfxSource;
    [SerializeField] private AudioSource uiSource;

    [Header("Voice Playback")]
    [SerializeField, Range(0f, 1.5f)] private float voiceVolumeScale = 1f;
    [SerializeField] private Vector2 voicePitchRange = Vector2.one;
    [SerializeField, Range(0f, 1f)] private float duplicateSuppressionWindow = 0.12f;

    private string lastVoiceCardId = string.Empty;
    private PublicActionType lastVoiceActionType = PublicActionType.PlayCard;
    private float lastVoicePlayedTime = -10f;
    private int currentVoicePriority = int.MinValue;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        AutoBindSourcesIfNeeded();
        ConfigureSources();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public bool TryPlayCardVoice(string cardId, PublicActionType actionType)
    {
        if (string.IsNullOrEmpty(cardId))
            return false;
        if (CardDatabase.Instance == null)
            return false;

        AudioClip clip = GetCardVoiceClip(cardId, actionType);
        if (clip == null)
            return false;

        EnsureVoiceSource();
        if (voiceSource == null)
            return false;

        if (ShouldSuppressDuplicate(cardId, actionType))
            return false;

        int newPriority = GetVoicePriority(actionType);
        if (voiceSource.isPlaying && newPriority < currentVoicePriority)
            return false;

        voiceSource.Stop();
        voiceSource.clip = clip;
        voiceSource.volume = voiceVolumeScale;
        voiceSource.pitch = Random.Range(
            Mathf.Min(voicePitchRange.x, voicePitchRange.y),
            Mathf.Max(voicePitchRange.x, voicePitchRange.y));
        voiceSource.Play();

        lastVoiceCardId = cardId;
        lastVoiceActionType = actionType;
        lastVoicePlayedTime = Time.unscaledTime;
        currentVoicePriority = newPriority;
        return true;
    }

    private AudioClip GetCardVoiceClip(string cardId, PublicActionType actionType)
    {
        CardData cardData = CardDatabase.Instance.GetCardById(cardId);
        if (cardData == null)
            return null;

        switch (actionType)
        {
            case PublicActionType.BuyCenterCard:
            case PublicActionType.BuyBaseCard:
                return cardData.buyWav;

            case PublicActionType.PlayCard:
            case PublicActionType.EquipCard:
            case PublicActionType.EquipWeapon:
                return cardData.playWav;

            default:
                return null;
        }
    }

    private bool ShouldSuppressDuplicate(string cardId, PublicActionType actionType)
    {
        if (!Mathf.Approximately(duplicateSuppressionWindow, 0f) &&
            cardId == lastVoiceCardId &&
            actionType == lastVoiceActionType &&
            Time.unscaledTime - lastVoicePlayedTime <= duplicateSuppressionWindow)
        {
            return true;
        }

        return false;
    }

    private int GetVoicePriority(PublicActionType actionType)
    {
        switch (actionType)
        {
            case PublicActionType.PlayCard:
            case PublicActionType.EquipCard:
            case PublicActionType.EquipWeapon:
                return 20;

            case PublicActionType.BuyCenterCard:
            case PublicActionType.BuyBaseCard:
                return 10;

            default:
                return 0;
        }
    }

    private void AutoBindSourcesIfNeeded()
    {
        if (voiceSource == null)
        {
            voiceSource = FindChildAudioSource("Voice Source");
        }

        if (bgmSource == null)
        {
            bgmSource = FindChildAudioSource("Bgm Source");
        }

        if (sfxSource == null)
        {
            sfxSource = FindChildAudioSource("Sfx Source");
        }

        if (uiSource == null)
        {
            uiSource = FindChildAudioSource("Ui Source");
        }
    }

    private void ConfigureSources()
    {
        if (voiceSource != null)
        {
            voiceSource.playOnAwake = false;
            voiceSource.loop = false;
        }

        if (bgmSource != null)
        {
            bgmSource.playOnAwake = false;
        }

        if (sfxSource != null)
        {
            sfxSource.playOnAwake = false;
        }

        if (uiSource != null)
        {
            uiSource.playOnAwake = false;
        }
    }

    private void EnsureVoiceSource()
    {
        if (voiceSource != null)
            return;

        voiceSource = gameObject.AddComponent<AudioSource>();
        voiceSource.playOnAwake = false;
        voiceSource.loop = false;
    }

    private AudioSource FindChildAudioSource(string childName)
    {
        Transform child = transform.Find(childName);
        if (child == null)
            return null;

        return child.GetComponent<AudioSource>();
    }
}
