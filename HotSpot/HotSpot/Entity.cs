using System;

/// <summary>
/// Possible entity types. The value of each enumerated value have no
/// intersecting bits with the other values in the enumeration. This
/// will allow support for entities having multiple types without
/// having to manage or iterate through an array of enum values.
/// </summary>
public enum EntityType
: int
{
    Item     = 0x00,    //-- Can be anything
    Player   = 0x01,    //-- In-game entity representing a player
    AI       = 0x02,    //-- In-game entity representing an NPC
    Creature = 0x04,    //-- In-game entity representing a creature other than a player or an AI
}

/// <summary>
/// Every interactable object in the game is an instance of an
/// Entity (or an instance of a subclass of Entity)
/// </summary>
public abstract class Entity
{
    //
    //-- Settings
    //
    public EntityType m_Type = EntityType.Item;

    //
    //-- Members
    //
    private InstanceID m_ID = new InstanceID();

    //
    //-- Attributes
    //
    public InstanceID ID { get { return m_ID; } }

    //
    //-- Abstract members
    //
    public virtual void Affect( EffectType type, int amount ) { }
    public virtual void EnterWorld() { }
    public virtual void ExitWorld() { }
    public virtual void HandleCollision( Entity collider ) { }
    public virtual void HandleCollisionEnd( Entity collider ) { }

    //
    //-- Body
    //
    public bool IsPlayer()
    {
        return (EntityType.Player == m_Type);
    }

    public bool IsAI()
    {
        return (EntityType.AI == m_Type);
    }

    public bool IsCreature()
    {
        return (EntityType.Creature == m_Type) || (EntityType.Player == m_Type) || (EntityType.AI == m_Type);
    }

    public bool IsItem()
    {
        return (EntityType.Item == m_Type);
    }

    public InstanceID GetID()
    {
        return m_ID;
    }
}
