using UnityEngine;

namespace ModCrafting
{
    class PlayerExtended : Player
    {
        protected override void Start()
        {
            base.Start();
            new GameObject($"__{nameof(ModCrafting)}__").AddComponent<ModCrafting>();
        }
    }
}
