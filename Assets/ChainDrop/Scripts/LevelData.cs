using System;

namespace ChainDrop
{
    // Level files live in Resources/ChainDrop/levels.json.
    // Arena (top-down floor): x = -5 .. 5, y = -7 .. 6. Height above the floor is separate ("h").

    [Serializable]
    public class CrateDef
    {
        public float x, y;     // floor position of the crate's shadow
        public float h;        // hanging height (higher = falls later). 3 low, 5 normal, 7 high
        public float size;     // 1.6 normal, 2.4+ big
        public float swing;    // swing amplitude in units, 0 = still
        public string axis;    // "x" or "y" swing direction
    }

    [Serializable]
    public class RobotDef
    {
        public float x, y;     // spawn point (just inside a wall = a door)
        public float delay;    // seconds after level start
        public string type;    // "walker", "runner", "tank"
    }

    [Serializable]
    public class LevelDef
    {
        public string name;
        public int theme;      // 0 factory, 1 mine, 2 harbor, 3 power plant, 4 robot base
        public int shots;      // ammo
        public int par;        // shots for 3 stars
        public float px, py;   // player start
        public string hint;
        public CrateDef[] crates;
        public RobotDef[] robots;
    }

    [Serializable]
    public class LevelPack
    {
        public LevelDef[] levels;
    }
}
