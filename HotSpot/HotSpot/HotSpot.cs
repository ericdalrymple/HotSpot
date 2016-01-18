using System;
using System.Collections.Generic;
using System.Linq;

public class HotSpot
: Entity
{
    //
    //-- Definitions
    //
    public struct HotSpotTarget
    {
        public InstanceID TargetID { get; set; }
        public int AffectationCount { get; set; }
    }

    //
    //-- Constants
    //
    private static readonly Random RANDOM = new Random();

    //
    //-- Settings
    //
    public float m_InitialDelay = 0.0f;     //-- Time (in seconds) during which an entity must be in contact with this HotSpot before first being affected
    public float m_RepeatInterval = 1.0f;   //-- Interval (in seconds) at which this HotSpot affects its targets after the initial delay
    
    public int m_MinDamage = 1;             //-- Minimum amount of damage dealt by this HotSpot at each affectation
    public int m_MaxDamage = 1;             //-- Maximum amount of damage dealt by this HotSpot at each affectation
    public int m_RepeatCount = -1;          //-- Number of consecutive times that this HotSpot affects a given target [-1: infinite | 0: only once | n: n+1 times]

    public EffectType m_EffectType = EffectType.Health; //-- Type of damage affected by this HotSpot
    public EntityType[] m_TargetEntityTypes;            //-- Types of entities affected by this HotSpot

    //
    //-- Members
    //
    private bool m_Active = false;
    private SortedDictionary<double, LinkedList<HotSpotTarget>> m_JobCollection;

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
        if( null == collider )
        {
            //-- Don't accept null targets
            return;
        }

        if( !m_Active )
        {
            //-- Don't accept targets when inactive
            return;
        }

        if( !IsValidTarget( collider ) )
        {
            //-- This entity is not affected by this HotSpot
            return;
        }

        //-- Create a new target to schedule to be affected
        HotSpotTarget newTarget = new HotSpotTarget();
        newTarget.TargetID = collider.GetID();
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

        if( !IsValidTarget( collider ) )
        {
            //-- This entity is not affected by this HotSpot
            return;
        }

        //-- Remove target by ID
        RemoveTarget( collider.GetID() );
    }

    public void ProcessJob()
    {
        if( !m_Active )
        {
            //-- Don't process jobs or schedule more jobs if inactive
            return;
        }

        //-- Cache the current time
        double serverTime = GameSystem.GetTime();

        //-- Send a message to the next batch of targets in the queue and either discard them or re-queue them
        KeyValuePair<double, LinkedList<HotSpotTarget>> scheduledTargets = m_JobCollection.First();
        LinkedListNode<HotSpotTarget> targetIter = scheduledTargets.Value.First;
        while( null != targetIter )
        {
            //-- Cache target reference
            HotSpotTarget target = targetIter.Value;

            //-- Resolve the intensity of the effect
            int intensity = RANDOM.Next( m_MinDamage, m_MaxDamage );

            //-- Send the affect message to the target immediately
            bool success = GameSystem.SendAffectMessage( target.TargetID, m_EffectType, intensity );
            if( success )
            {
                //-- Target received the message; update its hit count unless infinite
                target.AffectationCount = target.AffectationCount + 1;

                //-- If the target hasn't reached its maximum hit count, then re-schedule it
                if( (0 > m_RepeatCount) || (target.AffectationCount < m_RepeatCount) )
                {
                    ScheduleTarget( target, serverTime + m_RepeatInterval );
                }
            }

            //-- Next scheduled target
            targetIter = targetIter.Next;
        }

        //-- Remove the batch of targets that we've just processed. This gets rid of any targets
        //   that have not been rescheduled either because they've been dealt all of their hits
        //   or because they've become unreachable.
        m_JobCollection.Remove( scheduledTargets.Key );

        //-- Bootstrap the next job if there are any left
        if( 0 < m_JobCollection.Count )
        {
            GameSystem.SendProcessJobMessage( GetID(), m_JobCollection.First().Key );
        }
    }

    private bool IsValidTarget( Entity target )
    {
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

    private void RemoveTarget( InstanceID targetInstanceID )
    {
        foreach( KeyValuePair<double, LinkedList<HotSpotTarget>> kvp in m_JobCollection )
        {
            //-- Iterate over the 
            LinkedListNode<HotSpotTarget> iter = kvp.Value.First;
            while( null != iter )
            {
                if( targetInstanceID == iter.Value.TargetID )
                {
                    kvp.Value.Remove( iter );
                    break;
                }

                iter = iter.Next;
            }

            //-- If the target list is empty, remove it from the dictionary
            if( 0 == kvp.Value.Count )
            {
                m_JobCollection.Remove( kvp.Key );
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
