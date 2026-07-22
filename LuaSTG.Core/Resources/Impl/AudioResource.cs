using Miniaudio;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using NVorbis;

namespace LuaSTG.Core.Resources.Impl;

public abstract unsafe class AudioResource : IResource, IDisposable
{
    public string Name { get; set; }
    public string Path { get; set; }
    protected byte[] Data { get; }

    private GCHandle pinnedData;
    private nint virtualNameAnsi;
    private ma_resource_manager* resourceManager;
    private ma_resource_manager_data_source* rmDataSource;
    private bool registered;

    private int refCount;
    private bool pendingUnload;

    public bool IsPlaying => refCount > 0;

    internal void AddRef() => refCount++;

    protected AudioResource(string name, string path, byte[] data)
    {
        Name = name;
        if (path.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase))
        {
            Data = OggToWav(data);
            Path = System.IO.Path.ChangeExtension(path, ".wav");
        }
        else
        {
            Data = data;
            Path = path;
        }
    }

    #region Helper

    public static byte[] OggToWav(byte[] oggData)
    {
        using var msInput = new MemoryStream(oggData);
        using var vorbis = new VorbisReader(msInput);
        using var msOutput = new MemoryStream();
        using var writer = new BinaryWriter(msOutput);

        int channels = vorbis.Channels;
        int sampleRate = vorbis.SampleRate;

        writer.Write(Encoding.ASCII.GetBytes("RIFF"));
        writer.Write(0);
        writer.Write(Encoding.ASCII.GetBytes("WAVE"));

        writer.Write(Encoding.ASCII.GetBytes("fmt "));
        writer.Write(16);
        writer.Write((short)3);
        writer.Write((short)channels);
        writer.Write(sampleRate);
        writer.Write(sampleRate * channels * 4);
        writer.Write((short)(channels * 4));
        writer.Write((short)32);

        writer.Write(Encoding.ASCII.GetBytes("data"));
        writer.Write(0);

        float[] readBuffer = new float[channels * 4096];
        int framesRead;
        int totalDataBytes = 0;

        while ((framesRead = vorbis.ReadSamples(readBuffer, 0, readBuffer.Length)) > 0)
        {
            for (int i = 0; i < framesRead; i++)
                writer.Write(readBuffer[i]);
            totalDataBytes += framesRead * sizeof(float);
        }

        long fileLength = msOutput.Length;

        msOutput.Position = 4;
        writer.Write((int)(fileLength - 8));

        msOutput.Position = 40;
        writer.Write(totalDataBytes);

        return msOutput.ToArray();
    }

    #endregion

    internal void Register(ma_engine* engine)
    {
        if (registered)
            return;

        if (engine == null)
            throw new Exception($"Cannot register '{Name}': engine is null (AudioDevice not initialized yet?)");

        if (Data == null || Data.Length == 0)
            throw new Exception($"Cannot register '{Name}': byte array data is null or empty!");

        resourceManager = ma.engine_get_resource_manager(engine);

        if (resourceManager == null)
            throw new Exception($"Cannot register '{Name}': resource manager is null");

        pinnedData = GCHandle.Alloc(Data, GCHandleType.Pinned);
        //Hot garbage
        string finalName = System.IO.Path.ChangeExtension(Name, System.IO.Path.GetExtension(Path));
        virtualNameAnsi = Marshal.StringToHGlobalAnsi(finalName);

        ma_result result = ma.resource_manager_register_encoded_data(
            resourceManager,
            (sbyte*)virtualNameAnsi,
            (void*)pinnedData.AddrOfPinnedObject(),
            (nuint)Data.Length
        );

        if (result != ma_result.MA_SUCCESS)
        {
            pinnedData.Free();
            Marshal.FreeHGlobal(virtualNameAnsi);
            throw new Exception($"Failed to register audio resource '{Name}' ({result})");
        }

        uint rmFlags = 0;
        if (this is MusicResource music && music.OnceDecode)
        {
            rmFlags |= 2;
        }

        rmDataSource = (ma_resource_manager_data_source*)NativeMemory.Alloc((nuint)sizeof(ma_resource_manager_data_source));

        result = ma.resource_manager_data_source_init(resourceManager, (sbyte*)virtualNameAnsi, rmFlags, null, rmDataSource);
        if (result != ma_result.MA_SUCCESS)
        {
            ma.resource_manager_unregister_data(resourceManager, (sbyte*)virtualNameAnsi);
            pinnedData.Free();
            Marshal.FreeHGlobal(virtualNameAnsi);
            NativeMemory.Free(rmDataSource);
            rmDataSource = null;
            throw new Exception($"Failed to initialize data source backend for '{Name}' ({result})");
        }

        registered = true;
    }

    internal void* DataSourcePtr => rmDataSource;
    internal sbyte* VirtualNamePtr => (sbyte*)virtualNameAnsi;
    internal bool IsRegistered => registered;

    internal void Release()
    {
        if (refCount > 0)
            refCount--;

        if (refCount == 0 && pendingUnload)
            Dispose();
    }

    public void RequestUnload()
    {
        if (refCount > 0)
            pendingUnload = true;
        else
            Dispose();
    }

    public void Dispose()
    {
        if (rmDataSource != null)
        {
            ma.resource_manager_data_source_uninit(rmDataSource);
            NativeMemory.Free(rmDataSource);
            rmDataSource = null;
        }

        if (registered)
        {
            ma.resource_manager_unregister_data(resourceManager, VirtualNamePtr);
            registered = false;
        }

        if (virtualNameAnsi != 0)
        {
            Marshal.FreeHGlobal(virtualNameAnsi);
            virtualNameAnsi = 0;
        }

        if (pinnedData.IsAllocated)
            pinnedData.Free();
    }
}

public sealed class MusicResource : AudioResource
{
    public double LoopStartSeconds { get; }
    public double LoopEndSeconds { get; }
    public bool OnceDecode { get; }

    public MusicResource(string name, string path, byte[] data, double loopStart, double loopEnd, bool onceDecode)
        : base(name, path, data)
    {
        LoopStartSeconds = loopStart;
        LoopEndSeconds = loopEnd;
        OnceDecode = onceDecode;
    }
}

public sealed class SoundEffectResource : AudioResource
{
    public SoundEffectResource(string name, string path, byte[] data) : base(name, path, data) { }
}
