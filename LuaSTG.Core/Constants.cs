using Steamworks;
using System;
using System.Collections.Generic;
using System.Text;

namespace LuaSTG.Core;

public static class Constants
{
    public const bool HAVE_STEAM_API = false;
    public static AppId_t STEAM_APP_ID = new(480);
    public const bool KEEP_LAUNCH_BY_STEAM = true;
    public const string LUASTG_CONFIGURATION_FILE = "config.json";
    public const string LUASTG_LOG_FILE = "engine.log";

    public const string LUASTG_NAME = "LuaSTG";
    public const string LUASTG_BRANCH = "Entropy";
    public const string LUASTG_VERSION_NAME = "v1.0.0";
    public const string LUASTG_DEV_BRANCH = "alpha";
    public const int LUASTG_VERSION_MAJOR = 1;
    public const int LUASTG_VERSION_MINOR = 0;
    public const int LUASTG_VERSION_PATCH = 0;
    public static string LUASTG_INFO = $"{LUASTG_NAME} {LUASTG_BRANCH} {LUASTG_VERSION_NAME}-{LUASTG_DEV_BRANCH}";

    public const bool USING_LAUNCH_FILE = true;
    public const string LUASTG_LAUNCH_SCRIPT = "launch";

    #region Maths

    public const double L_PI = 3.1415926535897932384626433832795;
    public const float L_PI_F = 3.1415926535897932384626433832795f;

    public const double L_PI_HALF = L_PI * 0.5;
    public const float L_PI_HALF_F = L_PI_F * 0.5f;

    public const double L_TAU = L_PI * 2.0;
    public const float L_TAU_F = L_PI_F * 2.0f;


    public const double L_RAD_TO_DEG = 180.0 / L_PI;
    public const float L_RAD_TO_DEG_F = 180.0f / L_PI_F;

    public const double L_DEG_TO_RAD = L_PI / 180.0;
    public const float L_DEG_TO_RAD_F = L_PI_F / 180.0f;

    #endregion
}
