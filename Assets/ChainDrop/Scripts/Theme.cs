using UnityEngine;

namespace ChainDrop
{
    /// <summary>Color palette per world. Swap these for real art later.</summary>
    public class Theme
    {
        public string name;
        public Color bg, bgFar, ground, groundTop, metal, metalDark, chain, robot, player, accent;

        static Theme Make(string name, string bg, string bgFar, string ground, string groundTop, string metal,
                          string metalDark, string chain, string robot, string player, string accent)
        {
            return new Theme
            {
                name = name, bg = Util.Hex(bg), bgFar = Util.Hex(bgFar), ground = Util.Hex(ground), groundTop = Util.Hex(groundTop),
                metal = Util.Hex(metal), metalDark = Util.Hex(metalDark), chain = Util.Hex(chain), robot = Util.Hex(robot),
                player = Util.Hex(player), accent = Util.Hex(accent)
            };
        }

        static Theme[] all;

        public static Theme Get(int i)
        {
            if (all == null)
                all = new[]
                {
                    Make("FABRİKA",   "#26303d", "#1d252f", "#3a4048", "#5b636d", "#9aa3ad", "#5a6370", "#c9ced4", "#4f8fd6", "#3fae6a", "#f2b632"),
                    Make("MADEN",     "#2e2620", "#231c17", "#4a3a2c", "#6b543e", "#a8957f", "#5e4d3c", "#cbbba5", "#c0563c", "#3fae6a", "#e8a33c"),
                    Make("LİMAN",     "#1f3442", "#162733", "#34444f", "#55666f", "#d08a3a", "#7a4f24", "#c9ced4", "#4fc3c0", "#e6e6e6", "#ff6a3d"),
                    Make("SANTRAL",   "#1c1f2e", "#141623", "#2e3244", "#4b5068", "#8c93b0", "#4a4f68", "#b8bfd8", "#e0d24f", "#3fae6a", "#5ee0ff"),
                    Make("ROBOT ÜSSÜ","#2a1d24", "#1f141a", "#3d2c33", "#5e4650", "#a99aa1", "#5e4d55", "#d0c4c9", "#d64f6a", "#3fae6a", "#ff3b6b"),
                };
            return all[Mathf.Clamp(i, 0, all.Length - 1)];
        }
    }
}
