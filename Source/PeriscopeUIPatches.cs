
using Harmony;
using UBOAT.Game.UI.Periscope;
using UnityEngine.UI;

namespace SinkingPhysicsOverhaul
{

    public class PeriscopeUIPatches
    {
        [HarmonyPatch(typeof(PeriscopeUI), "UpdateParameters")]
        public class UpdateParameters_Patch
        {
            private static void Postfix(ref Slider ___healthBar)
            {                
                if (___healthBar.IsActive() != Settings.hpBar)
                    ___healthBar.gameObject.SetActive(Settings.hpBar); 
            }

        } 
    }

}