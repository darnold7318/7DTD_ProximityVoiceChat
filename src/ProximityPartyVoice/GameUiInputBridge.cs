using System;
using System.Reflection;
using UnityEngine;

namespace ProximityPartyVoice;

internal static class GameUiInputBridge
{
    static bool modal;
    static bool savedCursorVisible;
    static CursorLockMode savedLockState;
#pragma warning disable 618
    static bool savedScreenLockCursor;
#pragma warning restore 618
    static bool stateCaptured;
    static object? inputManager;
    static Type? inputManagerType;

    public static bool Modal => modal;

    public static void SetModalCursor(bool enabled)
    {
        if (enabled == modal)
        {
            if (enabled) ApplyModalState();
            return;
        }

        if (enabled)
        {
            CaptureState();
            modal = true;
            ApplyModalState();
        }
        else
        {
            modal = false;
            RestoreState();
        }
    }

    public static void MaintainModalCursor()
    {
        if (modal) ApplyModalState();
    }

    public static void ConsumeGuiMouseEvent()
    {
        if (!modal || Event.current == null) return;
        EventType type = Event.current.type;
        if (type == EventType.MouseDown || type == EventType.MouseUp ||
            type == EventType.MouseDrag || type == EventType.ScrollWheel)
            Event.current.Use();
    }

    static void CaptureState()
    {
        if (stateCaptured) return;
        savedCursorVisible = Cursor.visible;
        savedLockState = Cursor.lockState;
#pragma warning disable 618
        savedScreenLockCursor = Screen.lockCursor;
#pragma warning restore 618
        stateCaptured = true;
    }

    static void ApplyModalState()
    {
        // Let the game input manager enter its modal state first. Some B13 input
        // paths rewrite Unity cursor visibility while doing this, so the final
        // cursor state must be asserted after those calls.
        ApplyInputManagerState(false);
#pragma warning disable 618
        Screen.lockCursor = false;
#pragma warning restore 618
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    static void RestoreState()
    {
        ApplyInputManagerState(true);
        if (!stateCaptured) return;
#pragma warning disable 618
        Screen.lockCursor = savedScreenLockCursor;
#pragma warning restore 618
        Cursor.lockState = savedLockState;
        Cursor.visible = savedCursorVisible;
        stateCaptured = false;
    }

    static void ApplyInputManagerState(bool gameplayEnabled)
    {
        try
        {
            inputManagerType ??= Type.GetType("Platform.PlayerInputManager, Assembly-CSharp")
                                 ?? Type.GetType("PlayerInputManager, Assembly-CSharp");
            if (inputManagerType == null) return;

            inputManager ??= GetStaticMember(inputManagerType, "Instance")
                          ?? GetStaticMember(inputManagerType, "instance");
            if (inputManager == null) return;

            bool cursorEnabled = !gameplayEnabled;
            InvokeBool(inputManager, "SetCursorVisible", cursorEnabled);
            InvokeBool(inputManager, "SetCursorHidden", !cursorEnabled);
            InvokeBool(inputManager, "SetCursorEnabledOverride", cursorEnabled);
            InvokeBool(inputManager, "SetMouseLookEnabled", gameplayEnabled);
            InvokeBool(inputManager, "SetGameplayInputEnabled", gameplayEnabled);
            InvokeBool(inputManager, "SetPlayerInputEnabled", gameplayEnabled);

            SetBool(inputManager, "MouseCursorForced", cursorEnabled);
            SetBool(inputManager, "bCursorVisibleOverride", cursorEnabled);
            SetBool(inputManager, "bCursorVisibleOverrideState", cursorEnabled);
            SetBool(inputManager, "MouseCursorBlocksMouseLook", cursorEnabled);
            SetBool(inputManager, "skipMouseLookNextFrame", cursorEnabled);
            SetBool(inputManager, "SkipMouseLookNextFrame", cursorEnabled);
            SetBool(inputManager, "gameplayInputEnabled", gameplayEnabled);
            SetBool(inputManager, "GameplayInputEnabled", gameplayEnabled);
        }
        catch (Exception ex)
        {
            ModLog.Warning("Input bridge: " + ex.Message);
        }
    }

    static object? GetStaticMember(Type type, string name)
    {
        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
        return type.GetProperty(name, flags)?.GetValue(null)
            ?? type.GetField(name, flags)?.GetValue(null);
    }

    static void InvokeBool(object target, string name, bool value)
    {
        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        MethodInfo? method = target.GetType().GetMethod(name, flags, null, new[] { typeof(bool) }, null);
        method?.Invoke(target, new object[] { value });
    }

    static void SetBool(object target, string name, bool value)
    {
        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        PropertyInfo? property = target.GetType().GetProperty(name, flags);
        if (property?.CanWrite == true && property.PropertyType == typeof(bool))
        {
            property.SetValue(target, value);
            return;
        }

        FieldInfo? field = target.GetType().GetField(name, flags);
        if (field?.FieldType == typeof(bool)) field.SetValue(target, value);
    }
}
