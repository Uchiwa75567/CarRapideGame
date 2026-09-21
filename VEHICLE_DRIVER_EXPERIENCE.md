# Driver experience

Open `Assets/Scenes/SampleScene.unity` with Unity **6000.6.1f1** and enter Play Mode. The characters, textures, animations and recorded engine audio are bundled; no download or additional Unity package is required.

## Controls

- **E**: approach, open the driver door, climb into the cabin, sit and close the door.
- **R**: reach the ignition after seating and door closure, then play the starter recording. Driving stays locked until the sound finishes and the hand returns to the wheel.
- **W / S** or **Up / Down**: accelerate, brake and reverse.
- **A / D** or **Left / Right**: steer. **Space**: handbrake.
- **P**, while the engine is off: one passenger boards through the rear door, sits, then alights. Driver and passenger interactions are mutually exclusive.
- Gamepad: left stick drives, south button applies the handbrake after ignition.

## Geometry calibration

The FBX is preserved. Its scene instance is scaled uniformly by **2.2**, producing a roughly 5.4 m long vehicle. The old collider extended below the tyres and made the mesh float. Its bottom now coincides with the tyre contact plane; the vehicle starts settled on the ground.

The original `Parebrise` material slots now use transparent URP glass, exposing the cabin without replacing any mesh. The separate `Fenetre_arriere` frame follows the rear door. A frictionless collider material leaves rolling resistance and lateral grip to the existing arcade controller; acceleration is applied after its velocity constraints, and braking is limited to the current speed to prevent oscillation around zero.

The measurements use transformed mesh vertices, not padded skinned bounds or the roof rack:

| Reference | Measurement at authored vehicle scale |
|---|---|
| Driver hinge | Original `Porte_avant_gauche` pivot, around x=-1.058, z=2.112 |
| Door opening | 76 degrees about vehicle-up, accounting for the FBX local rotation |
| Left mirror | `Retroviseur gauche`, reparented with world transforms preserved |
| Misleading FBX name | `Porte_avant_gauche.001` is the opposite-side mirror; it stays untouched |
| Driver seat | Left section of `Chaises avant`, cushion near y=1.034 |
| Wheel grips | Upper ring vertices of `Volant`; the bounds also contain its long column |
| Driver step | Floor at x=-1.015, z=1.95, y=0.336; forward of the wheel housing |
| Rear platform | `Carosserie` vertices behind z=-2.45, top near y=0.32 |
| Receiver grip | Actual rear ladder rail (`Escalier`), near x=-0.63, z=-2.20 |

All interaction anchors are serialized under `CarRapidePlayer/InteractionPoints`. Select the `VehicleInteractionPoints` component to display its gizmos. Driver height is 1.72 m; receiver/passenger height is 1.70 m, measured from baked rest-pose mesh vertices rather than inflated animation bounds.

## Animation and ownership

- `VehicleDriverExperience` composes the experience and handles prompts/input.
- `DriverController` sequences individual support, swing, reach, duck, pivot, seating and ignition phases.
- `DriverAnimationController` blends Animator clips and contact tracks.
- `CharacterContactRig` applies a final two-bone solve for feet and hands with explicit elbow/knee poles. Planted feet keep fixed contact coordinates while the other foot follows a lifted swing arc. The pelvis trajectory shifts over supporting feet. The character root is not lerped to the seat.
- `VehicleDoorController` animates the real door and attached left mirror/handle.
- `VehicleEngineController` gates ignition and `CanDrive`; `VehicleAudioController` blends recorded truck idle, load and mechanical rattle layers from the front engine position. Speed and propulsion demand control pitch/volume, with gentle irregularity for a worn motor. Braking does not count as throttle.
- `VehicleCameraController` chooses views; `VehicleCameraFollow` supplies continuous damping.
- `ReceiverController` filters vehicle acceleration into small body compensation movements while keeping feet and the hand attached to the vehicle.
- `PassengerController` demonstrates the shared contact animation architecture. Its waiting area remains in world space, so an unboarded person does not follow the departing vehicle.

The motion is authored procedurally, not motion capture. The demonstration passenger reuses one of the licensed generic avatars. The source avatars are not specifically Senegalese; attribution and exact asset provenance are in `CREDITS.md`.

## Authoring and verification tools

`Car Rapide > Vehicle > Install calibrated driver experience` reinstalls the serialized layout and materials in Edit Mode. Existing baked Humanoid performances are preserved. This operation saves the open scene and supports Undo.

`Car Rapide > Vehicle > Record 30 second Play Mode review` runs a repeatable smoke test from a fresh Play Mode session: hold controls before seating, board, start, accelerate/turn, brake and reverse. Screenshots and check results are written to `Library/VehicleReview/Run` (ignored by Git). This is a development test, not a production autoplay mode.

The review also captures the complete solved Humanoid muscle performance. After stopping Play Mode, `Bake captured Humanoid performance` writes editable `.anim` assets. Contact tracks remain authoritative for placement in this particular cabin; the clips supply the full articulated body pose and can be inspected with the Animator/Animation windows. During the turn toward the seat, the pelvis stays forward over the supporting foot until that foot lifts into its final position.

`Record passenger Play Mode review` records the rear entry, seating and exit separately in `Library/VehicleReview/Passenger`. Both reviews report contact reach errors against a 5 cm maximum threshold. The driving review also checks steering, handbrake stopping, reverse motion, held controls throughout ignition, and actual audio samples at the listener. Run them individually; use a fresh Play Mode session for the driving review.

`Record driver review with audio MP4` additionally exports `Library/VehicleReview/Run/driver-review.mp4` at 1280×720, H.264 with a 12 Mbps target bitrate. It captures Unity's actual listener mix into the MP4 and a companion `driver-review-audio.wav`. The simulation uses a fixed 30 fps during capture and restores its previous rate afterwards, including on interruption. Unity routes sound to the recording while this export runs; normal playback resumes afterwards. Encoding can take longer than the 30 seconds shown in the video.

`VehicleReviewBridge` is an Editor-only local file command harness for repeatable visual inspection. It only accepts a fixed set of commands and writes to the ignored `Library/VehicleReview` directory.

## Validation — 2026-09-21

Verified in the actual Unity 6000.6.1f1 editor on Windows, with repeated Play Mode recordings and visual inspection of the original front/rear doors, step contacts, cabin seating, wheel grips, roof clearance, camera transitions and receiver platform placement.

- Driver review with the worn truck engine: all 13 checks passed, including held W+A before boarding and throughout ignition, door closure, running audio, acceleration above 30 km/h, steering, handbrake stopping, reverse and no digital audio clipping.
- Driver contact error: maximum 0.044 m during motion; seated feet and wheel grips report 0.000 m at the recorded precision.
- Passenger review: all 3 checks passed; maximum contact error 0.032 m, with seating reached before alighting and the ignition interlock released afterwards.
- The captured listener mix reaches a peak of 0.2699 during driving (0.5739 including the starter). The engine is silent before ignition. Driving RMS is 0.0733 and falls to 0.0629 during the braking segment.
- The reviewed MP4 contains 900 frames at 30 fps (30 seconds), with both video and audio tracks. The companion WAV contains 29.995 seconds of stereo 48 kHz audio. A local delivery copy, driving sound preview and check logs are under `Recordings/VehicleExperience/`, ignored by Git.

Validation covers this scene in Play Mode; no standalone player build was produced. The avatar styling and engine recordings remain generic, as detailed in `CREDITS.md`.
