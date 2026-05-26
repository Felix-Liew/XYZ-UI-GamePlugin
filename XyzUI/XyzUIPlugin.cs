using Rocket.API;
using Rocket.API.Collections;
using Rocket.Core.Commands;
using Rocket.Core.Plugins;
using Rocket.Unturned;
using Rocket.Unturned.Chat;
using Rocket.Unturned.Player;
using SDG.Unturned;
using Steamworks;
using System.Collections.Generic;
using UnityEngine;

namespace FelixLiew.XyzUI
{
    public class XyzUI : RocketPlugin<XyzUIConfiguration>
    {
        public static XyzUI Instance;

        private const ushort EFFECT_ID = 32501;
        private const short EFFECT_KEY = 3457;

        private const float UPDATE_INTERVAL = 0.20f;
        private const float MOVE_THRESHOLD_SQR = 0.08f * 0.08f;
        private const int COORD_MIN = -10000;
        private const int COORD_MAX = 10000;

        private static readonly HashSet<CSteamID> ActivePlayers = new HashSet<CSteamID>();
        public static readonly Dictionary<CSteamID, HudState> States = new Dictionary<CSteamID, HudState>();

        private float nextTick;

        public class HudState
        {
            public bool Enabled;
            public Vector3 LastPos;
            public string LastX = string.Empty;
            public string LastY = string.Empty;
            public string LastZ = string.Empty;
        }

        public override TranslationList DefaultTranslations => new TranslationList()
        {
            { "Showxyz", "You enabled xyz." },
            { "Hidexyz", "You hid xyz." },
            { "Usage", "Use: /xyz" }
        };

        protected override void Load()
        {
            Instance = this;
            U.Events.OnPlayerConnected += OnPlayerConnected;
            U.Events.OnPlayerDisconnected += OnPlayerDisconnected;
        }

        protected override void Unload()
        {
            U.Events.OnPlayerConnected -= OnPlayerConnected;
            U.Events.OnPlayerDisconnected -= OnPlayerDisconnected;

            foreach (var id in ActivePlayers)
            {
                EffectManager.askEffectClearByID(EFFECT_ID, id);
            }

            ActivePlayers.Clear();
            States.Clear();
            Instance = null;
        }

        private void Update()
        {
            float now = Time.realtimeSinceStartup;
            if (now < nextTick) return;
            nextTick = now + UPDATE_INTERVAL;

            if (ActivePlayers.Count == 0) return;

            foreach (CSteamID id in ActivePlayers)
            {
                Player player = PlayerTool.getPlayer(id);
                if (player == null) continue;

                if (!States.TryGetValue(id, out HudState state) || !state.Enabled)
                    continue;

                Vector3 pos = player.transform.position;
                Vector3 delta = pos - state.LastPos;

                if (delta.sqrMagnitude < MOVE_THRESHOLD_SQR) continue;

                state.LastPos = pos;
                UpdateHudText(id, pos, state);
            }
        }

        private void SendHudFrame(CSteamID id, Vector3 pos, HudState state)
        {
            EffectManager.sendUIEffect(EFFECT_ID, EFFECT_KEY, id, true);
            UpdateHudText(id, pos, state, true);
        }

        private void UpdateHudText(CSteamID id, Vector3 pos, HudState state, bool force = false)
        {
            // Round coordinates to nearest integer and clamp to allowed range
            int xVal = Mathf.RoundToInt(pos.x);
            int yVal = Mathf.RoundToInt(pos.y);
            int zVal = Mathf.RoundToInt(pos.z);

            xVal = Mathf.Clamp(xVal, COORD_MIN, COORD_MAX);
            yVal = Mathf.Clamp(yVal, COORD_MIN, COORD_MAX);
            zVal = Mathf.Clamp(zVal, COORD_MIN, COORD_MAX);

            string xStr = xVal.ToString();
            string yStr = yVal.ToString();
            string zStr = zVal.ToString();

            if (!force && state.LastX == xStr && state.LastY == yStr && state.LastZ == zStr)
                return;

            state.LastX = xStr;
            state.LastY = yStr;
            state.LastZ = zStr;

            EffectManager.sendUIEffectText(EFFECT_KEY, id, true, "UI_X", xStr);
            EffectManager.sendUIEffectText(EFFECT_KEY, id, true, "UI_Y", yStr);
            EffectManager.sendUIEffectText(EFFECT_KEY, id, true, "UI_Z", zStr);
        }
        private void OnPlayerConnected(UnturnedPlayer player)
        {
            CSteamID id = player.CSteamID;
            Vector3 pos = player.Position;

            HudState state = new HudState
            {
                Enabled = true,
                LastPos = pos
            };
            States[id] = state;
            ActivePlayers.Add(id);

            SendHudFrame(id, pos, state);
        }

        private void OnPlayerDisconnected(UnturnedPlayer player)
        {
            CSteamID id = player.CSteamID;
            ActivePlayers.Remove(id);
            States.Remove(id);
        }

        [RocketCommand("xyz", "Show/hide xyz", "/xyz", AllowedCaller.Player)]
        public void Cmd(IRocketPlayer caller, string[] args)
        {
            if (!(caller is UnturnedPlayer player)) return;

            if (args.Length > 0)
            {
                UnturnedChat.Say(player, Translate("Usage"), Color.red);
                return;
            }

            CSteamID id = player.CSteamID;

            if (!States.TryGetValue(id, out HudState state))
            {
                state = new HudState();
                States[id] = state;
            }

            if (state.Enabled)
            {
                state.Enabled = false;
                ActivePlayers.Remove(id);
                EffectManager.askEffectClearByID(EFFECT_ID, id);
                UnturnedChat.Say(player, Translate("Hidexyz"), Color.green);
            }
            else
            {
                state.Enabled = true;
                ActivePlayers.Add(id);

                Vector3 pos = player.Position;
                state.LastPos = pos;

                SendHudFrame(id, pos, state);
                UnturnedChat.Say(player, Translate("Showxyz"), Color.green);
            }
        }
    }
}