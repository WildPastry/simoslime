using UnityEngine.InputSystem;

namespace SimoSlime.Game
{
    /// <summary>Enter does each screen's main action; either Enter key counts.</summary>
    public static class EnterKey
    {
        public static bool WasPressedThisFrame()
        {
            Keyboard keyboard = Keyboard.current;
            return keyboard != null && (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame);
        }
    }
}
