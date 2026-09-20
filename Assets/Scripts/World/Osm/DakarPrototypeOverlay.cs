using UnityEngine;

namespace CarRapide.World.Osm
{
    public sealed class DakarPrototypeOverlay : MonoBehaviour
    {
        private GUIStyle titleStyle;
        private GUIStyle smallStyle;

        private void OnGUI()
        {
            titleStyle ??= new GUIStyle(GUI.skin.label)
            {
                fontSize = 22,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };

            smallStyle ??= new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                normal = { textColor = Color.white }
            };

            GUI.Box(new Rect(16, 16, 420, 96), GUIContent.none);
            GUI.Label(new Rect(30, 25, 390, 30), "Dakar OSM Prototype", titleStyle);
            GUI.Label(new Rect(30, 56, 390, 22), "Plateau • Médina • Colobane", smallStyle);
            GUI.Label(new Rect(30, 77, 390, 22), "WASD / flèches : conduire   •   Espace : frein à main", smallStyle);

            GUI.Box(new Rect(16, Screen.height - 48, 420, 32), GUIContent.none);
            GUI.Label(
                new Rect(28, Screen.height - 42, 400, 22),
                "Données © OpenStreetMap contributors — ODbL",
                smallStyle);
        }
    }
}
