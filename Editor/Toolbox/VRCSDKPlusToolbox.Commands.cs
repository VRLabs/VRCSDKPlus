using System;
using UnityEngine;

namespace DreadScripts.VRCSDKPlus
{
    internal static partial class VRCSDKPlusToolbox
    {
        #region Keyboard Commands
        internal enum EventCommands
        {
            Copy,
            Cut,
            Paste,
            Duplicate,
            Delete,
            SoftDelete,
            SelectAll,
            Find,
            FrameSelected,
            FrameSelectedWithLock,
            FocusProjectWindow
        }
        internal static bool HasReceivedCommand(EventCommands command, string matchFocusControl = "", bool useEvent = true)
        {
            if (!string.IsNullOrEmpty(matchFocusControl) && GUI.GetNameOfFocusedControl() != matchFocusControl) return false;
            Event e = Event.current;
            if (command.ToString() != e.commandName) return false;

            // Unity first sends ValidateCommand to ask whether the command is handled here (also when it is only
            // enabling Edit menu items). Using it answers "yes". The action itself runs on the ExecuteCommand that follows.
            if (e.type == EventType.ValidateCommand)
            {
                if (useEvent) e.Use();
                return false;
            }

            if (e.type != EventType.ExecuteCommand) return false;
            if (useEvent) e.Use();
            return true;
        }

        internal static bool HasReceivedKey(KeyCode key, string matchFocusControl = "", bool useEvent = true)
        {
            if (!string.IsNullOrEmpty(matchFocusControl) && GUI.GetNameOfFocusedControl() != matchFocusControl) return false;
            Event e = Event.current;
            bool received = e.type == EventType.KeyDown && e.keyCode == key;
            if (received && useEvent) e.Use();
            return received;
        }

        internal static bool HasReceivedEnter(string matchFocusControl = "",bool useEvent = true) => HasReceivedKey(KeyCode.Return, matchFocusControl, useEvent) || HasReceivedKey(KeyCode.KeypadEnter, matchFocusControl, useEvent);
        internal static bool HasReceivedCancel(string matchFocusControl = "",  bool useEvent = true) => HasReceivedKey(KeyCode.Escape, matchFocusControl, useEvent);
        internal static bool HasReceivedAnyDelete(string matchFocusControl = "", bool useEvent = true) => HasReceivedCommand(EventCommands.SoftDelete, matchFocusControl, useEvent) || HasReceivedCommand(EventCommands.Delete, matchFocusControl, useEvent) || HasReceivedKey(KeyCode.Delete, matchFocusControl, useEvent);
        internal static bool HandleConfirmEvents(string matchFocusControl = "", Action onConfirm = null, Action onCancel = null)
        {
            if (HasReceivedEnter(matchFocusControl))
            {
                onConfirm?.Invoke();
                return true;
            }

            if (HasReceivedCancel(matchFocusControl))
            {
                onCancel?.Invoke();
                return true;
            }
            return false;
        }

        internal static bool HandleTextFocusConfirmCommands(string matchFocusControl, Action onConfirm = null, Action onCancel = null)
        {
            if (!HandleConfirmEvents(matchFocusControl, onConfirm, onCancel)) return false;
            GUI.FocusControl(null);
            return true;
        }
        #endregion

    }
}
