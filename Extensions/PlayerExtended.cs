using ModCrafting.Managers;
using UnityEngine;

namespace ModCrafting.Extensions
{
    class PlayerExtended : Player
    {
        protected override void Start()
        {
            base.Start();
            new GameObject($"__{nameof(ModCrafting)}__").AddComponent<ModCrafting>();
            new GameObject($"__{nameof(StylingManager)}__").AddComponent<StylingManager>();
        }
    }
}
