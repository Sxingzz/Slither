using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(AudioSource))]//Unity sẽ tự động thêm thành phần AudioSource vào GameObject có script này.
public class ClientMusicPlayer : Singleton<ClientMusicPlayer>
{
    [SerializeField] private AudioClip nomAudioClip;

    private AudioSource _audioSource;

    public override void Awake()
    {
        base.Awake();
        _audioSource = GetComponent<AudioSource>();
    }

    public void PlayNomAudioClip()
    {
        _audioSource.clip = nomAudioClip;
        _audioSource.Play();
    }
}
