public enum EffectType
: int
{
      Health = 0
    , Stamina
    , Mana
}

public class Player
: Entity
{
    public Player()
    {
        m_Type = EntityType.Player;
    }
}

public class AI
: Entity
{
    public AI()
    {
        m_Type = EntityType.AI;
    }
}

public class Creature
: Entity
{
    public Creature()
    {
        m_Type = EntityType.Creature;
    }
}

public class Item
: Entity
{
    public Item()
    {
        m_Type = EntityType.Item;
    }
}
