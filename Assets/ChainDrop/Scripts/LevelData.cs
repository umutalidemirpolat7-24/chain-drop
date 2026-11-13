using System;

namespace ChainDrop
{
    // Level files live in Resources/ChainDrop/levels.json.
    // 3D arena floor: x = -5 .. 5 (left/right), z = -7 .. 6 (near/far). Height is y.

    [Serializable]
    public class CrateDef
    {
        public float x, z;     // floor position under the crate (the red landing marker)
        public float h;        // height of the crate's bottom above the floor. 3 low, 4-5 normal, 7 high
        public float size;     // 2.0 normal, 2.8+ big
        public float swing;    // swing amplitude in units, 0 = still
        public string axis;    // "x" or "z" swing direction
    }

    [Serializable]
    public class RobotDef
    {
        public float x, z;     // spawn point (door, just inside a wall)
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
        public float px, pz;   // player start
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
