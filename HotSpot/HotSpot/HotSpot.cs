using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// 
/// OVERVIEW:
/// 
///   This HotSpot uses a HashSet of InstanceIDs to track its colliders to benifit from HashSet's
///   efficient Insert, Remove and Check operations in addition to automatic guarding against duplicate
///   entries. All valid colliding entities are added to the 'm_CollidingEntities' HashSet upon collision
///   and removed from it upon collision end.
///   
///   
///   
/// 
/// ASSUMPTIONS:
/// 
///   - An assumption has been made that 'HandleCollision' will not get called twice for the same collider
///     unless there has been a call to 'HandleCollisionEnd' for that collider in between.
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

public class HotSpot
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

    /// <summary>
    /// Collection of all entities currently colliding with this HotSpot. This collection is not particularly
    /// useful for this specific implementation of HotSpot, but it is nonetheless a requirement for the test.
    /// </summary>
    private HashSet<InstanceID> m_CollidingEntities = new HashSet<InstanceID>();

    /// <summary>
    /// Collection of jobs to be processed by this HotSpot in the form of target lists sorted by time.
    /// </summary>
    private SortedDictionary<double, LinkedList<HotSpotTarget>> m_JobCollection = new SortedDictionary<double, LinkedList<HotSpotTarget>>();



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
        //-- Clear all jobs
        m_JobCollection.Clear();

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

        //-- Track the colliding entity
        m_CollidingEntities.Add( collider.GetID() );

        if( !IsAffectedTarget( collider ) )
        {
            //-- This entity is not affected by this HotSpot
            return;
        }

        //-- Create a new target to schedule to be affected
        HotSpotTarget newTarget = new HotSpotTarget();
        newTarget.TargetEntityID = collider.GetID();
        newTarget.AffectationCount = 0;

        //-- Schedule the new target
        ScheduleTarget( newTarget, GameSystem.GetTime() + m_InitialDelay );
    }
    
    public override void HandleCollisionEnd( Entity collider )
    {
        if( null == collider )
        {
            //-- Ignore null targets
            return;
        }

        //-- Track colliding entity
        m_CollidingEntities.Remove( collider.GetID() );
    }

    /// <summary>
    /// Affects all of the targets in the next scheduled job. If an affected target must be
    /// affected by this HotSpot again, it is re-scheduled in a future job. Otherwise, processed
    /// targets do not get rescheduled and are thus no longer kept track of by this HotSpot's
    /// job collection.
    /// </summary>
    public void ProcessJob()
    {
        if( !m_Active )
        {
            //-- Don't process jobs or schedule more jobs if inactive
            return;
        }

        if( 0 == m_JobCollection.Count )
        {
            //-- No jobs to process (unlikely to be reached since this function
            //   would not be called if there were no jobs)
            return;
        }

        //-- Cache the current time
        double serverTime = GameSystem.GetTime();

        //-- Send a message to the batch of targets in the next job and either reschedule them or discard them
        KeyValuePair<double, LinkedList<HotSpotTarget>> nextJob = m_JobCollection.First();
        LinkedListNode<HotSpotTarget> targetIter = nextJob.Value.First;
        while( null != targetIter )
        {
            //-- Cache target reference
            HotSpotTarget target = targetIter.Value;

            //-- Only affect the target if this HotSpot is still colliding with it
            if( m_CollidingEntities.Contains( target.TargetEntityID ) )
            {
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

                    //-- If the target hasn't reached its maximum hit count, then re-schedule it
                    if( (0 > m_RepeatCount) || (target.AffectationCount < m_RepeatCount) )
                    {
                        ScheduleTarget( target, serverTime + m_RepeatInterval );
                    }
                }
            }

            //-- Next target in the job
            targetIter = targetIter.Next;
        }

        //-- Remove the batch of targets that we've just processed. This gets rid of any targets
        //   that have not been rescheduled either because they've been dealt all of their hits
        //   or because they've become unreachable.
        m_JobCollection.Remove( nextJob.Key );

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

    private void RemoveTargetFromJobs( InstanceID targetInstanceID )
    {
        foreach( KeyValuePair<double, LinkedList<HotSpotTarget>> kvp in m_JobCollection )
        {
            //-- Iterate over the targets for this job
            LinkedListNode<HotSpotTarget> targetIter = kvp.Value.First;
            while( null != targetIter )
            {
                if( targetInstanceID == targetIter.Value.TargetEntityID )
                {
                    //-- We found the matching target, remove it from the job
                    kvp.Value.Remove( targetIter );

                    //-- The system is designed in such a way that every unique target
                    //   is only in the job collection once, so we can stop here
                    return;
                }

                //-- Next job target
                targetIter = targetIter.Next;
            }
        }
    }

    private void ScheduleTarget( HotSpotTarget target, double time )
    {
        bool noTargets = (0 == m_JobCollection.Count);

        //-- Schedule the new target; start by checking if there are any other targets scheduled at the same time
        LinkedList<HotSpotTarget> simultaneousTargets;
        bool found = m_JobCollection.TryGetValue( time, out simultaneousTargets );
        if( !found )
        {
            //-- No other targets were registered at that time slot, so let's register a
            //   new list of targets for that time
            simultaneousTargets = new LinkedList<HotSpotTarget>();
            m_JobCollection.Add( time, simultaneousTargets );
        }

        //-- Add our new target to the list for its time slot
        simultaneousTargets.AddLast( target );

        //-- If this is the first item to be queued, then we must schedule an
        //   update to affect it
        if( noTargets )
        {
            GameSystem.SendProcessJobMessage( GetID(), time );
        }
    }
}
