# Sprite scale compensation

Standard: 12.8 px/m. `at scale 1` is what the sprite would measure with no transform scale; `as authored` is what it measures now.

| prefab | object | sprite | source px | scale | at scale 1 (m) | as authored (m) |
|---|---|---|---|---|---|---|
| `Assets/LeanTween/Examples/Prefabs/DustCloud.prefab` | DustCloud | DustCloud | 512x512 | 0.377,0.377 | 5.12 x 5.12 | 1.933 x 1.933 |
| `Assets/Prefabs/Environment/RV.prefab` | Body | WhiteSquare | 4x4 | 3.947,9.929 | 0.313 x 0.313 | 1.233 x 3.103 |
| `Assets/Prefabs/Environment/RV.prefab` | CabStripe | WhiteSquare | 4x4 | 3.825,0.7 | 0.313 x 0.313 | 1.195 x 0.219 |
| `Assets/Resources/OnFoot/RVInterior.prefab` | Floor | WhiteSquare | 4x4 | 9.93,3.95 | 0.313 x 0.313 | 3.103 x 1.234 |
| `Assets/Resources/OnFoot/RVInterior.prefab` | WallFrontL | WhiteSquare | 4x4 | 1.19,0.25 | 0.313 x 0.313 | 0.372 x 0.078 |
| `Assets/Resources/OnFoot/RVInterior.prefab` | WallFrontR | WhiteSquare | 4x4 | 7.6,0.25 | 0.313 x 0.313 | 2.375 x 0.078 |
| `Assets/Resources/OnFoot/RVInterior.prefab` | WallBack | WhiteSquare | 4x4 | 10.18,0.25 | 0.313 x 0.313 | 3.181 x 0.078 |
| `Assets/Resources/OnFoot/RVInterior.prefab` | WallLeft | WhiteSquare | 4x4 | 0.25,3.95 | 0.313 x 0.313 | 0.078 x 1.234 |
| `Assets/Resources/OnFoot/RVInterior.prefab` | WallRight | WhiteSquare | 4x4 | 0.25,3.95 | 0.313 x 0.313 | 0.078 x 1.234 |
| `Assets/Resources/OnFoot/RVInterior.prefab` | Doormat | WhiteSquare | 4x4 | 1.39,0.5 | 0.313 x 0.313 | 0.434 x 0.156 |
| `Assets/Resources/OnFoot/RVInterior.prefab` | Rug | WhiteSquare | 4x4 | 2,1.5 | 0.313 x 0.313 | 0.625 x 0.469 |
| `Assets/Resources/OnFoot/RVInterior.prefab` | Table | WhiteSquare | 4x4 | 1.2,1 | 0.313 x 0.313 | 0.375 x 0.313 |
| `Assets/Resources/OnFoot/RVInterior.prefab` | Bed | WhiteSquare | 4x4 | 1.6,2.7 | 0.313 x 0.313 | 0.5 x 0.844 |
| `Assets/Resources/OnFoot/RVInterior.prefab` | BedPillow | WhiteSquare | 4x4 | 0.5,2.4 | 0.313 x 0.313 | 0.156 x 0.75 |
| `Assets/Resources/OnFoot/RVInterior.prefab` | Counter | WhiteSquare | 4x4 | 2.4,0.7 | 0.313 x 0.313 | 0.75 x 0.219 |
| `Assets/Resources/OnFoot/RVInterior.prefab` | DriverSeat | WhiteSquare | 4x4 | 1,1.1 | 0.313 x 0.313 | 0.313 x 0.344 |
| `Assets/Resources/OnFoot/RVInterior.prefab` | PassengerSeat | WhiteSquare | 4x4 | 1,1.1 | 0.313 x 0.313 | 0.313 x 0.344 |
| `Assets/Resources/OnFoot/RVInterior.prefab` | SatnavBody | WhiteSquare | 4x4 | 0.44,0.62 | 0.313 x 0.313 | 0.138 x 0.194 |
| `Assets/Resources/OnFoot/RVInterior.prefab` | SatnavScreen | WhiteSquare | 4x4 | 0.3,0.44 | 0.313 x 0.313 | 0.094 x 0.138 |

19 scaled sprite renderer(s) found.
