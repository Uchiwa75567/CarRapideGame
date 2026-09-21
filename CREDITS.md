# Credits

## Car Rapide 3D model

- **Asset:** Car Rapide – Iconic Senegal Public Transport
- **Creator:** KidBi-Gaming
- **Source:** https://sketchfab.com/3d-models/car-rapide-iconic-senegal-public-transport-d543f055db75441cb7cb797abcc6b58b
- **License:** Creative Commons Attribution (CC BY)

This asset is used in the CarRapideGame prototype with attribution to the original creator.

## Driver and receiver avatars

The bundled driver, receiver and demonstration passenger use the **VALID (Validated Avatar Library for Inclusion and Diversity)** project by the University of Central Florida and Google.

- **Project:** VALID Avatar Library
- **Repository:** https://github.com/google/valid-avatar-library
- **Assets:** `Black_M_1_Casual.fbx` and `Black_M_2_Casual.fbx`, including their embedded textures
- **License:** MIT License
- **Paper:** https://doi.org/10.3389/frvir.2023.1248915

- **Copyright:** Copyright (c) 2022 Tiffany Do
- **Full license:** [ThirdParty/VALID-LICENSE.txt](ThirdParty/VALID-LICENSE.txt)
- **Citation:** Do, T. D., Zelenty, S., Gonzalez-Franco, M., and McMahan, R. P. (2023). *VALID: a perceptually validated Virtual Avatar Library for Inclusion and Diversity*. Frontiers in Virtual Reality 4. DOI: 10.3389/frvir.2023.1248915.

Adaptations in this project: Unity Humanoid import, separate URP materials, mesh-based height calibration, contact animation and vehicle roles. These are generic Black male avatars; the source does not identify them as Senegalese people. The Dakar context is supplied by the vehicle and gameplay.

## Engine recordings

- `Engine_Start.wav`: **Car engine Start Up 02**, looneybits, CC0 1.0. https://opengameart.org/content/car-engine-start-up-02
- `Engine_Idle.wav`, `Engine_Loop.wav`, `Engine_Rattle.wav`: adapted from **Moteur de camion #1 / Truck engine #1**, Joseph SARDIN, LaSonotheque / BigSoundBank, sound 1442. https://lasonotheque.org/moteur-de-camion-1-s1442.html
- The source page identifies the recording as CC0 and explicitly permits editing, redistribution and commercial use. Rights: https://lasonotheque.org/licences.html
- Original 48 kHz, mono, 24-bit recording: https://lasonotheque.org/UPLOAD/bwf-fr/1442.wav
- Source SHA-256: `8d58c374fe6994400e06be44deec4cb9d643a5c810ddc0345af10be98adac542`.
- License: https://creativecommons.org/publicdomain/zero/1.0/

Adaptations: seamless 8-second idle/load loops and a 6-second mechanical rattle layer, frequency shaping, soft saturation and level normalization. `Tools/Audio/prepare_worn_engine.py` reproduces these layers from the original recording. Pitch and level follow propulsion load and speed, with small irregular fluctuations to suggest a worn engine. The source is a real truck recording, not a recording of this specific Car Rapide. No synthetic oscillator is used.
