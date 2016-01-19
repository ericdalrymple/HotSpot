using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// 
/// OVERVIEW:
/// 
///   Using a List as a target pool in order to use index access for targets O(1).
///   
///   Using a Queue to keep track of free spots in the target pool as they become free so that
///   new targets don't need to seek the pool for free spots.
///   
///   Using a Dictionary to map Entity instance IDs to pool indices so that we can clear
///   a pool slot in O(log n) instead of O(n) on 'HandleCollisionEnd'. There is one entry
///   in the map for every active HotSpot target.
///   
///   No polling. The affecting of targets is event based.
/// 
/// 
/// ASSUMPTIONS:
/// 
///   - An assumption has been made that 'HandleCollision' will not get called twice for the same collider
///     unless there has been a call to 'HandleCollisionEnd' for that collider in between.
/// 
///   - Assuming that the SendMessage calls are not blocking event when specifying a time in the future.
///   
/// 
/// FEATURES:
/// 
///   In addition meeting the criteria specified in the test instructions, this HotSpot implementation
///   sports the following features:
/// 
///   - Initial delay:   The designer may specify an amount of time between the time a target
///                      collides with the HotSpot and the time the HotSpot starts affecting them.
///                      For example, you may not want the player to run out of air immediately
///                      upon submerging under water. After this initial interval, the HotSpot
///                      deals affects its targets at a separately specified repeat interval.
/// 
///   - Repeat Count:    The designer may specify an exact number of times for the HotSpot to affect
///                      each of its targets. By default, the repeat count is infinite (-1).
///                    
///   - Type Filtering:  The designer may choose a subset of entity types that can be affected by
///                      a HotSpot via 'm_TargetEntityTypes'. If none are specified, the HotSpot
///                      affects all entity types. I do not believe it should be the HotSpot class'
///                      job to know about which types of entities should be affected by which
///                      types of effects (e.g. should not know that items don't have stamina or
///                      something like that).
/// 
/// 
/// MESSAGES:
/// 
///   In addition to the 'SendAffectMessage' function mentioned in the test documentation, I've
///   assumed support of the following messages:
/// 
///   - SendProcessJobMessage( InstanceID target, double time = -1.0f ): 
///         Sends a 'ProcessJob' message. This is a message that the HotSpot sends to itself in
///       order process its targets in an event-based manner (as opposed to polling based). Upon
///       receiving this message, a HotSpot will process its earliest scheduled job and will
///       send itself this message again at the time when its next job is to be performed if
///       applicable.
/// 
/// 
/// REDESIGNS:
/// 
///     Initially, I was only keeping track of the HotSpot's targets through the job collection
///   and I was removing the targets from the jobs right away when I was getting a
///   'HandleCollisionEnd' message. However this was an O(n) operation that I wanted to avoid.
///   By tracking the entities that are colliding with the HotSpot separately, I was simply
///   able to check whether the HotSpot was still colliding with a target at the moment when
///   I was about to affect it and filter it out of the job collection at that time at a cost of O(1).
///   In addition to being more efficient, this allowed me to fulfill the criteria of having to
///   keep track of all the colliders and would make things easier for someone debugging this class.
/// 
/// </summary>

public class HotSpot2
: Entity
{
    //
    //-- Definitions
    //

    /// <summary>
    /// Handy tuple structure representing a target affected by a HotSpot. This
    /// is useful if we need to keep track of HotSpot instance specific data for
    /// and entity.
    /// </summary>
    private struct HotSpotTarget
    {
        public InstanceID TargetEntityID { get; set; }
        public bool Active { get; set; }
        public bool Scheduled { get; set; }
        public int AffectationCount { get; set; }
    }

    //
    //-- Constants
    //
    private static readonly Random RANDOM = new Random();

    //
    //-- Settings
    //
    public float m_InitialDelay = 0.0f;     //-- Time (in seconds) during which an entity must be in contact with this HotSpot before first being affected by it
    public float m_RepeatInterval = 1.0f;   //-- Interval (in seconds) at which this HotSpot affects its targets after the initial delay
    
    public int m_MinDamage = 1;             //-- Minimum amount of damage dealt by this HotSpot at each affectation
    public int m_MaxDamage = 1;             //-- Maximum amount of damage dealt by this HotSpot at each affectation
    public int m_RepeatCount = -1;          //-- Number of consecutive times that this HotSpot affects a given target [-1: infinite | 0: only once | n: n+1 times]

    public EffectType m_EffectType = EffectType.Health;  //-- Type of damage affected by this HotSpot
    public EntityType[] m_TargetEntityTypes;       //-- Types of entities affected by this HotSpot



    //
    //-- Members
    //

    /// <summary>
    /// Whether this HotSpot is currently active.
    /// </summary>
    private bool m_Active = false;
    
    private Dictionary<InstanceID, int> m_TargetLookup = new Dictionary<InstanceID, int>();
    private List<HotSpotTarget> m_TargetPool = new List<HotSpotTarget>();
    private Queue<int> m_FreeSlots = new Queue<int>();

    //
    //-- Body
    //
    public override void EnterWorld()
    {
        //-- Activate this HotSpot as it enters the game world
        m_Active = true;
    }

    public override void ExitWorld()
    {
        //-- Clear targets
        m_TargetLookup.Clear();
        m_TargetPool.Clear();
        m_FreeSlots.Clear();

        //-- Turn off the HotSpot
        m_Active = false;
    }

    /// <summary>
    /// The implementation of this method code assumes that this function does
    /// not get called twice for the same entity unless there is a 'HandleCollisionEnd'
    /// call for that entity in between.
    /// </summary>
    public override void HandleCollision( Entity collider )
    {
        if( (null == collider) || (collider.GetID() == InstanceID.Invalid_IID) )
        {
            //-- Don't accept null or invalid targets
            return;
        }

        if( !m_Active )
        {
            //-- Don't accept targets when inactive
            return;
        }
        
        if( IsAffectedTarget( collider ) )
        {
            int newTargetSlotIndex = -1;
            if( 0 < m_FreeSlots.Count )
            {
                //-- There's an expired target we can use
                int slotIndex = m_FreeSlots.Dequeue();

                //-- Re-initilize expired target with new data
                HotSpotTarget target = m_TargetPool[slotIndex];
                {
                    target.TargetEntityID = collider.GetID();
                    target.AffectationCount = 0;
                    target.Active = true;
                }

                newTargetSlotIndex = slotIndex;
            }
            else
            {
                //-- There was no more room in the pool, so grow it. Create a new
                //   target and append it to the target pool
                HotSpotTarget newTarget = new HotSpotTarget();
                newTarget.TargetEntityID = collider.GetID();
                newTarget.AffectationCount = 0;
                newTarget.Active = true;

                //-- Add new slot to target pool
                m_TargetPool.Add( newTarget );

                newTargetSlotIndex = m_TargetPool.Count - 1;
            }

            //-- Register the new target to our lookup dictionary
            m_TargetLookup[collider.GetID()] = newTargetSlotIndex;

            //-- Immediately schedule the target to be affected
            ScheduleTarget( newTargetSlotIndex, GameSystem.GetTime() + m_InitialDelay );
        }
    }
    
    public override void HandleCollisionEnd( Entity collider )
    {
        if( null == collider )
        {
            //-- Ignore null targets
            return;
        }
        
        if( IsAffectedTarget( collider ) )
        {
            //-- Check if this entity is one of our targets
            int targetSlotIndex = -1;
            if( m_TargetLookup.TryGetValue( collider.GetID(), out targetSlotIndex ) )
            {
                //-- If so, deactivate it
                
            }
        }
    }

    /// <summary>
    /// Affects all of the targets in the next scheduled job. If an affected target must be
    /// affected by this HotSpot again, it is re-scheduled in a future job. Otherwise, processed
    /// targets do not get rescheduled and are thus no longer kept track of by this HotSpot's
    /// job collection.
    /// </summary>
    public void ProcessTarget( int targetID )
    {
        if( !m_Active )
        {
            //-- Don't process jobs or schedule more jobs if inactive
            return;
        }

        if( (0 > targetID) || (targetID <= m_TargetPool.Count) )
        {
            //-- Invalid index
            return;
        }
        
        HotSpotTarget target = m_TargetPool[targetID];
        if( target.Active && target.Scheduled )
        {
            //-- Unschedule the target
            target.Scheduled= false;

            //-- Resolve the intensity of the effect
            int intensity = RANDOM.Next( m_MinDamage, m_MaxDamage );

            //-- Send the affect message to the target immediately
            bool success = GameSystem.SendAffectMessage( target.TargetEntityID, m_EffectType, intensity );
            if( success )
            {
                //-- Target received the message; update its hit count unless this HotSpot deals infinite hits
                if( 0 <= m_RepeatCount )
                {
                    target.AffectationCount = target.AffectationCount + 1;
                }

                if( (0 > m_RepeatCount) || (target.AffectationCount < m_RepeatCount) )
                {
                    //-- If the target hasn't reached its maximum hit count, then re-schedule it{
                    ScheduleTarget( targetID, GameSystem.GetTime() + m_RepeatInterval );
                }
                else
                {
                    //-- Target has reached max hit count, disable it
                    target.Active = false;
                }
            }
        }

        //-- Bootstrap the next job if there are any left
        if( 0 < m_JobCollection.Count )
        {
            GameSystem.SendProcessJobMessage( GetID(), m_JobCollection.First().Key );
        }
    }

    /// <summary>
    /// Determines whether a specified entity is affected by this HotSpot.
    /// </summary>
    /// <param name="target">an Entity</param>
    /// <returns>'true' if this HotSpot affects the entity, 'false' otherwise</returns>
    private bool IsAffectedTarget( Entity target )
    {
        if( (null == m_TargetEntityTypes) || (0 == m_TargetEntityTypes.Length) )
        {
            //-- Affect all entity types by default
            return true;
        }

        bool valid = false;

        foreach( EntityType entityType in m_TargetEntityTypes )
        {
            switch( entityType )
            {
                case EntityType.AI:
                {
                    valid = target.IsAI();
                    break;
                }

                case EntityType.Creature:
                {
                    valid = target.IsCreature();
                    break;
                }

                case EntityType.Item:
                {
                    valid = target.IsItem();
                    break;
                }

                case EntityType.Player:
                {
                    valid = target.IsPlayer();
                    break;
                }
            }

            if( valid )
            {
                return true;
            }
        }

        return false;
    }

    private void RemoveTarget( int targetSlotIndex )
    {
        HotSpotTarget target = m_TargetPool[targetSlotIndex];
        target.TargetEntityID = InstanceID.Invalid_IID;
        target.Active = false;

        //-- And remove the lookup for it
        m_TargetLookup.Remove( target.GetID() );

        //-- Keep track of the pool slot we just freed up
        m_FreeSlots.Enqueue( targetSlotIndex );
    }

    private void ScheduleTarget( int targetSlotIndex, double time )
    {
        HotSpotTarget scheduledTarget = m_TargetPool[targetSlotIndex];
        if( scheduledTarget.Active && !scheduledTarget.Scheduled )
        {
            //-- Schedule the target for affectation and mark it as scheduled
            GameSystem.SendProcessTargetMessage( GetID(), targetSlotIndex, time );
            scheduledTarget.Scheduled = true;
        }
    }
}
