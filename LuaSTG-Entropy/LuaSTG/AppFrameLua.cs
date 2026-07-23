using luajit_sharp;
using LuaSTG.Core.Attributes;
using LuaSTG.Core.Debugger;
using LuaSTG.Core.FileSystem;
using LuaSTG.LuaSTG.LuaBinding;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace LuaSTG.LuaSTG;

public partial class AppFrame
{
    [LuaBind]
    public static int StackTraceback(LuaState L)
    {
        int ret = 0;

        lua_getfield(L, LUA_GLOBALSINDEX, "debug");
        if (!lua_istable(L, -1))
        {
            lua_pop(L, 1);
            return 1;
        }
        lua_getfield(L, -1, "traceback");
        if (!lua_isfunction(L, -1) && !lua_iscfunction(L, -1))
        {
            lua_pop(L, 2);
            return 1;
        }

        lua_pushvalue(L, -3);
        lua_pushinteger(L, 2);
        ret = lua_pcall(L, 2, 1, 0);
        if (ret != 0)
        {
            string? errmsg = lua_tostring(L, -1);
            errmsg ??= "(error object is a nil value)";
            Logger.luajit.Error("A StackTraceback error has occured: {}", errmsg);
            lua_pop(L, 2);
            return 1;
        }

        return 1;
    }

    public unsafe bool SafeCallScript(byte[] data, string desc)
    {
        int offset = 0;
        int length = data.Length;

        //Strip UTF-8 BOM if present.......why.
        if (length >= 3 && data[0] == 0xEF && data[1] == 0xBB && data[2] == 0xBF)
        {
            offset = 3;
            length -= 3;
        }

        fixed (byte* pData = &data[offset])
        {
            return SafeCallScript(pData, (nuint)length, desc);
        }
    }

    public bool SafeCallScript(string source, string desc)
    {
        byte[] data = Encoding.UTF8.GetBytes(source);
        return SafeCallScript(data, desc);
    }

    public unsafe bool SafeCallScript(byte* source, nuint len, string desc)
    {
        lua_pushcfunction(L, CFunctions.StackTraceback);
        int tStacktraceIndex = lua_gettop(L);
        if (luaL_loadbuffer(L, source, len, desc) != 0)
        {
            Logger.luajit.Error($"Error while compiling '{desc}': {lua_tostring(L, -1)}");
            //TODO: Messagebox error;

            lua_pop(L, 2);
            return false;
        }
        if (lua_pcall(L, 0, 0, tStacktraceIndex) != 0)
        {
            string? errmsg = lua_tostring(L, -1);
            errmsg ??= "(error object is a nil value)";
            Logger.luajit.Error($"Error while compiling '{desc}': {errmsg}");
            //TODO: Messagebox error;

            lua_pop(L, 2);
            return false;
        }
        lua_pop(L, 1);
        return true;
    }

    public bool UnsafeCallGlobalFunction(string name, int retc)
    {
        lua_getglobal(L, name);
        if (lua_isfunction(L, -1) || lua_iscfunction(L, -1))
        {
            lua_call(L, 0, retc);
            return true;
        }
        return false;
    }

    public unsafe bool SafeCallGlobalFunction(string name, int retc = 0)
    {
        lua_pushcfunction(L, CFunctions.StackTraceback);
        int tStacktraceIndex = lua_gettop(L);
        lua_getglobal(L, name);
        if (lua_pcall(L, 0, retc, tStacktraceIndex) != 0)
        {
            try
            {
                string? errmsg = lua_tostring(L, -1);
                errmsg ??= "(error object is a nil value)";
                Logger.luajit.Error($"Error calling global function '{name}': {errmsg}");
                //TODO: Messagebox for error
            }
            catch (Exception ex)
            {
                Logger.luajit.Error("Unable to call function");
            }
            lua_pop(L, 2);
            return false;
        }

        lua_remove(L, tStacktraceIndex);
        return true;
    }

    public void LoadScript(LuaState L, string path, string? packname)
    {
        if (packname != null)
            Logger.luastg.Information($"Loading script '{packname}' in package '{path}'");
        else
            Logger.luastg.Information($"Loading scripts '{path}'");

        bool loaded = false;
        byte[] src = [];
        if (packname != null)
        {
            if (FileSystemManager.TryGetArchiveByPath(packname, out FileSystemArchive? arc))
            {
                loaded = arc!.ReadFile(path, out byte[]? srcb);
                src = srcb ?? [];
            }
        }
        else
        {
            loaded = FileSystemManager.ReadFile(path, out byte[]? srcb);
            src = srcb ?? [];
        }

        if (!loaded)
        {
            Logger.luastg.Error($"Unable to load file '{path}'");
            luaL_error(L, $"can't load file '{path}'");
            return;
        }
        //TODO: This is gonna cause UTF-8 BOM issues too.
        if (luaL_loadbuffer(L, Encoding.UTF8.GetString(src), (nuint)src.Length, luaL_checkstring(L, 1)) != 0)
        {
            string? tDetail = lua_tostring(L, -1);
            Logger.luajit.Error($"Error while compiling '{path}': {tDetail}");
            luaL_error(L, $"failed to compile '{path}': {tDetail}");
            return;
        }
        lua_call(L, 0, LUA_MULTRET);
    }

    public bool OnOpenLuaEngine()
    {
        FileSystemManager.AddFileSystem("luastg", EmbeddedFileSystem.Instance);

        Logger.luajit.Information(LUAJIT_VERSION);
        L = luaL_newstate();
        if (L.IsNull)
        {
            Logger.luajit.Error("Unable to create LuaJIT engine");
            return false;
        }
        if (luaJIT_setmode(L, 0, LUAJIT_MODE.ENGINE | LUAJIT_MODE.ON) == 0)
        {
            Logger.luajit.Error("Unable to start JIT mode");
        }
        _ = lua_gc(L, LUA_GCSTOP, 0);
        {
            Logger.luajit.Information("Registering standard libraries and built-in packages");
            {
                luaL_openlibs(L);
                luaopen_cjson(L);
                luaopen_lfs(L);
                LuaRegisterCustomLoader(L);
            }
            lua_settop(L, 0);

            LuaWrapper.RegisterBuiltInClassWrapper(L);

            Logger.luajit.Information($"Getting command line arguments");

            var args = CommandLineArguments.GetArguments();
            //Arg 1 : dll of the exe
            //Arg 2 : Nil or lua code
            if (args.Any())
            {
                lua_getglobal(L, "lstg"); // ? t
                lua_createtable(L, args.Count, 0); // ? t t
                int idx = 0;
                foreach (var arg in args)
                {
                    Logger.luajit.Information($"[{idx}] {arg}");
                    lua_pushstring(L, arg); // ? t t s
                    lua_rawseti(L, -2, idx + 1); // ? t t
                    idx++;
                }
                lua_setfield(L, -2, "args"); // ? t
                lua_pop(L, 1); // ?
            }

            string boost_script = """
                -- LuaSTG Sub boost script
                package.cpath = ""
                package.path = "?.lua;?/init.lua;"
                require("luastg.main")
                """;

            if (!SafeCallScript(boost_script, "luastg/boost.lua"))
                return false;
        }
        _ = lua_gc(L, LUA_GCRESTART, -1);

        return true;
    }

    public bool OnLoadLaunchScriptAndFiles()
    {
        bool is_launch_loaded = false;
        
        if (USING_LAUNCH_FILE)
        {
            Logger.luastg.Information("Loading launch file");
            string filename = LUASTG_LAUNCH_SCRIPT;
            if (FileSystemManager.HasFile(Path.ChangeExtension(filename, ".lua")))
            {
                filename = Path.ChangeExtension(filename, ".lua");
            }
            if (FileSystemManager.ReadFile(filename, out byte[]? data))
            {
                Logger.luastg.Information($"Found '{filename}'");

                if (SafeCallScript(data!, filename))
                {
                    is_launch_loaded = true;
                    Logger.luastg.Information($"Loading script '{filename}'");
                }
                else
                {
                    Logger.luastg.Error($"Failed to load launch file '{filename}'");
                }
            }
            if (!is_launch_loaded)
            {
                Logger.luastg.Warning($"Launch file no found: '{filename}'");
            }
        }

        return true;
    }

    public bool OnLoadMainScriptAndFiles()
    {
        Logger.luastg.Information("Loading entry point candidates");
        //Added src/core.lua compared to Flux. Just felt like it.
        List<string> candidates = ["core.lua", "main.lua", "src/main.lua", "src/core.lua"];
        string? entry_script = null;
        foreach (string candidate in candidates)
        {
            if (FileSystemManager.HasFile(candidate))
            {
                entry_script = candidate;
                break;
            }
        }
        if (string.IsNullOrEmpty(entry_script))
        {
            Logger.luastg.Error($"Cannot find any entry point candidates at: {string.Join(", ", candidates)}");
            return true;
        }
        if (FileSystemManager.ReadFile(entry_script, out byte[]? data))
        {
            Logger.luastg.Information($"Loading script '{entry_script}'");
            SafeCallScript(Encoding.UTF8.GetString(data!), entry_script);
        }
        return true;
    }

    public unsafe static void LuaRegisterCustomLoader(LuaState L)
    {
        lua_getglobal(L, "package");
        if (lua_istable(L, -1))
        {
            lua_getfield(L, -1, "loaders");
            if (lua_istable(L, -1))
            {
                lua_pushinteger(L, (int)(lua_objlen(L, -1) + 1));
                lua_pushcfunction(L, LuaCustomLoader.CFunctions.PackageLoaderLuastg);
                lua_settable(L, -3);
            }
            lua_pop(L, 1);
        }
        lua_pop(L, 1);
    }
}
