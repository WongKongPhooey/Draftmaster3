namespace Draftmaster.Controls
{
    // Portrait driving on a phone.
    //
    // The swing camera turns the world until the car's nose points up the screen, so the road runs top to
    // bottom — and a landscape phone then shows a short strip of road ahead and a lot of grass either side.
    // Held upright instead, the same camera shows the road coming, so on a handheld with the swing camera
    // chosen the screen turns to portrait while the player is in the car, and back to the game's landscape
    // the moment they are not (on foot, the fixed camera picked, the results table up).
    //
    // Everything drawn over the race lays itself out from the screen's size, so it follows the turn on its
    // own: the kit's scale is taken from the short side (KitScale), which keeps a panel the same size in the
    // hand whichever way up the phone is, and the touch controls were already sized that way.
    //
    // The rules live here, away from the engine, so the tests can pin them; DriveOrientationController reads
    // the game and turns the screen.
    public static class DriveOrientation
    {
        // Whether the screen should be upright right now.
        public static bool WantsPortrait(bool handheld, bool swingCamera, bool inCar, bool resultsUp)
            => handheld && swingCamera && inCar && !resultsUp;

        // The kit's whole-number UI scale for a screen this size: design pixels (the 640x360 grid) per screen
        // pixel, taken from the short side so a portrait phone is not handed a scale meant for a screen twice
        // its width. On a landscape screen the short side is the height, which is what it always was.
        public static int KitScale(float screenWidth, float screenHeight, float designHeight = 360f)
        {
            float shortSide = screenWidth < screenHeight ? screenWidth : screenHeight;
            if (designHeight <= 0f) return 1;
            int s = (int)System.Math.Floor(shortSide / designHeight);
            return s < 1 ? 1 : s;
        }

        // What to multiply an orthographic camera's size by on a screen this shape, so a metre of track covers
        // as many pixels upright as it did on the same phone held sideways. The size is half the screen's
        // height, so upright it grows by the height over the width; a landscape or square screen is left alone.
        public static float PortraitZoom(float screenWidth, float screenHeight)
            => screenWidth > 0f && screenHeight > screenWidth ? screenHeight / screenWidth : 1f;
    }

    // Turns to portrait at once and back only after the reason has been gone for a moment. The in-car test
    // blinks false for a frame or two whenever the player's car is swapped (a scene reload, a team switch, the
    // broadcast view handing the car over), and a phone that spins its screen round for each of those — a
    // turn the OS animates over a good half second — would be far worse than one that waits.
    public sealed class PortraitLatch
    {
        public const float DefaultReleaseDelay = 0.75f;

        readonly float _releaseDelay;
        float _unwantedFor;

        public bool Portrait { get; private set; }

        public PortraitLatch(float releaseDelay = DefaultReleaseDelay)
        {
            _releaseDelay = releaseDelay < 0f ? 0f : releaseDelay;
        }

        // `dt` is real time: a paused race (timeScale 0) must still let go if the swing camera is switched off
        // from the pause menu.
        public bool Update(bool wantPortrait, float dt)
        {
            if (wantPortrait)
            {
                Portrait = true;
                _unwantedFor = 0f;
            }
            else if (Portrait)
            {
                _unwantedFor += dt > 0f ? dt : 0f;
                if (_unwantedFor >= _releaseDelay)
                {
                    Portrait = false;
                    _unwantedFor = 0f;
                }
            }
            return Portrait;
        }

        public void Reset()
        {
            Portrait = false;
            _unwantedFor = 0f;
        }
    }
}
