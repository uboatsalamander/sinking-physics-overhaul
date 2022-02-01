
using Harmony;
using UBOAT.Game.Scene.Entities;

namespace SinkingPhysicsOverhaul
{
    public class ReportSunkShipsPatches 
    {

        [HarmonyPatch(typeof(ReportSunkShips.Watcher), "Observator_ObservationRemoved")]
        public class Observator_ObservationRemoved_Patch
        {
            /*
             * Prevents Target_Destroyed event listener from being detached on targets that were lost.
             * This should fix the issues with not accounted tonnage and alert staying enabled forever after loading a game state.
             */
            private static bool Prefix(Observator observator, DirectObservation observation)
            {
                if (observator.SandboxEntity.IsPlayerShip)
                {                    
                    if (observation.Entity.SandboxEntity != null && observation.Entity.ReceivedDamages.Count > 0)
                    {                        
                        return false;
                    }
                        
                }
                // make sure we only stop further execution when really necessary
                return true;
            }
        }

    }

}
