using luajit_sharp;
using LuaSTG.Core.FileSystem;
using LuaSTG.Core.Attributes;
using System;
using System.Collections.Generic;
using System.Text;

namespace LuaSTG.LuaSTG;

//Direct port from Flux
public unsafe partial class LuaCustomLoader
{
    private static bool Readable(string filename)
    {
        try
        {
            return FileSystemManager.HasFile(filename);
        }
        catch
        {
            return false;
        }
    }

    private static string? PushNextTemplate(LuaState L, ref string path)
    {
        path = path.TrimStart(';');
        if (string.IsNullOrEmpty(path)) return null;

        int sepIdx = path.IndexOf(';');
        string template = sepIdx < 0 ? path : path[..sepIdx];

        path = sepIdx < 0 ? string.Empty : path[sepIdx..];

        lua_pushstring(L, template);
        return template;
    }

    public static string Searchpath(LuaState L, string name, string path, string sep, string dirsep)
    {
        luaL_Buffer msg;
        luaL_buffinit(L, &msg);

        if (!string.IsNullOrEmpty(sep))
        {
            luaL_gsub(L, name, sep, dirsep);
            name = lua_tostring(L, -1);
        }

        string remainingPath = path;
        while (PushNextTemplate(L, ref remainingPath) != null)
        {
            string templateStr = lua_tostring(L, -1);

            luaL_gsub(L, templateStr, "?", name);
            string filename = lua_tostring(L, -1);

            lua_remove(L, -2);

            if (Readable(filename))
            {
                return filename;
            }

            lua_pushstring(L, $"\n\tno file '{filename}'");
            lua_remove(L, -2);
            luaL_addvalue(&msg);
        }

        luaL_pushresult(&msg);
        return null;
    }

    public static string? Findfile(LuaState L, string name, string pname)
    {
        string? path = null;
        lua_getglobal(L, "package");
        if (lua_istable(L, -1))
        {
            lua_getfield(L, -1, "path");
            if (lua_isstring(L, -1))
                path = lua_tostring(L, -1);
            else
                luaL_error(L, $"package.{pname} must be a string");
            lua_pop(L, 1);
        }
        else
            luaL_error(L, "package must be a table");
        lua_pop(L, 1);
        return path == null ? null : Searchpath(L, name, path, ".", "/");
    }

    public static void Loaderror(LuaState L, string filename)
    {
        luaL_error(L, $"error loading module {lua_tostring(L, 1)} from file {filename}:\n\t{lua_tostring(L, -1)}");
    }

    [LuaBind]
    public static int PackageLoaderLuastg(LuaState L)
    {
        string? filename = "";
        string name = luaL_checkstring(L, 1);
        filename = Findfile(L, name, "path");
        if (filename == null)
            return 1;

        if (!FileSystemManager.ReadFile(filename, out byte[]? dataBuffer) || dataBuffer == null)
            Loaderror(L, filename);
        else
        {
            fixed (byte* pBytes = dataBuffer)
            {
                if (luaL_loadbuffer(L, pBytes, (nuint)dataBuffer.Length, filename) != 0)
                    Loaderror(L, filename);
            }
        }

        return 1;
    }
}
