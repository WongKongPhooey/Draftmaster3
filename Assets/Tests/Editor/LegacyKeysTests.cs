using System;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;

// EditMode coverage for LegacyKeys — serialized KeyCode toggles read through the Input System, since the
// project runs without the legacy Input class (Android does not support "Both").
//
// The mapping is the part that can go quietly wrong: a KeyCode that maps to Key.None is a panel toggle that
// silently never opens. So every key a panel in this project is bound to by default is pinned here, with the
// names the two enums spell differently.
//
// LegacyKeys lives in Assembly-CSharp, which an asmdef can't reference, so it is reached by reflection.
public class LegacyKeysTests
{
    static readonly Type KeysType = Type.GetType("LegacyKeys, Assembly-CSharp");

    static Key ToKey(KeyCode code) => (Key)KeysType.GetMethod("ToKey").Invoke(null, new object[] { code });

    [Test]
    public void TypeExists() => Assert.IsNotNull(KeysType, "LegacyKeys is gone.");

    [TestCase(KeyCode.Escape, Key.Escape)]
    [TestCase(KeyCode.Tab, Key.Tab)]
    [TestCase(KeyCode.F2, Key.F2)]
    [TestCase(KeyCode.F4, Key.F4)]
    [TestCase(KeyCode.A, Key.A)]
    [TestCase(KeyCode.D, Key.D)]
    [TestCase(KeyCode.Alpha3, Key.Digit3)]
    [TestCase(KeyCode.Keypad7, Key.Numpad7)]
    [TestCase(KeyCode.Return, Key.Enter)]
    [TestCase(KeyCode.LeftControl, Key.LeftCtrl)]
    [TestCase(KeyCode.BackQuote, Key.Backquote)]
    [TestCase(KeyCode.LeftShift, Key.LeftShift)]
    [TestCase(KeyCode.Space, Key.Space)]
    public void MapsToTheSameKey(KeyCode code, Key expected) => Assert.AreEqual(expected, ToKey(code));

    [Test]
    public void EveryDigit_MapsToItsOwnKey()
    {
        for (int n = 0; n <= 9; n++)
        {
            Assert.AreEqual(Enum.Parse<Key>("Digit" + n), ToKey(KeyCode.Alpha0 + n), $"Alpha{n}");
            Assert.AreEqual(Enum.Parse<Key>("Numpad" + n), ToKey(KeyCode.Keypad0 + n), $"Keypad{n}");
        }
    }

    [Test]
    public void EveryFunctionKey_Maps()
    {
        for (KeyCode k = KeyCode.F1; k <= KeyCode.F12; k++)
            Assert.AreNotEqual(Key.None, ToKey(k), $"{k} would never fire.");
    }

    [Test]
    public void EveryLetter_Maps()
    {
        for (KeyCode k = KeyCode.A; k <= KeyCode.Z; k++)
            Assert.AreNotEqual(Key.None, ToKey(k), $"{k} would never fire.");
    }
}
