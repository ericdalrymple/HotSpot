using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

public class InstanceID
{
    //
    //-- Constants
    //
    private static readonly int INVALID_ID = -1;
    
    //
    //-- Members
    //
    private static int s_MaxID = INVALID_ID + 1;
    private static InstanceID s_InvalidID = null;

    private int m_ID;

    //
    //-- Attributes
    //
    public static InstanceID Invalid_IID
    {
        get
        {
            if( null == s_InvalidID )
            {
                s_InvalidID = new InstanceID();
                s_InvalidID.m_ID = INVALID_ID;
            }

            return s_InvalidID;
        }
    }

    public bool IsValid{ get{ return (this != Invalid_IID); } }

    //
    //-- Operators
    //
    public static bool operator ==( InstanceID a, InstanceID b )
    {
        return (a.m_ID == b.m_ID);
    }

    public static bool operator !=( InstanceID a, InstanceID b )
    {
        return !(a == b);
    }

    //
    //-- Body
    //
    public InstanceID()
    {
        m_ID = s_MaxID++;
    }
    
    public override bool Equals( object obj )
    {
        if( obj is InstanceID )
        {
            return Equals( (InstanceID)obj );
        }

        return false;
    }

    public bool Equals( InstanceID other )
    {
        return (m_ID == other.m_ID);
    }

    public override int GetHashCode()
    {
        return m_ID;
    }
}

