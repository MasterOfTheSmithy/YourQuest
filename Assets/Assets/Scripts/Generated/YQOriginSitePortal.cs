using UnityEngine;

[DisallowMultipleComponent]
public sealed class YQOriginSitePortal : MonoBehaviour
{
    private static float nextAllowedTransitionTime;

    private Vector3 destination;
    private Quaternion destinationFacing = Quaternion.identity;
    private string transitionName = string.Empty;

    public void Configure(
        Vector3 newDestination,
        Vector3 faceDirection,
        string newTransitionName)
    {
        // note: Portals move the one authoritative player between reviewed surface and transition-only interior sites; no parallel player is created.
        destination = newDestination;
        destinationFacing = faceDirection.sqrMagnitude > 0.001f
            ? Quaternion.LookRotation(faceDirection.normalized, Vector3.up)
            : Quaternion.identity;
        transitionName = newTransitionName ?? string.Empty;

        BoxCollider trigger = GetComponent<BoxCollider>();
        // note: Unity's destroyed-object null is not respected by C#'s null-coalescing operator, so use Unity's explicit null check before adding the trigger.
        if (trigger == null)
            trigger = gameObject.AddComponent<BoxCollider>();

        if (trigger == null)
        {
            Debug.LogError(
                "[YQOriginSitePortal] Could not create transition trigger on " +
                gameObject.name + ".");
            return;
        }

        trigger.isTrigger = true;
        trigger.center = new Vector3(0f, 1.2f, 0f);
        trigger.size = new Vector3(2.2f, 2.6f, 2.2f);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (Time.unscaledTime < nextAllowedTransitionTime || other == null)
            return;

        YQInvestorPlayerMotor motor =
            other.GetComponentInParent<YQInvestorPlayerMotor>();

        if (motor == null || !motor.IsAuthoritative)
            return;

        TeleportAuthoritativePlayer(motor.gameObject);
    }

    private void TeleportAuthoritativePlayer(GameObject player)
    {
        nextAllowedTransitionTime = Time.unscaledTime + 1.25f;
        CharacterController controller =
            player.GetComponent<CharacterController>();
        bool controllerEnabled = controller != null && controller.enabled;
        Rigidbody body = player.GetComponent<Rigidbody>();

        if (controller != null)
            controller.enabled = false;

        if (body != null)
        {
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.position = destination;
            body.rotation = destinationFacing;
        }

        player.transform.SetPositionAndRotation(
            destination,
            destinationFacing);

        if (controller != null)
            controller.enabled = controllerEnabled;

        // note: The Rigidbody, transform, and CharacterController now share the destination pose; avoid synchronizing every collider in the generated world for one portal transition.
        // note: Transition logging names the authored destination without exposing prefab paths or mutable asset authority.
        Debug.Log("[YQOriginSitePortal] ENTERED " + transitionName);
    }
}
