# Dakar OSM Prototype

Cette branche ajoute une première zone de conduite basée sur les données réelles d'OpenStreetMap pour le corridor **Dakar-Plateau → Médina → Colobane**.

## Objectif

Le prototype ne cherche pas encore à reproduire chaque façade de Dakar. Il utilise les géométries OSM comme squelette réel pour :

- les routes principales, secondaires et résidentielles ;
- les emplacements de bâtiments ;
- certains marchés et arrêts de bus ;
- le positionnement relatif de Dakar-Plateau, Médina et Colobane.

Les bâtiments sont volontairement extrudés de manière simple afin de tester la taille de la zone, la conduite et les performances avant de créer les vrais assets sénégalais.

## Générer la scène

1. Ouvrir le projet avec Unity `6000.6.1f1`.
2. Attendre la fin de l'import/compilation.
3. Dans la barre de menu Unity, ouvrir :
   `Car Rapide > Dakar OSM > Build or Refresh Prototype`.
4. Accepter le téléchargement OpenStreetMap.
5. Attendre la génération de la scène.
6. Ouvrir `Assets/Scenes/DakarOsmPrototype.unity` si elle n'est pas déjà ouverte.
7. Appuyer sur **Play**.

Contrôles :

- `W` / flèche haut : accélérer ;
- `S` / flèche bas : freiner puis reculer ;
- `A` / `D` : direction ;
- `Espace` : frein à main.

## Zone utilisée

Bounding box du prototype :

- Sud : `14.6650`
- Ouest : `-17.4550`
- Nord : `14.7000`
- Est : `-17.4300`

Cette zone couvre la première tranche de travail autour de Dakar-Plateau, Médina et Colobane.

## Données cartographiques

Les géométries sont téléchargées à la demande depuis l'API Overpass et proviennent d'OpenStreetMap.

**Attribution : © OpenStreetMap contributors — licence ODbL.**

OpenStreetMap : https://www.openstreetmap.org/
Licence ODbL : https://www.openstreetmap.org/copyright

## Pourquoi la scène est générée dans Unity

On évite de stocker un gros export OSM figé dans Git. Cela permet de :

- rafraîchir les données cartographiques ;
- tester d'autres zones plus tard ;
- garder le dépôt léger ;
- séparer les données réelles de la direction artistique finale.

La prochaine étape sera de remplacer progressivement les volumes temporaires par des routes, trottoirs, bâtiments, marchés, taxis et **Ndiaga Ndiaye** adaptés à l'identité visuelle du jeu.
