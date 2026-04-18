/*
 * Copyright (C) 2024 Game4Freak.io
 * This mod is provided under the Game4Freak EULA.
 * Full legal terms can be found at https://game4freak.io/eula/
 */

using Newtonsoft.Json;
using System.Collections.Generic;

namespace Oxide.Plugins
{
    [Info("Unarmed Parachuting", "VisEntities", "1.1.0")]
    [Description("Prevents players from equipping items while parachuting.")]
    public class UnarmedParachuting : RustPlugin
    {
        #region Fields

        private static UnarmedParachuting _plugin;
        private static Configuration _config;

        #endregion Fields

        #region Configuration

        private class Configuration
        {
            [JsonProperty("Version")]
            public string Version { get; set; }

            [JsonProperty("Drop Items On Ground Instead Of Moving To Inventory")]
            public bool DropItemsOnGround { get; set; }
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            _config = Config.ReadObject<Configuration>();

            if (_config.Version != Version.ToString())
            {
                PrintWarning("Configuration appears to be outdated; updating and saving");
                _config.Version = Version.ToString();
                SaveConfig();
            }
        }

        protected override void LoadDefaultConfig()
        {
            _config = new Configuration
            {
                Version = Version.ToString(),
                DropItemsOnGround = false,
            };
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(_config, true);
        }

        #endregion Configuration

        #region Oxide Hooks

        private void Init()
        {
            _plugin = this;
            PermissionUtil.RegisterPermissions();
        }

        private void Unload()
        {
            _config = null;
            _plugin = null;
        }

        private void OnActiveItemChange(BasePlayer player, Item oldItem, ItemId newItemId)
        {
            if (player == null)
                return;

            if (PermissionUtil.HasPermission(player, PermissionUtil.IGNORE))
                return;

            BaseVehicle mountedEntity = player.GetMountedVehicle();
            if (mountedEntity != null && mountedEntity is Parachute parachute)
            {
                ItemContainer beltContainer = player.inventory.containerBelt;
                if (beltContainer == null)
                    return;

                Item newItem = beltContainer.FindItemByUID(newItemId);
                if (newItem != null)
                {
                    RemoveActiveItem(player, newItem);
                    ShowToast(player, Lang.CannotEquipItemWhileParachuting, GameTip.Styles.Red_Normal);
                }
            }
        }

        #endregion Oxide Hooks

        #region Active Item Removal

        // Below method was inspired by WhiteThunder. Adapted with permission.
        private void RemoveActiveItem(BasePlayer player, Item activeItem)
        {
            if (activeItem == null)
                return;

            int slot = activeItem.position;
            activeItem.RemoveFromContainer();
            player.inventory.SendUpdatedInventory(PlayerInventory.Type.Belt, player.inventory.containerBelt);

            var playerPosition = player.transform.position;

            ServerMgr.Instance.Invoke(() =>
            {
                if (_config.DropItemsOnGround)
                {
                    activeItem.DropAndTossUpwards(playerPosition);
                }
                else if (!activeItem.MoveToContainer(player.inventory.containerBelt, slot)
                    && !player.inventory.GiveItem(activeItem))
                {
                    activeItem.DropAndTossUpwards(playerPosition);
                }
            }, 0.2f);
        }

        #endregion Active Item Removal

        #region Permissions

        private static class PermissionUtil
        {
            public const string IGNORE = "unarmedparachuting.ignore";
            private static readonly List<string> _permissions = new List<string>
            {
                IGNORE,
            };

            public static void RegisterPermissions()
            {
                foreach (var permission in _permissions)
                {
                    _plugin.permission.RegisterPermission(permission, _plugin);
                }
            }

            public static bool HasPermission(BasePlayer player, string permissionName)
            {
                return _plugin.permission.UserHasPermission(player.UserIDString, permissionName);
            }
        }

        #endregion Permissions

        #region Localization

        private class Lang
        {
            public const string CannotEquipItemWhileParachuting = "CannotEquipItemWhileParachuting";
        }

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                [Lang.CannotEquipItemWhileParachuting] = "You cannot hold items while parachuting.",

            }, this, "en");
        }

        private static string GetMessage(BasePlayer player, string messageKey, params object[] args)
        {
            string message = _plugin.lang.GetMessage(messageKey, _plugin, player.UserIDString);

            if (args.Length > 0)
                message = string.Format(message, args);

            return message;
        }

        public static void MessagePlayer(BasePlayer player, string messageKey, params object[] args)
        {
            string message = GetMessage(player, messageKey, args);
            _plugin.SendReply(player, message);
        }

        public static void ShowToast(BasePlayer player, string messageKey, GameTip.Styles style = GameTip.Styles.Blue_Normal, params object[] args)
        {
            string message = GetMessage(player, messageKey, args);
            player.SendConsoleCommand("gametip.showtoast", (int)style, message);
        }

        #endregion Localization
    }
}