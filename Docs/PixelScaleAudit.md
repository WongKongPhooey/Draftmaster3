# Pixel scale audit

Generated 2026-08-11 21:19 from scene(s): WatkinsGlen

Standard: **12.8 texture pixels per world metre** (1 px = 0.0781 m), taken from the 64x32 car livery imported at 12.8 px/unit = a 5.0m x 2.5m car.

`ratio` is measured density / standard. 1.00 = correct. 4.00 = pixels are drawn 4x too small (texture is 4x too dense). 0.25 = pixels 4x too chunky.

`px/m low`/`px/m high` are the lowest and highest texel density measured across the mesh's edges. A gap between them is a stretch; both should read 12.8.

| object | material | texture | tex px | tiling | world size (m) | px/m low | px/m high | ratio low | ratio high | flags |
|---|---|---|---|---|---|---|---|---|---|---|
| EscapeRoad_BusStop | White |  | 0x0 | 0,0 | 0 x 0 | 0 | 0 | 0 | 0 | material has no texture (flat colour) |
| EscapeRoad_BusStop | White |  | 0x0 | 0,0 | 0 x 0 | 0 | 0 | 0 | 0 | material has no texture (flat colour) |
| LongRouteEntry | White |  | 0x0 | 0,0 | 0 x 0 | 0 | 0 | 0 | 0 | material has no texture (flat colour) |
| LongRouteEntry | White |  | 0x0 | 0,0 | 0 x 0 | 0 | 0 | 0 | 0 | material has no texture (flat colour) |
| LongRouteExit | White |  | 0x0 | 0,0 | 0 x 0 | 0 | 0 | 0 | 0 | material has no texture (flat colour) |
| LongRouteExit | White |  | 0x0 | 0,0 | 0 x 0 | 0 | 0 | 0 | 0 | material has no texture (flat colour) |
| PlayerCar | (none) |  | 0x0 | 0,0 | 0 x 0 | 0 | 0 | 0 | 0 | no material assigned (built at runtime?) — not measurable at edit time |
| RV/Body | (SpriteRenderer) | WhiteSquare | 4x4 | 1,1 | 1.23 x 3.1 | 3.24 | 1.29 | 0.25 | 0.1 | non-uniform transform scale |
| RV/CabStripe | (SpriteRenderer) | WhiteSquare | 4x4 | 1,1 | 1.2 x 0.22 | 3.35 | 18.29 | 0.26 | 1.43 | non-uniform transform scale |
| Track/BrakeMarkers/Marker_100m | (SpriteRenderer) |  | 2x2 | 1,1 | 2.4 x 1.4 | 0.83 | 1.43 | 0.07 | 0.11 | non-uniform transform scale |
| Track/BrakeMarkers/Marker_100m | (SpriteRenderer) |  | 2x2 | 1,1 | 2.4 x 1.4 | 0.83 | 1.43 | 0.07 | 0.11 | non-uniform transform scale |
| Track/BrakeMarkers/Marker_100m | (SpriteRenderer) |  | 2x2 | 1,1 | 2.4 x 1.4 | 0.83 | 1.43 | 0.07 | 0.11 | non-uniform transform scale |
| Track/BrakeMarkers/Marker_100m | (SpriteRenderer) |  | 2x2 | 1,1 | 2.4 x 1.4 | 0.83 | 1.43 | 0.07 | 0.11 | non-uniform transform scale |
| Track/BrakeMarkers/Marker_100m | (SpriteRenderer) |  | 2x2 | 1,1 | 2.4 x 1.4 | 0.83 | 1.43 | 0.07 | 0.11 | non-uniform transform scale |
| Track/BrakeMarkers/Marker_100m/Label | Font Material | Font Texture | 256x256 | 1,1 | 0 x 0 | 0 | 0 | 0 | 0 | no mesh (runtime-built) |
| Track/BrakeMarkers/Marker_100m/Label | Font Material | Font Texture | 256x256 | 1,1 | 0 x 0 | 0 | 0 | 0 | 0 | no mesh (runtime-built) |
| Track/BrakeMarkers/Marker_100m/Label | Font Material | Font Texture | 256x256 | 1,1 | 0 x 0 | 0 | 0 | 0 | 0 | no mesh (runtime-built) |
| Track/BrakeMarkers/Marker_100m/Label | Font Material | Font Texture | 256x256 | 1,1 | 0 x 0 | 0 | 0 | 0 | 0 | no mesh (runtime-built) |
| Track/BrakeMarkers/Marker_100m/Label | Font Material | Font Texture | 256x256 | 1,1 | 0 x 0 | 0 | 0 | 0 | 0 | no mesh (runtime-built) |
| Track/BrakeMarkers/Marker_150m | (SpriteRenderer) |  | 2x2 | 1,1 | 2.4 x 1.4 | 0.83 | 1.43 | 0.07 | 0.11 | non-uniform transform scale |
| Track/BrakeMarkers/Marker_150m | (SpriteRenderer) |  | 2x2 | 1,1 | 2.4 x 1.4 | 0.83 | 1.43 | 0.07 | 0.11 | non-uniform transform scale |
| Track/BrakeMarkers/Marker_150m | (SpriteRenderer) |  | 2x2 | 1,1 | 2.4 x 1.4 | 0.83 | 1.43 | 0.07 | 0.11 | non-uniform transform scale |
| Track/BrakeMarkers/Marker_150m | (SpriteRenderer) |  | 2x2 | 1,1 | 2.4 x 1.4 | 0.83 | 1.43 | 0.07 | 0.11 | non-uniform transform scale |
| Track/BrakeMarkers/Marker_150m | (SpriteRenderer) |  | 2x2 | 1,1 | 2.4 x 1.4 | 0.83 | 1.43 | 0.07 | 0.11 | non-uniform transform scale |
| Track/BrakeMarkers/Marker_150m/Label | Font Material | Font Texture | 256x256 | 1,1 | 0 x 0 | 0 | 0 | 0 | 0 | no mesh (runtime-built) |
| Track/BrakeMarkers/Marker_150m/Label | Font Material | Font Texture | 256x256 | 1,1 | 0 x 0 | 0 | 0 | 0 | 0 | no mesh (runtime-built) |
| Track/BrakeMarkers/Marker_150m/Label | Font Material | Font Texture | 256x256 | 1,1 | 0 x 0 | 0 | 0 | 0 | 0 | no mesh (runtime-built) |
| Track/BrakeMarkers/Marker_150m/Label | Font Material | Font Texture | 256x256 | 1,1 | 0 x 0 | 0 | 0 | 0 | 0 | no mesh (runtime-built) |
| Track/BrakeMarkers/Marker_150m/Label | Font Material | Font Texture | 256x256 | 1,1 | 0 x 0 | 0 | 0 | 0 | 0 | no mesh (runtime-built) |
| Track/BrakeMarkers/Marker_50m | (SpriteRenderer) |  | 2x2 | 1,1 | 2.4 x 1.4 | 0.83 | 1.43 | 0.07 | 0.11 | non-uniform transform scale |
| Track/BrakeMarkers/Marker_50m | (SpriteRenderer) |  | 2x2 | 1,1 | 2.4 x 1.4 | 0.83 | 1.43 | 0.07 | 0.11 | non-uniform transform scale |
| Track/BrakeMarkers/Marker_50m | (SpriteRenderer) |  | 2x2 | 1,1 | 2.4 x 1.4 | 0.83 | 1.43 | 0.07 | 0.11 | non-uniform transform scale |
| Track/BrakeMarkers/Marker_50m | (SpriteRenderer) |  | 2x2 | 1,1 | 2.4 x 1.4 | 0.83 | 1.43 | 0.07 | 0.11 | non-uniform transform scale |
| Track/BrakeMarkers/Marker_50m | (SpriteRenderer) |  | 2x2 | 1,1 | 2.4 x 1.4 | 0.83 | 1.43 | 0.07 | 0.11 | non-uniform transform scale |
| Track/BrakeMarkers/Marker_50m/Label | Font Material | Font Texture | 256x256 | 1,1 | 0 x 0 | 0 | 0 | 0 | 0 | no mesh (runtime-built) |
| Track/BrakeMarkers/Marker_50m/Label | Font Material | Font Texture | 256x256 | 1,1 | 0 x 0 | 0 | 0 | 0 | 0 | no mesh (runtime-built) |
| Track/BrakeMarkers/Marker_50m/Label | Font Material | Font Texture | 256x256 | 1,1 | 0 x 0 | 0 | 0 | 0 | 0 | no mesh (runtime-built) |
| Track/BrakeMarkers/Marker_50m/Label | Font Material | Font Texture | 256x256 | 1,1 | 0 x 0 | 0 | 0 | 0 | 0 | no mesh (runtime-built) |
| Track/BrakeMarkers/Marker_50m/Label | Font Material | Font Texture | 256x256 | 1,1 | 0 x 0 | 0 | 0 | 0 | 0 | no mesh (runtime-built) |
| Track/LeftEdgeLine | White |  | 0x0 | 0,0 | 0 x 0 | 0 | 0 | 0 | 0 | material has no texture (flat colour) |
| Track/PitLane/PitBoxLane | PitBoxLaneGrey |  | 0x0 | 0,0 | 0 x 0 | 0 | 0 | 0 | 0 | material has no texture (flat colour) |
| Track/RightEdgeLine | White |  | 0x0 | 0,0 | 0 x 0 | 0 | 0 | 0 | 0 | material has no texture (flat colour) |
| TrackEnvironment/Barriers/Barrier_Inner_0_0 | White |  | 0x0 | 0,0 | 0 x 0 | 0 | 0 | 0 | 0 | material has no texture (flat colour) |
| TrackEnvironment/Barriers/Barrier_Inner_16_0 | White |  | 0x0 | 0,0 | 0 x 0 | 0 | 0 | 0 | 0 | material has no texture (flat colour) |
| TrackEnvironment/Barriers/Barrier_Inner_17_0 | White |  | 0x0 | 0,0 | 0 x 0 | 0 | 0 | 0 | 0 | material has no texture (flat colour) |
| TrackEnvironment/Barriers/Barrier_Inner_2_0 | White |  | 0x0 | 0,0 | 0 x 0 | 0 | 0 | 0 | 0 | material has no texture (flat colour) |
| TrackEnvironment/Barriers/Barrier_Inner_20_0 | White |  | 0x0 | 0,0 | 0 x 0 | 0 | 0 | 0 | 0 | material has no texture (flat colour) |
| TrackEnvironment/Barriers/Barrier_Inner_21_0 | White |  | 0x0 | 0,0 | 0 x 0 | 0 | 0 | 0 | 0 | material has no texture (flat colour) |
| TrackEnvironment/Barriers/Barrier_Inner_3_0 | White |  | 0x0 | 0,0 | 0 x 0 | 0 | 0 | 0 | 0 | material has no texture (flat colour) |
| TrackEnvironment/Barriers/Barrier_Inner_4_0 | White |  | 0x0 | 0,0 | 0 x 0 | 0 | 0 | 0 | 0 | material has no texture (flat colour) |
| TrackEnvironment/Barriers/Barrier_Inner_5_0 | White |  | 0x0 | 0,0 | 0 x 0 | 0 | 0 | 0 | 0 | material has no texture (flat colour) |
| TrackEnvironment/Barriers/Barrier_Inner_6_0 | White |  | 0x0 | 0,0 | 0 x 0 | 0 | 0 | 0 | 0 | material has no texture (flat colour) |
| TrackEnvironment/Barriers/Barrier_Inner_7_0 | White |  | 0x0 | 0,0 | 0 x 0 | 0 | 0 | 0 | 0 | material has no texture (flat colour) |
| TrackEnvironment/Barriers/Barrier_Inner_8_0 | White |  | 0x0 | 0,0 | 0 x 0 | 0 | 0 | 0 | 0 | material has no texture (flat colour) |
| TrackEnvironment/Barriers/Barrier_Inner_Manual_2 | White |  | 0x0 | 0,0 | 0 x 0 | 0 | 0 | 0 | 0 | material has no texture (flat colour) |
| TrackEnvironment/Barriers/Barrier_Inner_Manual_3 | White |  | 0x0 | 0,0 | 0 x 0 | 0 | 0 | 0 | 0 | material has no texture (flat colour) |
| TrackEnvironment/Barriers/Barrier_Outer_0_0 | White |  | 0x0 | 0,0 | 0 x 0 | 0 | 0 | 0 | 0 | material has no texture (flat colour) |
| TrackEnvironment/Barriers/Barrier_Outer_15_0 | White |  | 0x0 | 0,0 | 0 x 0 | 0 | 0 | 0 | 0 | material has no texture (flat colour) |
| TrackEnvironment/Barriers/Barrier_Outer_18_0 | White |  | 0x0 | 0,0 | 0 x 0 | 0 | 0 | 0 | 0 | material has no texture (flat colour) |
| TrackEnvironment/Barriers/Barrier_Outer_19_0 | White |  | 0x0 | 0,0 | 0 x 0 | 0 | 0 | 0 | 0 | material has no texture (flat colour) |
| TrackEnvironment/Barriers/Barrier_Outer_20_0 | White |  | 0x0 | 0,0 | 0 x 0 | 0 | 0 | 0 | 0 | material has no texture (flat colour) |
| TrackEnvironment/Barriers/Barrier_Outer_21_0 | White |  | 0x0 | 0,0 | 0 x 0 | 0 | 0 | 0 | 0 | material has no texture (flat colour) |
| TrackEnvironment/Barriers/Barrier_Outer_3_0 | White |  | 0x0 | 0,0 | 0 x 0 | 0 | 0 | 0 | 0 | material has no texture (flat colour) |
| TrackEnvironment/Barriers/Barrier_Outer_4_0 | White |  | 0x0 | 0,0 | 0 x 0 | 0 | 0 | 0 | 0 | material has no texture (flat colour) |
| TrackEnvironment/Barriers/Barrier_Outer_5_0 | White |  | 0x0 | 0,0 | 0 x 0 | 0 | 0 | 0 | 0 | material has no texture (flat colour) |
| TrackEnvironment/Barriers/Barrier_Outer_6_0 | White |  | 0x0 | 0,0 | 0 x 0 | 0 | 0 | 0 | 0 | material has no texture (flat colour) |
| TrackEnvironment/Barriers/Barrier_Outer_7_0 | White |  | 0x0 | 0,0 | 0 x 0 | 0 | 0 | 0 | 0 | material has no texture (flat colour) |
| TrackEnvironment/Barriers/Barrier_Outer_8_0 | White |  | 0x0 | 0,0 | 0 x 0 | 0 | 0 | 0 | 0 | material has no texture (flat colour) |
| TrackEnvironment/Barriers/Barrier_Outer_Manual_0 | White |  | 0x0 | 0,0 | 0 x 0 | 0 | 0 | 0 | 0 | material has no texture (flat colour) |
| TrackEnvironment/Barriers/Barrier_Outer_Manual_1 | White |  | 0x0 | 0,0 | 0 x 0 | 0 | 0 | 0 | 0 | material has no texture (flat colour) |
| TrackEnvironment/Barriers/Barrier_Outer_Manual_4 | White |  | 0x0 | 0,0 | 0 x 0 | 0 | 0 | 0 | 0 | material has no texture (flat colour) |
| TrackEnvironment/Runoff/Gravel 7 | (none) |  | 0x0 | 0,0 | 0 x 0 | 0 | 0 | 0 | 0 | no material assigned (built at runtime?) — not measurable at edit time |
| TrackEnvironment/Strips/Kerb Inner 12+150m | Kerb | kerb | 16x32 | 1,1 | 136.9 x 49.76 | 9.51 | 14.1 | 0.74 | 1.1 | OFF-STANDARD |
| TrackEnvironment/Strips/Kerb Inner 18+100m | Kerb | kerb | 16x32 | 1,1 | 3.17 x 101.29 | 11.79 | 12.8 | 0.92 | 1 | OFF-STANDARD |
| TrackEnvironment/Strips/PitExitLine | White |  | 0x0 | 0,0 | 0 x 0 | 0 | 0 | 0 | 0 | material has no texture (flat colour) |
| TrackReferenceImage | (SpriteRenderer) | watkins-glen-aerial | 2271x1232 | 1,1 | 1657.83 x 899.36 | 1.37 | 1.37 | 0.11 | 0.11 | OFF-STANDARD |
| EscapeRoad_BusStop | TrackSurface | asphalt-dark-1024-1024 | 1024x1024 | 1,1 | 200 x 11 | 12.71 | 12.71 | 0.99 | 0.99 | ok |
| Grandstand_MainStraight | Grandstand | crowd-phoenix | 128x384 | 1,1 | 120 x 20.5 | 12.8 | 12.8 | 1 | 1 | ok |
| Grandstand_MainStraight (1) | Grandstand | crowd-phoenix | 128x384 | 1,1 | 60 x 12 | 12.8 | 12.8 | 1 | 1 | ok |
| Grandstand_MainStraight (2) | Grandstand | crowd-phoenix | 128x384 | 1,1 | 120 x 10 | 12.8 | 12.8 | 1 | 1 | ok |
| Grandstand_MainStraight (3) | Grandstand | crowd-phoenix | 128x384 | 1,1 | 48 x 20.5 | 12.8 | 12.8 | 1 | 1 | ok |
| Grandstand_MainStraight (4) | Grandstand | crowd-phoenix | 128x384 | 1,1 | 48 x 20.5 | 12.8 | 12.8 | 1 | 1 | ok |
| Grandstand_MainStraight (5) | Grandstand | crowd-phoenix | 128x384 | 1,1 | 48 x 20.5 | 12.8 | 12.8 | 1 | 1 | ok |
| Grandstand_MainStraight (6) | Grandstand | crowd-phoenix | 128x384 | 1,1 | 48 x 20.5 | 12.8 | 12.8 | 1 | 1 | ok |
| Ground | Grass | grass-thick | 32x32 | 2000,2000 | 5000 x 5000 | 12.8 | 12.8 | 1 | 1 | ok |
| LongRouteEntry | TrackSurface | asphalt-dark-1024-1024 | 1024x1024 | 1,1 | 343.13 x 142.12 | 12.8 | 12.8 | 1 | 1 | ok |
| LongRouteExit | TrackSurface | asphalt-dark-1024-1024 | 1024x1024 | 1,1 | 154.32 x 258.83 | 12.8 | 12.8 | 1 | 1 | ok |
| Track | TrackSurface | asphalt-dark-1024-1024 | 1024x1024 | 1,1 | 1519.55 x 778.23 | 12.8 | 12.8 | 1 | 1 | ok |
| Track/PitLane | TrackSurface | asphalt-dark-1024-1024 | 1024x1024 | 1,1 | 698.54 x 95.08 | 12.8 | 12.8 | 1 | 1 | ok |
| TrackEnvironment/Decorations/Marshal Post A | (SpriteRenderer) | post1 | 8x32 | 1,1 | 0.63 x 2.5 | 12.8 | 12.8 | 1 | 1 | ok |
| TrackEnvironment/Decorations/Marshal Post B | (SpriteRenderer) | post1 | 8x32 | 1,1 | 0.63 x 2.5 | 12.8 | 12.8 | 1 | 1 | ok |
| TrackEnvironment/Decorations/Marshal Post C | (SpriteRenderer) | post1 | 8x32 | 1,1 | 0.63 x 2.5 | 12.8 | 12.8 | 1 | 1 | ok |
| TrackEnvironment/Runoff/TarmacRunoff 0 | TrackSurface | asphalt-dark-1024-1024 | 1024x1024 | 1,1 | 100.14 x 219.84 | 12.8 | 12.8 | 1 | 1 | ok |
| TrackEnvironment/Runoff/TarmacRunoff 1 | TrackSurface | asphalt-dark-1024-1024 | 1024x1024 | 1,1 | 89.03 x 209.04 | 12.8 | 12.8 | 1 | 1 | ok |
| TrackEnvironment/Runoff/TarmacRunoff 3 | TrackSurface | asphalt-dark-1024-1024 | 1024x1024 | 1,1 | 158.48 x 69.23 | 12.8 | 12.8 | 1 | 1 | ok |
| TrackEnvironment/Runoff/TarmacRunoff 3 | TrackSurface | asphalt-dark-1024-1024 | 1024x1024 | 1,1 | 151.99 x 261.27 | 12.8 | 12.8 | 1 | 1 | ok |
| TrackEnvironment/Runoff/TarmacRunoff 4 | TrackSurface | asphalt-dark-1024-1024 | 1024x1024 | 1,1 | 58.54 x 76.81 | 12.8 | 12.8 | 1 | 1 | ok |
| TrackEnvironment/Runoff/TarmacRunoff 5 | TrackSurface | asphalt-dark-1024-1024 | 1024x1024 | 1,1 | 200.69 x 62.05 | 12.8 | 12.8 | 1 | 1 | ok |
| TrackEnvironment/Runoff/TarmacRunoff 6 | TrackSurface | asphalt-dark-1024-1024 | 1024x1024 | 1,1 | 470.52 x 72.32 | 12.8 | 12.8 | 1 | 1 | ok |
| TrackEnvironment/Strips/FinishLine | FinishLine | finish | 16x256 | 1,1 | 1.5 x 14 | 12.8 | 12.8 | 1 | 1 | ok |
| TrackEnvironment/Strips/Kerb Inner 16+340m | Kerb | kerb | 16x32 | 1,1 | 122.94 x 214.99 | 12.8 | 13.73 | 1 | 1.07 | curve spread |
| TrackEnvironment/Strips/Kerb Inner 17+60m | Kerb | kerb | 16x32 | 1,1 | 57.81 x 18 | 12.8 | 12.8 | 1 | 1 | ok |
| TrackEnvironment/Strips/Kerb Inner 20+100m | Kerb | kerb | 16x32 | 1,1 | 54.17 x 61.81 | 12.8 | 14.49 | 1 | 1.13 | curve spread |
| TrackEnvironment/Strips/Kerb Inner 3+240m | Kerb | kerb | 16x32 | 1,1 | 149.59 x 159.66 | 12.8 | 13.32 | 1 | 1.04 | ok |
| TrackEnvironment/Strips/Kerb Inner 7+240m | Kerb | kerb | 16x32 | 1,1 | 188.59 x 129.56 | 12.8 | 13.21 | 1 | 1.03 | ok |
| TrackEnvironment/Strips/Kerb Inner 9+20m | Kerb | kerb | 16x32 | 1,1 | 15.44 x 6.97 | 12.8 | 18.14 | 1 | 1.42 | curve spread |
| TrackEnvironment/Strips/Kerb Outer 0+70m | Kerb | kerb | 16x32 | 1,1 | 70 x 2 | 12.8 | 12.8 | 1 | 1 | ok |
| TrackEnvironment/Strips/Kerb Outer 11+25m | Kerb | kerb | 16x32 | 1,1 | 18.84 x 7.78 | 12.8 | 20.57 | 1 | 1.61 | curve spread |
| TrackEnvironment/Strips/Kerb Outer 17+140m | Kerb | kerb | 16x32 | 1,1 | 124.93 x 37.88 | 12.8 | 12.8 | 1 | 1 | ok |
| TrackEnvironment/Strips/Kerb Outer 18+120m | Kerb | kerb | 16x32 | 1,1 | 61.07 x 84.14 | 12.8 | 14.05 | 1 | 1.1 | curve spread |
| TrackEnvironment/Strips/Kerb Outer 2+168m | Kerb | kerb | 16x32 | 1,1 | 22.46 x 166.99 | 12.8 | 12.8 | 1 | 1 | ok |
| TrackEnvironment/Strips/Kerb Outer 5+200m | Kerb | kerb | 16x32 | 1,1 | 150.6 x 113.98 | 12.8 | 13.35 | 1 | 1.04 | ok |
| TrackEnvironment/Strips/Turn 1 Inside | Kerb | kerb | 16x32 | 1,1 | 31.74 x 23.2 | 12.8 | 16.7 | 1 | 1.3 | curve spread |
