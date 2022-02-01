using UBOAT.Game.Core.Data;

namespace SinkingPhysicsOverhaul
{
    public class Settings
    {
        public static bool hpBar = new GameDataReference(Constants.HPBAR_DATA_PATH).GetBoolean();
    }

}
