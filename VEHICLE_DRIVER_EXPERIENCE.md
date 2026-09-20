# Vehicle driver experience

This branch contains a first playable version of the driver flow requested during PR review.

## Implemented flow

1. The Car Rapide starts stationary with its engine off.
2. Press **E** to open the driver's door.
3. Press **E** again to enter the vehicle.
4. During entry, the camera moves close to the cabin.
5. Press **R** to start the engine.
6. Driving controls remain disabled until the engine has started.
7. The camera transitions smoothly back behind the vehicle.
8. An apprentice / receiver is attached to the rear platform.
9. A procedural start sound and looping engine sound are included.
10. The engine loop changes pitch and volume with vehicle speed.

The interaction system is attached automatically at runtime to the playable `VehicleController`, so the existing scene does not need manual component wiring.

## Optional realistic characters — free

The prototype works immediately with procedural placeholder characters.

For the more realistic first version requested for the PR:

1. Open Unity `6000.6.1f1`.
2. Use **Car Rapide > Vehicle > Download Free Driver & Receiver**.
3. Accept the download.
4. Wait for Unity to import and configure both FBX files as Humanoid rigs.
5. Open `Assets/Scenes/SampleScene.unity`.
6. Press **Play**.

The downloader uses two fully rigged Black male avatars from Google's/UCF's VALID avatar library. They are MIT licensed and have embedded materials/textures.

## Controls

- **E**: open the door / enter the vehicle
- **R**: start the engine
- **W / Up**: accelerate after engine start
- **S / Down**: brake then reverse
- **A / D**: steer
- **Space**: handbrake

## PR demo checklist

Record a short clip showing:

- engine off at the beginning;
- the door opening;
- driver entering;
- close cabin camera;
- engine start;
- rear driving camera;
- vehicle controls working only after engine start;
- apprentice visible on the rear platform;
- audible engine loop while driving.
