using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

public static class GameSystem
{
    public static double GetTime()
    {
        return (DateTime.Now - Process.GetCurrentProcess().StartTime).TotalSeconds;
    }

    public static bool SendAffectMessage( InstanceID target, EffectType type, int intensity, double time = -1.0d )
    {
        return true;
    }

    public static bool SendProcessJobMessage( InstanceID target, double time = -1.0d )
    {
        return true;
    }
}
