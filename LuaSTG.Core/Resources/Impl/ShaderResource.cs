using Silk.NET.OpenGL;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;

namespace LuaSTG.Core.Resources.Impl;

public sealed unsafe class ShaderResource : IResource, IDisposable
{
    public string Name { get; set; }

    public uint Handle { get; }

    private readonly GL _gl;
    private readonly Dictionary<string, int> _uniformCache = [];

    public ShaderResource(GL gl, string vertexSource, string fragmentSource)
    {
        _gl = gl;
        Handle = Compile(vertexSource, fragmentSource);
    }

    public void Use() => _gl.UseProgram(Handle);

    public int GetUniformLocation(string name)
    {
        if (_uniformCache.TryGetValue(name, out int cached))
            return cached;

        int loc = _gl.GetUniformLocation(Handle, name);
        _uniformCache[name] = loc;
        return loc;
    }

    public void SetUniform(string name, int value) => _gl.Uniform1(GetUniformLocation(name), value);
    public void SetUniform(string name, float value) => _gl.Uniform1(GetUniformLocation(name), value);
    public void SetUniform(string name, Vector2 value) => _gl.Uniform2(GetUniformLocation(name), value.X, value.Y);
    public void SetUniform(string name, Vector4 value) => _gl.Uniform4(GetUniformLocation(name), value.X, value.Y, value.Z, value.W);
    public void SetUniform(string name, Matrix4x4 value) => _gl.UniformMatrix4(GetUniformLocation(name), 1, false, (float*)&value);

    private uint Compile(string vertSrc, string fragSrc)
    {
        uint vert = CompileStage(ShaderType.VertexShader, vertSrc);
        uint frag = CompileStage(ShaderType.FragmentShader, fragSrc);

        uint program = _gl.CreateProgram();
        _gl.AttachShader(program, vert);
        _gl.AttachShader(program, frag);
        _gl.LinkProgram(program);

        _gl.GetProgram(program, ProgramPropertyARB.LinkStatus, out int status);
        if (status == 0)
            throw new Exception($"Shader program link failed: {_gl.GetProgramInfoLog(program)}");

        _gl.DetachShader(program, vert);
        _gl.DetachShader(program, frag);
        _gl.DeleteShader(vert);
        _gl.DeleteShader(frag);
        return program;
    }

    private uint CompileStage(ShaderType type, string src)
    {
        uint shader = _gl.CreateShader(type);
        _gl.ShaderSource(shader, src);
        _gl.CompileShader(shader);
        _gl.GetShader(shader, ShaderParameterName.CompileStatus, out int status);
        if (status == 0)
            throw new Exception($"{type} compile failed: {_gl.GetShaderInfoLog(shader)}");
        return shader;
    }

    public void Dispose()
    {
        _gl.DeleteProgram(Handle);
    }
}
