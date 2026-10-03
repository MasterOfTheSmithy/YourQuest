using UnityEngine;

// note: A full-menu sibling backdrop follows this owner's visibility without affecting quick UI or gameplay pause tokens.
public sealed class YQBlueglassPauseBackdrop : MonoBehaviour
{
    private GameObject _backdrop;
    // note: Binding can happen after OnEnable; synchronize immediately even when the full root was already active.
    public GameObject Backdrop
    {
        get => _backdrop;
        set { _backdrop=value; if (_backdrop!=null) _backdrop.SetActive(isActiveAndEnabled); }
    }
    private void OnEnable() { if (Backdrop!=null) Backdrop.SetActive(true); }
    private void OnDisable() { if (Backdrop!=null) Backdrop.SetActive(false); }
}
