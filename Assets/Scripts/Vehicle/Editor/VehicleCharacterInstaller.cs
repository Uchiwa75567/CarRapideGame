#if UNITY_EDITOR
using System;
using System.IO;
using System.Net.Http;
using UnityEditor;
using UnityEngine;

namespace CarRapide.EditorTools
{
    public static class VehicleCharacterInstaller
    {
        private const string CharacterFolder = "Assets/Resources/CarRapide/Characters";

        private static readonly DownloadItem[] Characters =
        {
            new DownloadItem(
                "Chauffeur",
                "https://raw.githubusercontent.com/google/valid-avatar-library/main/Avatars/Black/Black_M_1_Casual.fbx",
                CharacterFolder + "/Black_M_1_Casual.fbx"),
            new DownloadItem(
                "Apprenti / receveur",
                "https://raw.githubusercontent.com/google/valid-avatar-library/main/Avatars/Black/Black_M_2_Casual.fbx",
                CharacterFolder + "/Black_M_2_Casual.fbx")
        };

        [MenuItem("Car Rapide/Vehicle/Download / Fix Driver & Receiver")]
        public static async void DownloadAndFixCharacters()
        {
            bool confirmed = EditorUtility.DisplayDialog(
                "Car Rapide — personnages",
                "Télécharger si nécessaire puis reconfigurer les deux avatars VALID avec textures, matériaux et rig Humanoid ?",
                "Continuer",
                "Annuler");

            if (!confirmed)
            {
                return;
            }

            try
            {
                Directory.CreateDirectory(CharacterFolder);

                using HttpClient client = new HttpClient
                {
                    Timeout = TimeSpan.FromMinutes(2)
                };
                client.DefaultRequestHeaders.UserAgent.ParseAdd("CarRapideGame/1.1");

                for (int i = 0; i < Characters.Length; i++)
                {
                    DownloadItem item = Characters[i];

                    EditorUtility.DisplayProgressBar(
                        "Car Rapide — personnages",
                        $"Préparation : {item.Label} ({i + 1}/{Characters.Length})",
                        (i + 0.2f) / Characters.Length);

                    if (!File.Exists(item.AssetPath))
                    {
                        byte[] bytes = await client.GetByteArrayAsync(item.Url);
                        File.WriteAllBytes(item.AssetPath, bytes);
                    }
                }

                AssetDatabase.Refresh();

                foreach (DownloadItem item in Characters)
                {
                    ConfigureHumanoidAndMaterials(item.AssetPath);
                }

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                EditorUtility.ClearProgressBar();
                EditorUtility.DisplayDialog(
                    "Car Rapide — personnages",
                    "Terminé. Les personnages ont été réimportés avec leurs textures et le rig Humanoid.",
                    "OK");
            }
            catch (Exception exception)
            {
                EditorUtility.ClearProgressBar();
                Debug.LogException(exception);
                EditorUtility.DisplayDialog(
                    "Erreur personnages",
                    "Impossible de télécharger/importer les personnages.\n\n" + exception.Message,
                    "OK");
            }
        }

        [MenuItem("Car Rapide/Vehicle/Fix Existing Character Materials")]
        public static void FixExistingCharacters()
        {
            int fixedCount = 0;

            foreach (DownloadItem item in Characters)
            {
                if (!File.Exists(item.AssetPath))
                {
                    continue;
                }

                ConfigureHumanoidAndMaterials(item.AssetPath);
                fixedCount++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog(
                "Car Rapide — personnages",
                fixedCount == 0
                    ? "Aucun FBX trouvé. Utilise d'abord Download / Fix Driver & Receiver."
                    : $"{fixedCount} personnage(s) réimporté(s) avec textures et rig Humanoid.",
                "OK");
        }

        private static void ConfigureHumanoidAndMaterials(string assetPath)
        {
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);

            if (AssetImporter.GetAtPath(assetPath) is not ModelImporter importer)
            {
                return;
            }

            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.optimizeGameObjects = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            importer.materialLocation = ModelImporterMaterialLocation.External;
            importer.materialName = ModelImporterMaterialName.BasedOnMaterialName;
            importer.materialSearch = ModelImporterMaterialSearch.Local;
            importer.SaveAndReimport();
        }

        private readonly struct DownloadItem
        {
            public DownloadItem(string label, string url, string assetPath)
            {
                Label = label;
                Url = url;
                AssetPath = assetPath;
            }

            public string Label { get; }
            public string Url { get; }
            public string AssetPath { get; }
        }
    }
}
#endif
