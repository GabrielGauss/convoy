using Convoy.Level;
using UnityEngine;

namespace Convoy.Presentation
{
    /// <summary>
    /// Dust-storm look for the Páramo (TR-01c, D-031): reads this run's <see cref="WorldVariation"/> from the
    /// <see cref="WorldVariationRunner"/> and sets the scene fog (exponential-squared; URP Lit and Terrain Lit
    /// shaders read <see cref="RenderSettings"/> fog). Presentation reads state only (D-007): it writes nothing but
    /// RenderSettings fog and never changes the simulation. Runs once; no per-frame cost.
    /// </summary>
    /// <remarks>
    /// <c>[DefaultExecutionOrder(-150)]</c>: after the runner (-200) and before default-order scripts, so the very first
    /// rendered frame already has the right fog. <see cref="WorldVariationRunner.EnsureApplied"/> makes it correct even
    /// if someone changes the order.
    /// </remarks>
    [DefaultExecutionOrder(-150)]
    [DisallowMultipleComponent]
    public sealed class ParamoAtmosphere : MonoBehaviour
    {
        [SerializeField] private WorldVariationRunner _runner;
        [Tooltip("Fog colours/densities. Empty = the runner's config.")]
        [SerializeField] private WorldVariationConfig _config;

        private void Awake()
        {
            if (_runner == null)
            {
                Debug.LogError($"{name}: ParamoAtmosphere needs a WorldVariationRunner; fog left as authored.", this);
                return;
            }
            _runner.EnsureApplied();
            WorldVariationConfig config = _config != null ? _config : _runner.Config;
            if (!_runner.IsApplied || config == null) return; // the runner already logged why

            bool storm = _runner.Current.DustStorm;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = storm ? config.stormFogColor : config.clearFogColor;
            RenderSettings.fogDensity = storm ? config.stormFogDensity : config.clearFogDensity;
        }
    }
}
