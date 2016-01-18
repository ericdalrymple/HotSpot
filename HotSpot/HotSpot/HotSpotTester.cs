using System;

class HotSpotTester
{
    static void Main( string[] args )
    {
        InstanceID id1 = new InstanceID();
        InstanceID id2 = new InstanceID();
        id1 = id2;

        Console.In.ReadLine();
    }
}
