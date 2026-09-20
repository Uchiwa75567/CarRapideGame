#if UNITY_EDITOR
using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
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

        [MenuItem("Car Rapide/Vehicle/Download Free Driver & Receiver")]
        public static async void DownloadFreeCharacters()
        {
            bool confirmed = EditorUtility.DisplayDialog(
                "Car Rapide — personnages",
                "Télécharger deux avatars masculins noirs, riggés et gratuits depuis la VALID Avatar Library (MIT) ?\n\nIls serviront de première version réaliste du chauffeur et de l'apprenti.",
                "Télécharger",
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
                client.DefaultRequestHeaders.UserAgent.ParseAdd("CarRapideGame/1.0");

                for (int i = 0; i < Characters.Length; i++)
                {
                    DownloadItem item = Characters[i];
                    EditorUtility.DisplayProgressBar(
                        "Car Rapide — personnages",
                        $"Téléchargement : {item.Label} ({i + 1}/{Characters.Length})",
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
                    ConfigureHumanoid(item.AssetPath);
                }

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                EditorUtility.ClearProgressBar();
                EditorUtility.DisplayDialog(
                    "Car Rapide — personnages",
                    "Terminé. Les personnages sont prêts. Ouvre SampleScene et appuie sur Play.",
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

        [MenuItem("Car Rapide/Vehicle/Open Character Folder")]
        public static void OpenCharacterFolder()
        {
            Directory.CreateDirectory(CharacterFolder);
            AssetDatabase.Refresh();
            UnityEngine.Object folder = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(CharacterFolder);
            Selection.activeObject = folder;
            EditorGUIUtility.PingObject(folder);
        }

        private static void ConfigureHumanoid(string assetPath)
        {
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);

            if (AssetImporter.GetAtPath(assetPath) is not ModelImporter importer)
            {
                return;
            }

            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.optimizeGameObjects = false;
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
