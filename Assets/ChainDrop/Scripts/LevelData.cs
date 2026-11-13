using System;

namespace ChainDrop
{
    // Level files live in Resources/ChainDrop/levels.json.
    // World layout: ground top is y = 0, ceiling is y = 11, visible x is about -5.5 .. 5.5.

    [Serializable]
    public class CrateDef
    {
        public float x;        // horizontal position
        public float chain;    // chain length below the ceiling (world units)
        public float size;     // crate width/height (1.4 normal, 2.0+ big)
        public float swing;    // starting sideways speed; 0 = still, +/- = swinging pendulum
    }

    [Serializable]
    public class RobotDef
    {
        public string side;    // "L" or "R"
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
        public float playerX;
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
