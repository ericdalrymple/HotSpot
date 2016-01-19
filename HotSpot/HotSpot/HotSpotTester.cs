using System;
using System.Collections.Generic;

class HotSpotTester
{
    static void Main( string[] args )
    {
        SortedDictionary<int, string> testDict = new SortedDictionary<int, string>();
        testDict.Add( 10, "hello" );
        testDict.Add( 0, "my" );
        testDict.Add( 5, "name" );
        testDict.Add( 2, "is" );
        testDict.Add( 7, "steve" );

        foreach( int i in testDict.Keys )
        {
            Console.Out.WriteLine( i );
        }

        Console.In.ReadLine();
    }
}
