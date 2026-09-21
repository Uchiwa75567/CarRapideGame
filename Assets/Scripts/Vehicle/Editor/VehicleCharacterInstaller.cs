#if UNITY_EDITOR
using UnityEditor;

namespace CarRapide.EditorTools
{
    // Characters are bundled with their MIT notice. Reinstallation is deterministic and offline.
    public static class VehicleCharacterInstaller
    {
        [MenuItem("Car Rapide/Vehicle/Fix Existing Character Materials")]
        public static void FixExistingCharacters()
        {
            VehicleExperienceInstaller.PrepareMaterials();
            AssetDatabase.SaveAssets();
        }
    }
}
#endif
