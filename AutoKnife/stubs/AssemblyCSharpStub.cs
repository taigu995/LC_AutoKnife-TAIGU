// Assembly-CSharp stub (Lethal Company game types)
namespace GameNetcodeStuff
{
    public class PlayerControllerB : UnityEngine.MonoBehaviour
    {
        public GrabbableObject currentlyHeldObjectServer;
        // FIX v1.0.3: playerInput field removed - doesn't exist in V81
        // Input is accessed via IngamePlayerSettings.Instance.playerInput instead
        public bool isPlayerDead;

        public void UseItemOnClient() { }
        public void UseItemOnClient(int slot) { }
    }
}

public class GrabbableObject : UnityEngine.MonoBehaviour
{
    public bool isHeld;
    public GameNetcodeStuff.PlayerControllerB playerHeldBy;
}

public class KnifeItem : GrabbableObject
{
    private float timeAtLastDamageDealt;
    private float timeAtLastHit;

    public void HitKnife() { }
    public void HitKnife(UnityEngine.Vector3 hitPoint) { }
}

public class IngamePlayerSettings : UnityEngine.MonoBehaviour
{
    public static IngamePlayerSettings Instance { get; set; }
    public UnityEngine.InputSystem.PlayerInput playerInput { get; set; }
}

public class StartOfRound : UnityEngine.MonoBehaviour
{
    public static StartOfRound Instance { get; set; }
}

public class RoundManager : UnityEngine.MonoBehaviour
{
    public static RoundManager Instance { get; set; }
}
