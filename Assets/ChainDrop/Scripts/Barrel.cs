using UnityEngine;

namespace ChainDrop
{
    /// <summary>Explosive barrel. A falling crate or a nearby explosion sets it off.</summary>
    public class Barrel : MonoBehaviour
    {
        public bool Exploded { get; private set; }
        public Vector2 Ground => Util.G(transform.position);
        public const float Radius = 0.45f;
        public const float BlastRadius = 2.6f;

        public static Barrel Spawn(BarrelDef d, Transform parent, Theme th)
        {
            var go = new GameObject("Barrel");
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(d.x, 0, d.z);
            var b = go.AddComponent<Barrel>();

            var red = Util.Mat(Util.Hex("#c0392b"), 0.45f, 0.4f);
            var band = Util.Mat(new Color(0.08f, 0.08f, 0.09f), 0.5f, 0.6f);
            var sign = Util.Glow(Util.Hex("#ffd23f"), 1.6f);
            Util.Prim(PrimitiveType.Cylinder, "drum", go.transform, new Vector3(0, 0.55f, 0), new Vector3(0.85f, 0.55f, 0.85f), red);
            Util.Prim(PrimitiveType.Cylinder, "bandTop", go.transform, new Vector3(0, 0.85f, 0), new Vector3(0.88f, 0.04f, 0.88f), band);
            Util.Prim(PrimitiveType.Cylinder, "bandBot", go.transform, new Vector3(0, 0.25f, 0), new Vector3(0.88f, 0.04f, 0.88f), band);
            Util.Cube("warning", go.transform, new Vector3(0, 0.55f, -0.43f), new Vector3(0.36f, 0.3f, 0.02f), sign);
            Util.Prim(PrimitiveType.Cylinder, "cap", go.transform, new Vector3(0.18f, 1.12f, 0.1f), new Vector3(0.14f, 0.03f, 0.14f), band);

            var col = go.AddComponent<CapsuleCollider>();
            col.radius = Radius;
            col.height = 1.2f;
            col.center = new Vector3(0, 0.66f, 0);
            go.AddComponent<Obstacle>();
            return b;
        }

        public void MarkExploded()
        {
            Exploded = true;
            gameObject.SetActive(false);
        }
    }
}
