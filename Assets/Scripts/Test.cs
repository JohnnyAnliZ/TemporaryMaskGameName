using UnityEngine;
using UnityEngine.InputSystem;

public class TestPortal : MonoBehaviour
{
    private InputSystem_Actions inputActions;
    public Material mat;
    void Awake()
    {
        inputActions = new InputSystem_Actions();
    }

    void OnEnable()
    {
        inputActions.Enable();

        // Subscribe to input events
        inputActions.Player.Click.performed += OnClick;
        inputActions.Player.ClearPortals.performed += OnClearPortals;
    }

    void OnDisable()
    {
        // Unsubscribe from events
        inputActions.Player.Click.performed -= OnClick;
        inputActions.Player.ClearPortals.performed -= OnClearPortals;

        inputActions.Disable();
    }

    void OnClick(InputAction.CallbackContext context)
    {
        // Get mouse position
        Vector2 mousePos = inputActions.Player.PointerPosition.ReadValue<Vector2>();

        //DualSceneManager.Instance.AddRevealPortal(mousePos, 100f);
        Debug.Log($"Portal added at {mousePos}");
        
        //float isBaseCamera = mat.GetFloat("_IsBaseCamera");
        //mat.SetFloat("_IsBaseCamera", 1.0f - isBaseCamera);
    }

    void OnClearPortals(InputAction.CallbackContext context)
    {
        DualSceneManager.Instance.ClearPortals();
        Debug.Log("Portals cleared");
    }
}