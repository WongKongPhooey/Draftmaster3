using System;
using System.Collections.Generic;
using Draftmaster.Controls;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

// Every keyboard shortcut a player needs has a pad button, the pad buttons don't trip over each other, and
// every button a prompt can ask for has an icon to draw — for an Xbox pad and a PlayStation one.
//
// The reading and the drawing are MonoBehaviours that need a pad in hand and Play Mode; what can go wrong
// quietly is the table (two shortcuts landing on one button in the same place), the names (a prompt asking
// for a button that isn't on the pad) and the art (an icon that never got imported), and those are all here.
public class PadBindingsTests
{
    static IEnumerable<PadButton> Buttons()
    {
        foreach (PadButton b in Enum.GetValues(typeof(PadButton)))
            if (b != PadButton.None) yield return b;
    }

    static readonly PadFamily[] Families = { PadFamily.Xbox, PadFamily.PlayStation };

    // ------------------------------------------------------------------ the table

    [Test]
    public void EveryShortcut_HasAKeyAndAPadButton()
    {
        var names = new HashSet<string>();
        foreach (var s in PadBindings.All)
        {
            Assert.IsNotEmpty(s.action);
            Assert.IsTrue(names.Add(s.action), $"'{s.action}' is in the table twice.");
            Assert.IsNotEmpty(s.keyboard, $"'{s.action}' names no keyboard key.");
            Assert.AreNotEqual(PadButton.None, s.pad, $"'{s.action}' ({s.keyboard}) has no pad button.");
        }
    }

    // The keys the task is about by name: the interact key, the phone, the race panels and the pause key all
    // have to be reachable on a pad.
    [Test]
    public void TheShortcutsPlayersAskAbout_AreAllOnThePad()
    {
        var keys = new HashSet<string>();
        foreach (var s in PadBindings.All) keys.Add(s.keyboard);
        foreach (var k in new[] { "E", "P", "F1", "F2", "F3", "F4", "F6", "F10", "F11", "C", "V", "L", "T", "Q",
                                  "ESC", "TAB", "LEFT SHIFT" })
            Assert.IsTrue(keys.Contains(k), $"{k} has no pad alternative in PadBindings.");
    }

    [Test]
    public void NoTwoShortcuts_ShareAButton_UnlessTheySaySo()
    {
        var all = PadBindings.All;
        for (int i = 0; i < all.Length; i++)
            for (int j = i + 1; j < all.Length; j++)
            {
                var a = all[i];
                var b = all[j];
                if (a.pad != b.pad || !PadBindings.Overlap(a.context, b.context)) continue;
                bool declared = Array.IndexOf(a.sharesWith, b.action) >= 0 ||
                                Array.IndexOf(b.sharesWith, a.action) >= 0;
                Assert.IsTrue(declared,
                    $"'{a.action}' ({a.context}) and '{b.action}' ({b.context}) are both on {a.pad} with nothing " +
                    "to say why they never answer at the same time.");
            }
    }

    // A declared share that no longer shares anything is a note that has gone stale — the button moved and
    // the reason with it.
    [Test]
    public void EveryDeclaredShare_IsStillOnTheSameButton()
    {
        var byName = new Dictionary<string, PadBindings.Shortcut>();
        foreach (var s in PadBindings.All) byName[s.action] = s;

        foreach (var s in PadBindings.All)
            foreach (var other in s.sharesWith)
            {
                Assert.IsTrue(byName.TryGetValue(other, out var o), $"'{s.action}' shares with '{other}', which isn't in the table.");
                Assert.AreEqual(s.pad, o.pad, $"'{s.action}' says it shares a button with '{other}', but they're on different ones.");
                Assert.IsTrue(PadBindings.Overlap(s.context, o.context),
                              $"'{s.action}' and '{other}' can never be live together, so there is nothing to share.");
            }
    }

    [Test]
    public void AMenu_HasThePadToItself()
    {
        Assert.IsFalse(PadBindings.Overlap(PadBindings.Context.Menu, PadBindings.Context.Anywhere));
        Assert.IsFalse(PadBindings.Overlap(PadBindings.Context.Menu, PadBindings.Context.OnFoot));
        Assert.IsTrue(PadBindings.Overlap(PadBindings.Context.Seated, PadBindings.Context.OnFoot));
        Assert.IsFalse(PadBindings.Overlap(PadBindings.Context.Driving, PadBindings.Context.OnFoot));
    }

    // ------------------------------------------------------------------ names

    [Test]
    public void EveryButton_HasANameOnBothPads()
    {
        foreach (var b in Buttons())
            foreach (var f in Families)
                Assert.IsNotEmpty(PadGlyphs.Name(b, f), $"{b} has no name on {f}.");
    }

    [Test]
    public void APlayStationPad_IsNeverAskedForAnXboxButton()
    {
        foreach (var b in Buttons())
        {
            string ps = PadGlyphs.Name(b, PadFamily.PlayStation);
            foreach (var xbox in new[] { "A", "B", "X", "Y", "LB", "RB", "LT", "RT", "VIEW", "MENU" })
                Assert.AreNotEqual(xbox, ps, $"{b} is called {ps} on a PlayStation pad.");
        }
        Assert.AreEqual("CROSS", PadGlyphs.Name(PadButton.South, PadFamily.PlayStation));
        Assert.AreEqual("A", PadGlyphs.Name(PadButton.South, PadFamily.Xbox));
        Assert.AreEqual("L1", PadGlyphs.Name(PadButton.LeftShoulder, PadFamily.PlayStation));
    }

    // ControlHints stores a pad label and reads it back at draw time, so every name has to parse to the
    // button it came from, in either family's words.
    [Test]
    public void EveryName_ParsesBackToItsButton()
    {
        var parsed = new List<PadButton>();
        foreach (var b in Buttons())
            foreach (var f in Families)
            {
                parsed.Clear();
                string name = PadGlyphs.Name(b, f);
                Assert.IsTrue(PadGlyphs.TryParse(name, parsed), $"'{name}' does not parse.");
                CollectionAssert.AreEqual(new[] { b }, parsed, $"'{name}' parses to the wrong button.");
            }
    }

    [Test]
    public void ALabelOfSeveralButtons_ParsesInOrder_AndFreeTextDoesNot()
    {
        var parsed = new List<PadButton>();
        Assert.IsTrue(PadGlyphs.TryParse("RT / LT", parsed));
        CollectionAssert.AreEqual(new[] { PadButton.RightTrigger, PadButton.LeftTrigger }, parsed);

        parsed.Clear();
        Assert.IsTrue(PadGlyphs.TryParse("x", parsed), "Labels are matched without regard to case.");
        CollectionAssert.AreEqual(new[] { PadButton.West }, parsed, "Xbox's X is the west button.");

        parsed.Clear();
        parsed.Add(PadButton.Start);
        Assert.IsFalse(PadGlyphs.TryParse("LB / SPACE", parsed));
        CollectionAssert.AreEqual(new[] { PadButton.Start }, parsed, "A failed parse left half a label behind.");
        Assert.IsFalse(PadGlyphs.TryParse("E", parsed));
        Assert.IsFalse(PadGlyphs.TryParse("", parsed));
        Assert.IsFalse(PadGlyphs.TryParse(null, parsed));
    }

    // ------------------------------------------------------------------ icons

    [Test]
    public void EveryButton_HasAnIconOnBothPads()
    {
        foreach (var b in Buttons())
            foreach (var f in Families)
            {
                string path = PadGlyphs.IconResource(b, f);
                var sprite = Resources.Load<Sprite>(path);
                Assert.IsNotNull(sprite, $"No sprite at Resources/{path} for {b} on {f}.");
                Assert.AreEqual(16f, sprite.rect.width, $"{path} is not a 16px tile.");
                Assert.AreEqual(16f, sprite.rect.height, $"{path} is not a 16px tile.");
            }
        Assert.IsNull(PadGlyphs.IconResource(PadButton.None, PadFamily.Xbox));
    }

    // PixelGUI upscales kit art by reading its pixels, and point filtering is what keeps a 16px tile crisp
    // when it is drawn at 2x or 4x.
    [Test]
    public void TheIcons_AreImportedForPixelArt()
    {
        foreach (var b in Buttons())
            foreach (var f in Families)
            {
                string asset = "Assets/Resources/" + PadGlyphs.IconResource(b, f) + ".png";
                var importer = AssetImporter.GetAtPath(asset) as TextureImporter;
                Assert.IsNotNull(importer, $"{asset} is not imported as a texture.");
                Assert.AreEqual(TextureImporterType.Sprite, importer.textureType, asset);
                Assert.AreEqual(FilterMode.Point, importer.filterMode, asset);
                Assert.IsTrue(importer.isReadable, $"{asset} is not Read/Write, so PixelGUI cannot scale it.");
            }
    }
}
