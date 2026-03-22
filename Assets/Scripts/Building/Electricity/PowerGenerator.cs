using UnityEngine;

namespace Voidborne.Building.Electricity
{
    /// <summary>
    /// Base class for all power generators. Sets powerDraw = 0 and exposes
    /// IsGenerating so subclasses control whether they actually produce power.
    /// </summary>
    public abstract class PowerGenerator : PowerNode
    {
        protected bool _isGenerating;

        protected virtual void Awake()
        {
            powerDraw = 0f;
            priority  = 0; // Generators are always "on" when generating.
        }

        protected virtual void Start()
        {
            PowerNetworkManager.Instance?.RegisterNode(this);
        }

        protected virtual void OnDestroy()
        {
            PowerNetworkManager.Instance?.UnregisterNode(this);
        }

        public override float GetCurrentOutput() => _isGenerating ? powerOutput : 0f;
        public override float GetCurrentDraw()   => 0f;
    }
}
