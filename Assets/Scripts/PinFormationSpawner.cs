using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

// Editor-only convenience for stamping a triangle formation of bowling pins
// at edit time -- placing dozens by hand isn't worth doing manually.
// Right-click this component's header -> "Spawn Formation". Produces normal
// prefab instances as children, so nothing about this is runtime; each pin
// can still be nudged or deleted individually afterward like any other
// GameObject.
public class PinFormationSpawner : MonoBehaviour
{
    [SerializeField] private GameObject pinPrefab;
    [SerializeField] private int rows = 4;
    [SerializeField] private float spacing = 0.6f;

#if UNITY_EDITOR
    // Lets other editor tooling (e.g. RaceManager's corner-detection
    // auto-placement) configure a spawner it just created programmatically.
    public void Configure(GameObject prefab, int rowCount, float spacingValue)
    {
        pinPrefab = prefab;
        rows = rowCount;
        spacing = spacingValue;
    }

    [ContextMenu("Spawn Formation")]
    public void SpawnFormation()
    {
        if (pinPrefab == null)
        {
            Debug.LogWarning("PinFormationSpawner: assign a Pin Prefab first.");
            return;
        }

        ClearFormation();

        for (int row = 0; row < rows; row++)
        {
            int pinsInRow = row + 1;
            float rowOffset = -(pinsInRow - 1) * spacing * 0.5f;

            for (int i = 0; i < pinsInRow; i++)
            {
                Vector3 localPosition = new Vector3(rowOffset + i * spacing, 0f, row * spacing);
                GameObject pin = (GameObject)PrefabUtility.InstantiatePrefab(pinPrefab, transform);
                pin.transform.localPosition = localPosition;
                pin.transform.localRotation = Quaternion.identity;
                Undo.RegisterCreatedObjectUndo(pin, "Spawn Pin Formation");
            }
        }
    }

    [ContextMenu("Clear Formation")]
    private void ClearFormation()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Undo.DestroyObjectImmediate(transform.GetChild(i).gameObject);
        }
    }
#endif
}
