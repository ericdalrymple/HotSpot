using System;
using System.Collections.Generic;
using System.Diagnostics;

/// <summary>
/// Basic simulation for a game system that manages the entities that make up
/// the game world and provides some functionality for getting the current server
/// time, creating and destroying objects and managing collisions. The code in
/// this class is not meant to be particularly was efficient or complete. It was
/// written as a means to test the implementation of the HotSpot class required
/// for the technical test.
/// </summary>

public static class GameSystem
{
    //
    //-- Send Message Calls
    //     This section is for the MESSAGES such as the ones described in the
    //     test instructions.
    //

    public static bool SendAffectMessage( InstanceID recipient, EffectType type, int intensity, double time = -1.0d )
    {
        Entity targetEntity = null;

        if( s_GameEntities.TryGetValue( target, out targetEntity ) )
        {
            targetEntity.Affect( type, intensity );
            return true;
        }

        return false;
    }

    public static bool SendProcessTargetMessage( InstanceID recipient, int targetID, double time = -1.0d )
    {
        Entity targetEntity = null;

        if( s_GameEntities.TryGetValue( recipient, out targetEntity ) )
        {
            if( targetEntity is HotSpot )
            {
                HotSpot hs = (HotSpot)targetEntity;
                hs.ProcessJob( targetID );
            }
        }

        return true;
    }
    


    //
    //-- System Calls
    //     This section is meant for global methods and functions.
    //

    /// <summary>
    /// In an actualy implementation, this function would return the server time, but
    /// for testing purposes, it returns the application time in seconds.
    /// </summary>
    public static double GetTime()
    {
        return (DateTime.Now - Process.GetCurrentProcess().StartTime).TotalSeconds;
    }



    //
    //-- Testing Environment
    //     This section si for functions that allow the creation and
    //     manipulation of entities.
    //

    /// <summary>
    /// Collection of all the entities in the game.
    /// </summary>
    private static Dictionary<InstanceID, Entity> s_GameEntities = new Dictionary<InstanceID, Entity>();

    /// <summary>
    /// Collection of all the active collisions.
    /// </summary>
    private static LinkedList<Tuple<InstanceID, InstanceID>> s_Collisions = new LinkedList<Tuple<InstanceID, InstanceID>>();

    /// <summary>
    /// Creates a new entity of the specified type and puts it into the world. The
    /// 'EnterWorld' method is called on the entity once it has been created.
    /// </summary>
    /// <param name="type"></param>
    public static void CreateEntity( EntityType type )
    {
        Entity newEntity = null;

        switch( type )
        {
            case EntityType.AI:
                newEntity = new AI();
                break;

            case EntityType.Creature:
                newEntity = new Creature();
                break;

            case EntityType.Item:
                newEntity = new Item();
                break;

            case EntityType.Player:
                newEntity = new Player();
                break;
        }

        if( null != newEntity )
        {
            AddEntity( newEntity );
        }
    }

    /// <summary>
    /// Creates a new HotSpot entityand puts it into the world. The 'EnterWorld' method
    /// is called on the entity once it has been created.
    /// </summary>
    public static void CreateHotSpot()
    {
        AddEntity( new HotSpot() );
    }

    /// <summary>
    /// Removes the entity with the specified instance ID from the world or do nothing
    /// if no such entity exists.
    /// </summary>
    public static void DestroyEntity( int instanceID )
    {
        //-- Scan the entities for a matching instance ID
        foreach( Entity entity in s_GameEntities.Values )
        {
            if( entity.GetID() == instanceID )
            {
                //-- Remove the entity
                RemoveEntity( entity );
                return;
            }
        }
    }

    /// <summary>
    /// Creates a new collision between the two entities with the specified instance IDs. If
    /// the two entities are already colliding or if one or both of the specified instance
    /// IDs are not valid, the method does nothing.
    /// </summary>
    public static void Collide( int instanceID1, int instanceID2 )
    {
        if( instanceID1 == instanceID2 )
        {
            Console.Out.WriteLine( "Entities cannot collide with themselves. Specify two different instance IDs." );
        }

        Entity entity1 = null;
        Entity entity2 = null;

        bool matchesFound = GetEntityPair( instanceID1, instanceID2, out entity1, out entity2 );
        if( matchesFound )
        {
            AddCollision( entity1, entity2 );
        }
    }

    /// <summary>
    /// Ends a collision between two entities with specified instance IDs. If the two
    /// entities are not colliding or if one or both of the specified instance IDs are not
    /// valid, the method does nothing.
    /// </summary>
    public static void Separate( int instanceID1, int instanceID2 )
    {
        Entity entity1 = null;
        Entity entity2 = null;

        bool matchesFound = GetEntityPair( instanceID1, instanceID2, out entity1, out entity2 );
        if( matchesFound )
        {
            RemoveCollision( entity1, entity2 );
        }
    }

    /// <summary>
    /// Prints a list of all active collisions to the console.
    /// </summary>
    public static void PrintCollisions()
    {
        Console.Out.WriteLine();
        Console.Out.WriteLine( "Collisions:" );

        foreach( Tuple<InstanceID, InstanceID> collisionTuple in s_Collisions )
        {
            Console.Out.WriteLine( "{ " + collisionTuple.Item1 + ", " + collisionTuple.Item2 + " }" );
        }

        Console.Out.WriteLine( s_Collisions.Count + " collisions" );
        Console.Out.WriteLine();
    }

    /// <summary>
    /// Prints a list of all game entities to the console.
    /// </summary>
    public static void PrintEntities()
    {
        Console.Out.WriteLine();
        Console.Out.WriteLine( "Entities:" );

        foreach( KeyValuePair<InstanceID, Entity> entityTuple in s_GameEntities )
        {
            PrintEntity( entityTuple.Value );
        }

        Console.Out.WriteLine( s_GameEntities.Count + " entities" );
        Console.Out.WriteLine();
    }

    /// <summary>
    /// Adds a collision between two specified entities
    /// </summary>
    private static void AddCollision( Entity entity1, Entity entity2 )
    {
        //-- Only add a collision if the pair aren't already colliding
        if( GetCollision( entity1, entity2 ) == null )
        {
            //-- Send collision events
            entity1.HandleCollision( entity2 );
            entity2.HandleCollision( entity1 );

            //-- Add new collision
            s_Collisions.AddLast( new Tuple<InstanceID, InstanceID>( entity1.GetID(), entity2.GetID() ) );
        }
    }

    /// <summary>
    /// Removes all active collisions involving a specified entity or does nothing
    /// if no such collisions exist.
    /// </summary>
    private static void RemoveCollision( Entity entity )
    {
        LinkedListNode<Tuple<InstanceID, InstanceID>> collisionIter = s_Collisions.First;
        while( null != collisionIter )
        {
            //-- Cache next collision
            LinkedListNode<Tuple<InstanceID, InstanceID>> nextCollisionIter = collisionIter.Next;

            //-- Remove this collision if one of its colliders is the entity
            Tuple<InstanceID, InstanceID> collision = collisionIter.Value;
            if( (collision.Item1 == entity.GetID()) || (collision.Item2 == entity.GetID()) )
            {
                //-- Send collision end events
                Entity collider1 = GetEntity( collision.Item1 );
                Entity collider2 = GetEntity( collision.Item2 );

                if( null != collider1 )
                {
                    collider1.HandleCollisionEnd( collider2 );
                }

                if( null != collider2 )
                {
                    collider2.HandleCollisionEnd( collider1 );
                }

                //-- Remove the collision
                s_Collisions.Remove( collisionIter );
            }

            //-- Next collision
            collisionIter = nextCollisionIter;
        }
    }

    /// <summary>
    /// Removes active collision involving two specified entities or does nothing if
    /// no such collision exists.
    /// </summary>
    private static void RemoveCollision( Entity entity1, Entity entity2 )
    {
        Tuple<InstanceID, InstanceID> collision = GetCollision( entity1, entity2 );
        if( null != collision )
        {
            //-- Collision end events
            entity1.HandleCollisionEnd( entity2 );
            entity2.HandleCollisionEnd( entity1 );

            //-- Remove collision
            s_Collisions.Remove( collision );
        }
    }

    /// <summary>
    /// Returns an active collision involving two specified entities or null if
    /// no such collision exists.
    /// </summary>
    private static Tuple<InstanceID, InstanceID> GetCollision( Entity entity1, Entity entity2 )
    {
        foreach( Tuple<InstanceID, InstanceID> collision in s_Collisions )
        {
            bool colliding = false;
            colliding = colliding || ((collision.Item1 == entity1.GetID()) && (collision.Item2 == entity2.GetID()));
            colliding = colliding || ((collision.Item2 == entity1.GetID()) && (collision.Item1 == entity2.GetID()));

            if( colliding )
            {
                //-- Both members of the collision match the specified entities
                return collision;
            }
        }

        return null;
    }

    /// <summary>
    /// Adds a specified entity to the game world.
    /// </summary>
    private static void AddEntity( Entity entity )
    {
        //-- Print new entity
        Console.Out.Write( "Created entity: " );
        PrintEntity( entity );

        //-- Add the entity to the world
        s_GameEntities.Add( entity.GetID(), entity );
        entity.EnterWorld();

        //-- Print list
        Console.Out.WriteLine();
        PrintEntities();
    }

    /// <summary>
    /// Removes a specified entity from the game. This private function assumes that its parameter
    /// is a valid entity currently present in the game world.
    /// </summary>
    /// <param name="entity"></param>
    private static void RemoveEntity( Entity entity )
    {
        //-- Remove any collision that this entity may be involved with
        RemoveCollision( entity );
        
        //-- Remove the entity from the world
        entity.ExitWorld();
        s_GameEntities.Remove( entity.GetID() );

        //-- Print
        Console.Out.Write( "Removed entity: " );
        PrintEntity( entity );

        Console.Out.WriteLine();
        PrintEntities();
    }

    private static Entity GetEntity( InstanceID instanceID )
    {
        Entity result = null;
        if( instanceID != InstanceID.Invalid_IID )
        {
            s_GameEntities.TryGetValue( instanceID, out result );
        }
        return result;
    }

    /// <summary>
    /// Retrieves a pair of Entities corresponding to a pair of instance IDs. If both both instance IDs
    /// could be matched to a valid entity, the function returns true; otherwise it returns false.
    /// </summary>
    /// <param name="instanceID1">Instance ID of the first entity.</param>
    /// <param name="instanceID2">Instance ID of the second entity.</param>
    /// <param name="entity1">First entity or null.</param>
    /// <param name="entity2">Second entity or null.</param>
    /// <returns>Whether both instance IDs could be matched to valid entities.</returns>
    private static bool GetEntityPair( int instanceID1, int instanceID2, out Entity entity1, out Entity entity2 )
    {
        entity1 = null;
        entity2 = null;

        bool matchesFound = false;
        foreach( InstanceID instanceID in s_GameEntities.Keys )
        {
            //-- Match the first instance ID
            if( (null == entity1) && (instanceID == instanceID1) )
            {
                s_GameEntities.TryGetValue( instanceID, out entity1 );
            }

            //-- Match the second ID
            if( (null == entity2) && (instanceID == instanceID2) )
            {
                s_GameEntities.TryGetValue( instanceID, out entity2 );
            }

            //-- Break out of the loop early if possible
            if( (null != entity1) && (null != entity2) )
            {
                matchesFound = true;
                break;
            }
        }

        return matchesFound;
    }

    /// <summary>
    /// Prints the description for an individual entity on its own line.
    /// </summary>
    /// <param name="entity"></param>
    private static void PrintEntity( Entity entity )
    {
        Console.Out.WriteLine( entity.GetID() + " - " + entity.ToString() + " [" + entity.m_Type.ToString() + "]" );
    }
}
