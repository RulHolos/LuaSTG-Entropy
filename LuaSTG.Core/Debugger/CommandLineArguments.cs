using System;
using System.Collections.Generic;
using System.Text;

namespace LuaSTG.Core.Debugger;

public static class CommandLineArguments
{
    private static string[]? cached;

    private static string[] Args => cached ??= Environment.GetCommandLineArgs();

    public static IReadOnlyList<string> GetArguments() => Args;

    public static bool OptionExists(string option) => Array.IndexOf(Args, option) >= 0;
}
