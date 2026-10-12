using System.Collections.Generic;
using System.IO;
using Convoy.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Convoy.Input.Tests
{
    /// <summary>
    /// Guards ConvoyControls.inputactions (authored as JSON text): every action the router looks up exists,
    /// and the Pilot bindings are exactly PilotInputSource's bindings + LookBack (F-03b AC 2).
    /// The JSON is parsed with InputActionAsset.FromJson, the same parser the importer uses.
    /// </summary>
    public class ConvoyControlsAssetTests
    {
        // EditMode tests run with the project folder as the working directory.
        private const string AssetPath = "Assets/_Convoy/Config/Input/ConvoyControls.inputactions";

        private InputActionAsset _asset;

        [SetUp]
        public void SetUp()
        {
            _asset = InputActionAsset.FromJson(File.ReadAllText(AssetPath));
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_asset);
        }

        [Test]
        public void HasAllMaps_AndSeatMapNamesFollowSeatIdOrder()
        {
            Assert.AreEqual(Seats.Count, SeatInputRouter.SeatMapNames.Length);
            Assert.AreEqual("Pilot", SeatInputRouter.SeatMapNames[(int)SeatId.Pilot]);
            Assert.AreEqual("Gunner", SeatInputRouter.SeatMapNames[(int)SeatId.Gunner]);
            Assert.AreEqual("Engineer", SeatInputRouter.SeatMapNames[(int)SeatId.Engineer]);

            foreach (string map in new[] { "Pilot", "Gunner", "Engineer", "Debug", "UI" })
                Assert.IsNotNull(_asset.FindActionMap(map), map);
        }

        [Test]
        public void EveryActionTheRouterUses_Exists()
        {
            foreach (string path in SeatInputRouter.RequiredActionPaths)
                Assert.IsNotNull(_asset.FindAction(path), path);
            Assert.IsNotNull(_asset.FindAction("Gunner/SwitchStation"));
            Assert.IsNotNull(_asset.FindAction("Engineer/SwitchStation"));
            Assert.IsNotNull(_asset.FindAction("UI/Restart"));
        }

        [Test]
        public void ControlSchemes_KeyboardMouse_And_Gamepad()
        {
            Assert.IsTrue(_asset.FindControlSchemeIndex(SeatInputRouter.KeyboardMouseScheme) >= 0);
            Assert.IsTrue(_asset.FindControlSchemeIndex("Gamepad") >= 0);
        }

        [Test]
        public void EveryBinding_BelongsToAScheme()
        {
            // A binding without a group would fire for every player's devices, i.e. one keyboard driving all seats.
            foreach (InputActionMap map in _asset.actionMaps)
            foreach (InputBinding b in map.bindings)
            {
                if (b.isComposite) continue;
                Assert.IsTrue(b.groups == "KeyboardMouse" || b.groups == "Gamepad",
                    $"{map.name}/{b.action} {b.path} has groups '{b.groups}'");
            }
        }

        // Copied from PilotInputSource.Awake (the reference for "identical driving") + LookBack (C / R3).
        private static readonly Dictionary<string, string[]> PilotBindings = new Dictionary<string, string[]>
        {
            { "Accelerate", new[] { "<Keyboard>/w", "<Keyboard>/upArrow", "<Gamepad>/rightTrigger" } },
            { "Brake", new[] { "<Keyboard>/s", "<Keyboard>/downArrow", "<Gamepad>/leftTrigger" } },
            { "SteerKeys", new[] { "<Keyboard>/a", "<Keyboard>/d", "<Keyboard>/leftArrow", "<Keyboard>/rightArrow" } },
            { "SteerStick", new[] { "<Gamepad>/leftStick/x" } },
            { "Handbrake", new[] { "<Keyboard>/space", "<Gamepad>/buttonSouth" } },
            { "Boost", new[] { "<Keyboard>/leftShift", "<Keyboard>/rightShift", "<Gamepad>/rightShoulder" } },
            { "Reset", new[] { "<Keyboard>/r", "<Gamepad>/buttonNorth" } },
            { "LookBack", new[] { "<Keyboard>/c", "<Gamepad>/rightStickPress" } },
        };

        [Test]
        public void PilotBindings_MatchPilotInputSource()
        {
            InputActionMap pilot = _asset.FindActionMap("Pilot", throwIfNotFound: true);
            Assert.AreEqual(PilotBindings.Count, pilot.actions.Count, "unexpected extra/missing Pilot action");

            foreach (KeyValuePair<string, string[]> kv in PilotBindings)
            {
                InputAction action = pilot.FindAction(kv.Key, throwIfNotFound: true);
                var paths = new List<string>();
                foreach (InputBinding b in action.bindings)
                    if (!b.isComposite) paths.Add(b.path);
                CollectionAssert.AreEquivalent(kv.Value, paths, kv.Key);
            }
        }

        [Test]
        public void PilotSteerKeys_AreTwo1DAxisComposites_NegativeLeft()
        {
            InputAction steer = _asset.FindAction("Pilot/SteerKeys", throwIfNotFound: true);
            int composites = 0;
            foreach (InputBinding b in steer.bindings)
            {
                if (b.isComposite)
                {
                    composites++;
                    Assert.AreEqual("1DAxis", b.path);
                }
                else if (b.path == "<Keyboard>/a" || b.path == "<Keyboard>/leftArrow")
                {
                    Assert.AreEqual("negative", b.name.ToLowerInvariant());
                }
                else
                {
                    Assert.AreEqual("positive", b.name.ToLowerInvariant());
                }
            }
            Assert.AreEqual(2, composites);
        }

        [Test]
        public void PilotActionTypes_MatchPilotInputSource()
        {
            // Value for analogue axes (with conflict resolution), Button for held/pressed checks, as in PilotInputSource.
            Assert.AreEqual(InputActionType.Value, _asset.FindAction("Pilot/Accelerate").type);
            Assert.AreEqual(InputActionType.Value, _asset.FindAction("Pilot/Brake").type);
            Assert.AreEqual(InputActionType.Value, _asset.FindAction("Pilot/SteerKeys").type);
            Assert.AreEqual(InputActionType.Value, _asset.FindAction("Pilot/SteerStick").type);
            Assert.AreEqual(InputActionType.Button, _asset.FindAction("Pilot/Handbrake").type);
            Assert.AreEqual(InputActionType.Button, _asset.FindAction("Pilot/Boost").type);
            Assert.AreEqual(InputActionType.Button, _asset.FindAction("Pilot/Reset").type);
            Assert.AreEqual(InputActionType.PassThrough, _asset.FindAction("Gunner/AimMouse").type);
        }
    }
}
