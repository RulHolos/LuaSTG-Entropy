using LuaSTG.Core.Debugger;
using LuaSTG.Core.Resources.Impl;
using Miniaudio;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace LuaSTG.Core.Window.Audio;

public sealed unsafe class BgmChannel : IDisposable
{
    private MusicResource? currentResource;
    private readonly AudioDevice device;
    private ma_sound* sound;
    private bool initialized;
    private float trackVolume = 1f;
    private bool disposed;

    internal BgmChannel(AudioDevice device)
    {
        this.device = device;
        sound = (ma_sound*)NativeMemory.Alloc((nuint)sizeof(ma_sound));
    }

    public bool Play(MusicResource resource, float volume = 1f, bool loop = true)
    {
        if (initialized)
        {
            ma.sound_uninit(sound);
            initialized = false;
            currentResource?.Release();
            currentResource = null;
        }

        uint flags = resource.OnceDecode ? AudioDevice.MA_SOUND_FLAG_DECODE : 0;

        var result = ma.sound_init_from_data_source(device.Engine, resource.DataSourcePtr, flags, device.BgmGroup, sound);
        if (result != ma_result.MA_SUCCESS)
        {
            Logger.luastg.Error($"BgmChannel.Play failed to initialize sound. Error: {result}");
            return false;
        }

        initialized = true;
        currentResource = resource;
        resource.AddRef();

        trackVolume = Math.Clamp(volume, 0f, 1f);
        ma.sound_set_volume(sound, trackVolume);

        if (loop && resource.LoopEndSeconds > resource.LoopStartSeconds)
        {
            uint sampleRate;
            ma.sound_get_data_format(sound, null, null, &sampleRate, null, 0);

            ulong loopBeg = (ulong)(resource.LoopStartSeconds * sampleRate);
            ulong loopEnd = (ulong)(resource.LoopEndSeconds * sampleRate);

            var dataSource = ma.sound_get_data_source(sound);
            ma.data_source_set_loop_point_in_pcm_frames(dataSource, loopBeg, loopEnd);
        }

        ma.sound_set_looping(sound, loop ? 1u : 0u);
        ma.sound_start(sound);
        return true;
    }

    public void Stop()
    {
        if (initialized)
            ma.sound_stop(sound);
    }

    public void SetVolume(float volume)
    {
        trackVolume = Math.Clamp(volume, 0f, 1f);
        if (initialized)
            ma.sound_set_volume(sound, trackVolume);
    }

    public float Volume => trackVolume;

    public bool IsPlaying => initialized && ma.sound_is_playing(sound) != 0;

    public void Pause()
    {
        if (initialized)
            ma.sound_stop(sound); //Exactly like stop. But whatever, it keeps position.
    }

    public void Resume()
    {
        if (initialized)
            ma.sound_start(sound);
    }

    public void Dispose()
    {
        if (disposed)
            return;

        if (initialized)
        {
            ma.sound_uninit(sound);
            initialized = false;
            currentResource?.Release();
            currentResource = null;
        }

        NativeMemory.Free(sound);
        sound = null;
        disposed = true;
        device.ReleaseBgmChannel(this);
    }
}