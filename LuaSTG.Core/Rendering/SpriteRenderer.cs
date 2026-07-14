using LuaSTG.Core.Resources.Impl;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Reflection.Metadata;
using System.Text;

namespace LuaSTG.Core.Rendering;

public sealed class SpriteRenderer : IDisposable
{
    private readonly GL _gl;
    private readonly uint vao;
    private readonly uint vbo;
    private readonly ShaderResource defaultShader;

    private Matrix4x4 projection;

    private static readonly float[] QuadVertices =
    [
        -0.5f, -0.5f, 0f, 0f,
         0.5f, -0.5f, 1f, 0f,
         0.5f,  0.5f, 1f, 1f,
        -0.5f, -0.5f, 0f, 0f,
         0.5f,  0.5f, 1f, 1f,
        -0.5f,  0.5f, 0f, 1f,
    ];

    private const string DefaultVert = """
        #version 330 core
        layout(location = 0) in vec2 aPos;
        layout(location = 0) in vec2 aUv;

        uniform mat4 uMvp;
        uniform vec4 uUvRect; //x0, y0, x1, y1 subrect

        out vec2 vUv;

        void main()
        {
            gl_Position = uMvp * vec4(aPos, 0.0, 1.0);
            vUv = vec2(mix(uUvRect.x, uUvRect.z, aUv.x), mix(uUvRect.y, uUvRect.w, aUv.y));
        }
        """;

    private const string DefaultFrag = """
        #version 330 core
        in vec2 vUv;
        out vec4 FragColor;
 
        uniform sampler2D uTex;
        uniform vec4 uTint;
 
        void main()
        {
            FragColor = texture(uTex, vUv) * uTint;
        }
        """;

    public unsafe SpriteRenderer(GL gl, int screenWidth, int screenHeight)
    {
        _gl = gl;
        defaultShader = new ShaderResource(gl, DefaultVert, DefaultFrag);

        vao = _gl.GenVertexArray();
        vbo = _gl.GenBuffer();

        _gl.BindVertexArray(vao);
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, vbo);

        fixed (float* v = QuadVertices)
            _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(QuadVertices.Length * sizeof(float)), v, BufferUsageARB.StaticDraw);

        const uint stride = 4 * sizeof(float);
        _gl.EnableVertexAttribArray(0);
        _gl.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, stride, (void*)0);
        _gl.EnableVertexAttribArray(1);
        _gl.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, stride, (void*)(2 * sizeof(float)));

        _gl.BindVertexArray(0);

        Resize(screenWidth, screenHeight);

        _gl.Enable(EnableCap.Blend);
        _gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);

        RenderEngine.Instance.Device.Window.Resize += Resize;
    }

    public void Resize(Vector2D<int> vec) => Resize(vec.X, vec.Y);

    public void Resize(int screenWidth, int screenHeight)
    {
        projection = Matrix4x4.CreateOrthographicOffCenter(0, screenWidth, screenHeight, 0, -1f, 1f);
    }

    public void Draw(TextureResource texture, float x, float y, float rot = 0f, float scaleX = 1f, float scaleY = 1f, Vector4? tint = null, ShaderResource? shader = null)
    {
        Draw(texture.FullImage, x, y, rot, scaleX, scaleY, tint, shader);
    }

    public void Draw(ImageResource image, float x, float y, float rotation = 0f, float scaleX = 1f, float scaleY = 1f, Vector4? tint = null, ShaderResource? shader = null)
    {
        ShaderResource s = shader ?? defaultShader;

        Matrix4x4 model = Matrix4x4.CreateScale(image.Width * scaleX, image.Height * scaleY, 1f) *
            Matrix4x4.CreateRotationZ(rotation) *
            Matrix4x4.CreateTranslation(x, y, 0f);

        s.Use();
        s.SetUniform("uMvp", model * projection);
        s.SetUniform("uUvRect", new Vector4(image.U0, image.V0, image.U1, image.V1));
        s.SetUniform("uTint", tint ?? Vector4.One);
        s.SetUniform("uTex", 0);

        image.Source.Bind(0);
        _gl.BindVertexArray(vao);
        _gl.DrawArrays(PrimitiveType.Triangles, 0, 6);
    }

    /// <summary>
    /// Compiles a custom shader for use with Draw().
    /// </summary>
    /// <param name="vertexSource"></param>
    /// <param name="fragmentSource"></param>
    /// <returns></returns>
    public ShaderResource CreateShader(string vertexSource, string fragmentSource) => new(_gl, vertexSource, fragmentSource);

    public void Dispose()
    {
        RenderEngine.Instance.Device.Window.Resize -= Resize;
        _gl.DeleteBuffer(vbo);
        _gl.DeleteVertexArray(vao);
        defaultShader.Dispose();
    }
}
