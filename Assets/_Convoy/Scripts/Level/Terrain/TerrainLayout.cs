using UnityEngine;

namespace Convoy.Level
{
    /// <summary>
    /// Asset wrapper for <see cref="TerrainLayoutData"/> so designers tune the Páramo in the Inspector. The data lives in a
    /// plain class (not on the ScriptableObject itself) because the synthesis and its tests must run without creating
    /// Unity objects. New assets start from <see cref="ParamoLayoutDefaults"/>.
    /// </summary>
    [CreateAssetMenu(fileName = "TerrainLayout", menuName = "Convoy/Terrain Layout")]
    public sealed class TerrainLayout : ScriptableObject
    {
        public TerrainLayoutData data = ParamoLayoutDefaults.Create();
    }
}
