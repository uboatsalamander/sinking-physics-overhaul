
using Harmony;
using UBOAT.Game.UI;
using UnityEngine.UI;

namespace SinkingPhysicsOverhaul
{

    public class EntitySelectionUIPatches
    {

        [HarmonyPatch(typeof(EntitySelectionUI), "Update")]
        public class Update_Patch
        {

            private static void Postfix(ref Slider ___healthBar, ref Slider ___projectedHealthBar)
            {

                if (___healthBar.IsActive() != Settings.hpBar)
                    ___healthBar.gameObject.SetActive(Settings.hpBar);
                if (___projectedHealthBar.IsActive() != Settings.hpBar)
                    ___projectedHealthBar.gameObject.SetActive(Settings.hpBar);
            }
        }

        [HarmonyPatch(typeof(EntitySelectionUI), "Update")]
        public class Open_Patch
        {
            private static void Postfix(ref Slider ___healthBar, ref Slider ___projectedHealthBar)
            {
        
                if (___healthBar.IsActive() != Settings.hpBar)
                    ___healthBar.gameObject.SetActive(Settings.hpBar);
                if (___projectedHealthBar.IsActive() != Settings.hpBar)
                    ___projectedHealthBar.gameObject.SetActive(Settings.hpBar);

            }

        }

    }
}
