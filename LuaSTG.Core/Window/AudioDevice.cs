using LuaSTG.Core.Debugger;
using LuaSTG.Core.Resources.Impl;
using LuaSTG.Core.Window.Audio;
using Miniaudio;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace LuaSTG.Core.Window;

public sealed unsafe class AudioDevice : IDisposable
{
    private const int SeVoiceCount = 32; //Sound Effect max overlap count
    public const uint MA_SOUND_FLAG_DECODE = 0x00000002;

    private ma_engine* engine;

    public ma_engine* Engine => engine;
    public ma_sound* BgmGroup => bgmGroup;

    private ma_sound* masterGroup;
    private ma_sound* bgmGroup;
    private ma_sound* seGroup;

    private float masterVolume = 1f;
    private float bgmChannelVolume = 1f;
    private float seChannelVolume = 1f;

    private ma_sound* bgmSound;
    private bool bgmSoundInitialized;

    private readonly List<BgmChannel> bgmChannels = [];

    private ma_sound*[] seVoices;
    private bool[] seVoiceInitialized;
    private int seNextVoice;

    public bool IsInitialized { get; private set; }

    public AudioDevice()
    {
        Initialize();
    }

    public bool Initialize()
    {
        engine = (ma_engine*)NativeMemory.Alloc((nuint)sizeof(ma_engine));
        if (ma.engine_init(null, engine) != ma_result.MA_SUCCESS)
        {
            NativeMemory.Free(engine);
            return false;
        }

        masterGroup = (ma_sound*)NativeMemory.Alloc((nuint)sizeof(ma_sound));
        ma.sound_group_init(engine, 0, null, masterGroup);

        bgmGroup = (ma_sound*)NativeMemory.Alloc((nuint)sizeof(ma_sound));
        ma.sound_group_init(engine, 0, masterGroup, bgmGroup);

        seGroup = (ma_sound*)NativeMemory.Alloc((nuint)sizeof(ma_sound));
        ma.sound_group_init(engine, 0, masterGroup, seGroup);

        bgmSound = (ma_sound*)NativeMemory.Alloc((nuint)sizeof(ma_sound));

        seVoices = new ma_sound*[SeVoiceCount];
        seVoiceInitialized = new bool[SeVoiceCount];
        for (int i = 0; i < SeVoiceCount; i++)
            seVoices[i] = (ma_sound*)NativeMemory.Alloc((nuint)sizeof(ma_sound));

        IsInitialized = true;
        return true;
    }

    public void Dispose() => Shutdown();

    public void Shutdown()
    {
        if (!IsInitialized)
            return;

        foreach (var channel in bgmChannels.ToArray())
            channel.Dispose();

        if (bgmSoundInitialized)
            ma.sound_uninit(bgmSound);
        NativeMemory.Free(bgmSound);

        for (int i = 0; i < SeVoiceCount; i++)
        {
            if (seVoiceInitialized[i])
                ma.sound_uninit(seVoices[i]);
            NativeMemory.Free(seVoices[i]);
        }

        ma.sound_uninit(bgmGroup);
        NativeMemory.Free(bgmGroup);

        ma.sound_uninit(seGroup);
        NativeMemory.Free(seGroup);

        ma.sound_uninit(masterGroup);
        NativeMemory.Free(masterGroup);

        ma.engine_uninit(engine);
        NativeMemory.Free(engine);

        IsInitialized = false;
    }

    public void RegisterResource(AudioResource resource) => resource.Register(engine);

    public void SetMasterVolume(float vol)
    {
        MasterVolume = vol;
    }

    public float MasterVolume
    {
        get => masterVolume;
        set
        {
            masterVolume = Math.Clamp(value, 0f, 1f);
            ma.sound_group_set_volume(masterGroup, masterVolume);
        }
    }

    #region BGM

    public BgmChannel CreateBgmChannel()
    {
        var channel = new BgmChannel(this);
        bgmChannels.Add(channel);
        return channel;
    }

    internal void ReleaseBgmChannel(BgmChannel channel)
        => bgmChannels.Remove(channel);

    public bool PlayBgm(MusicResource resource, float volume = 1f, bool loop = true)
    {
        if (bgmSoundInitialized)
        {
            ma.sound_uninit(bgmSound);
            bgmSoundInitialized = false;
        }

        uint flags = resource.OnceDecode ? MA_SOUND_FLAG_DECODE : 0;

        var result = ma.sound_init_from_data_source(engine, resource.DataSourcePtr, flags, bgmGroup, bgmSound);
        if (result != ma_result.MA_SUCCESS)
        {
            Logger.luastg.Error($"PlayBgm failed to initialize sound node from data source. Error: {result}");
            return false;
        }

        bgmSoundInitialized = true;
        ma.sound_set_volume(bgmSound, Math.Clamp(volume, 0f, 1f));

        if (loop)
        {
            bool hasCustomLoopPoints = resource.LoopEndSeconds > resource.LoopStartSeconds;

            if (hasCustomLoopPoints)
            {
                uint sampleRate;
                ma.sound_get_data_format(bgmSound, null, null, &sampleRate, null, 0);

                ulong loopBeg = (ulong)(resource.LoopStartSeconds * sampleRate);
                ulong loopEnd = (ulong)(resource.LoopEndSeconds * sampleRate);

                var dataSource = ma.sound_get_data_source(bgmSound);
                ma.data_source_set_loop_point_in_pcm_frames(dataSource, loopBeg, loopEnd);
            }
        }

        ma.sound_set_looping(bgmSound, loop ? 1u : 0u);
        ma.sound_start(bgmSound);

        return true;
    }

    public void StopBgm()
    {
        if (bgmSoundInitialized)
            ma.sound_stop(bgmSound);
    }

    public void SetBgmTrackVolume(float volume)
    {
        if (bgmSoundInitialized)
            ma.sound_set_volume(bgmSound, Math.Clamp(volume, 0f, 1f));
    }

    public float BgmChannelVolume
    {
        get => bgmChannelVolume;
        set
        {
            bgmChannelVolume = Math.Clamp(value, 0f, 1f);
            ma.sound_group_set_volume(bgmGroup, bgmChannelVolume);
        }
    }

    #endregion
    #region SE

    public bool PlaySe(SoundEffectResource resource, float volume = 1f)
    {
        int i = seNextVoice;
        seNextVoice = (seNextVoice + 1) % seVoices.Length;

        if (seVoiceInitialized[i])
            ma.sound_uninit(seVoices[i]);

        var result = ma.sound_init_from_data_source(engine, resource.DataSourcePtr, 0, seGroup, seVoices[i]);
        if (result != ma_result.MA_SUCCESS)
        {
            seVoiceInitialized[i] = false;
            Logger.luastg.Error($"PlaySe failed to initialize sound node from data source. Error: {result}");
            return false;
        }

        seVoiceInitialized[i] = true;
        ma.sound_set_volume(seVoices[i], Math.Clamp(volume, 0f, 1f));
        ma.sound_start(seVoices[i]);
        return true;
    }

    public void SetSeTrackVolume(float volume)
    {
        for (int i = 0; i < SeVoiceCount; i++)
        {
            if (seVoiceInitialized[i])
                ma.sound_set_volume(seVoices[i], Math.Clamp(volume, 0f, 1f));
        } 
    }

    public float SeChannelVolume
    {
        get => seChannelVolume;
        set
        {
            seChannelVolume = Math.Clamp(value, 0f, 1f);
            ma.sound_group_set_volume(seGroup, seChannelVolume);
        }
    }

    #endregion
}
