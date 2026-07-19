using luajit_sharp;
using LuaSTG.Core.Attributes;
using LuaSTG.Core.Debugger;
using LuaSTG.Core.LuaBindings;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace LuaSTG.LuaSTG.LuaBinding.Modern;

public unsafe partial class Well512 : ILuaBinding
{
    private const string class_name = "lstg.Rand";

    #region Interop Helpers

    public static bool Is(LuaState L, int index)
    {
        LuaStack ctx = new(L);
        return ctx.IsMetatable(index, class_name);
    }

    public static Well512Impl* As(LuaState L, int index)
    {
        void* userdata = lua_touserdata(L, index);
        if (userdata == null)
            luaL_error(L, $"Expected {class_name} userdata pointer, got null.");
        return (Well512Impl*)userdata;
    }

    [LuaBind]
    public static int Create(LuaState L)
    {
        LuaStack ctx = new(L);
        var self = ctx.CreateUserData<Well512Impl>();
        ctx.SetMetatable(ctx.IndexOfTop(), class_name);

        self->Seed(0);
        return 1;
    }

    #endregion
    #region Metamethods

    [LuaBind]
    public static int __tostring(LuaState L)
    {
        LuaStack ctx = new(L);
        ctx.Push(class_name);
        return 1;
    }

    #endregion
    #region Methods

    [LuaBind]
    public static int Seed(LuaState L)
    {
        LuaStack ctx = new(L);
        var self = As(L, 1);
        uint value = (uint)luaL_checknumber(L, 2);
        self->Seed(value);
        return 0;
    }

    [LuaBind]
    public static int GetSeed(LuaState L)
    {
        LuaStack ctx = new(L);
        var self = As(L, 1);
        ctx.Push(self->BaseSeed);
        return 1;
    }

    [LuaBind]
    public static int Integer(LuaState L)
    {
        LuaStack ctx = new(L);
        var self = As(L, 1);
        int a = ctx.GetValue<int>(2);
        int b = ctx.GetValue<int>(3);

        if (a > b)
            (a, b) = (b, a);

        long range = (long)b - a;
        if (range > 0x7fffffff)
        {
            ctx.RaiseError($"range [a:{a}, b:{b}] too large, (b - a) must <= 2147483647");
            return 0;
        }

        ctx.Push(self->NextInteger(a, b));
        return 1;
    }

    [LuaBind]
    public static int Number(LuaState L)
    {
        LuaStack ctx = new(L);
        var self = As(L, 1);
        float a = ctx.GetValue<float>(2);
        float b = ctx.GetValue<float>(3);

        if (a > b)
            (a, b) = (b, a);

        ctx.Push(self->NextFloat(a, b));
        return 1;
    }

    [LuaBind]
    public static int Sign(LuaState L)
    {
        LuaStack ctx = new(L);
        var self = As(L, 1);
        int signResult = (self->Next() & 1) == 0 ? 1 : -1;
        ctx.Push(signResult);
        return 1;
    }

    [LuaBind]
    public static int Clone(LuaState L)
    {
        LuaStack ctx = new(L);
        var self = As(L, 1);

        var other = ctx.CreateUserData<Well512Impl>();
        ctx.SetMetatable(ctx.IndexOfTop(), class_name);

        *other = *self;
        return 1;
    }

    [LuaBind]
    public static int Serialize(LuaState L)
    {
        LuaStack ctx = new(L);
        var self = As(L, 1);

        ctx.Push(self->Serialize());
        return 1;
    }

    [LuaBind]
    public static int Deserialize(LuaState L)
    {
        LuaStack ctx = new(L);
        var self = As(L, 1);

        string data = ctx.GetValue<string>(2);

        bool successsssssssssssssssssssss = self->Deserialize(data);
        ctx.Push(successsssssssssssssssssssss);
        return 1;
    }

    #endregion

    public static void Register(LuaState L)
    {
        LuaWrapper.EnsureLSTGInStack(L);
        Logger.luastg.Verbose($"Registering bindings: 'Well512'");

        LuaStack ctx = new(L);

        //Methods
        StackIndex methodTable = ctx.CreateModule(class_name);
        ctx.SetMapValue(methodTable, "seed", CFunctions.Seed);
        ctx.SetMapValue(methodTable, "integer", CFunctions.Integer);
        ctx.SetMapValue(methodTable, "number", CFunctions.Number);
        ctx.SetMapValue(methodTable, "sign", CFunctions.Sign);
        ctx.SetMapValue(methodTable, "clone", CFunctions.Clone);
        ctx.SetMapValue(methodTable, "serialize", CFunctions.Serialize);
        ctx.SetMapValue(methodTable, "deserialize", CFunctions.Deserialize);
        ctx.SetMapValue(methodTable, "create", CFunctions.Create);

        ctx.SetMapValue(methodTable, "Seed", CFunctions.Seed);
        ctx.SetMapValue(methodTable, "GetSeed", CFunctions.GetSeed);
        ctx.SetMapValue(methodTable, "Int", CFunctions.Integer);
        ctx.SetMapValue(methodTable, "Float", CFunctions.Number);
        ctx.SetMapValue(methodTable, "Sign", CFunctions.Sign);

        //Metamethods
        StackIndex metatable = ctx.CreateMetatable(class_name);
        ctx.SetMapValue(metatable, "__tostring", CFunctions.__tostring);

        //Direct poiting
        ctx.SetMapValue(metatable, "__index", methodTable);

        //Legacy constructor
        StackIndex lstgTable = ctx.PushModule("lstg");
        ctx.SetMapValue(lstgTable, "Rand", CFunctions.Create);
    }
}
