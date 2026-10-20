using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using static FFXIVClientStructs.FFXIV.Client.UI.Misc.RaptureHotbarModule;

namespace MacroSlot;

public sealed class Plugin : IDalamudPlugin
{
    private const string Command = "/mslot";
    private const string Usage = "使い方: /mslot personal|shared <マクロ番号 0-99> <ホットバー 1-18> <スロット>";
    private readonly WindowSystem windowSystem = new("MacroAssist");
    private readonly MacroAssistWindow macroAssistWindow;

    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IChatGui ChatGui { get; private set; } = null!;
    [PluginService] internal static IClientState ClientState { get; private set; } = null!;
    [PluginService] internal static IFramework Framework { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;

    public Plugin()
    {
        macroAssistWindow = new MacroAssistWindow();
        CommandManager.AddHandler(Command, new CommandInfo(OnCommand) { HelpMessage = "personal|shared <マクロ番号 0-99> <ホットバー 1-18> <スロット>" });
        CommandManager.AddHandler("/massist", new CommandInfo((_, _) => macroAssistWindow.Toggle()) { HelpMessage = "Macro Assist を開閉します。" });
        windowSystem.AddWindow(macroAssistWindow);
        PluginInterface.UiBuilder.Draw += windowSystem.Draw;
        PluginInterface.UiBuilder.OpenConfigUi += macroAssistWindow.Open;
    }

    public void Dispose()
    {
        PluginInterface.UiBuilder.Draw -= windowSystem.Draw;
        PluginInterface.UiBuilder.OpenConfigUi -= macroAssistWindow.Open;
        windowSystem.RemoveAllWindows();
        CommandManager.RemoveHandler(Command);
        CommandManager.RemoveHandler("/massist");
    }

    private void OnCommand(string commandName, string arguments)
    {
        if (!TryParse(arguments, out var request, out var error)) { ChatGui.PrintError($"[MacroSlot] {error}"); ChatGui.Print(Usage); return; }
        if (!ClientState.IsLoggedIn) { ChatGui.PrintError("[MacroSlot] ログイン後に実行してください。"); return; }
        _ = Framework.RunOnFrameworkThread(() => Apply(request));
    }

    private static unsafe void Apply(SlotRequest request)
    {
        try
        {
            var module = RaptureHotbarModule.Instance();
            if (module is null || !module->ModuleReady || !module->DatFileLoadedSuccessfully) { ChatGui.PrintError("[MacroSlot] ホットバーデータの準備が完了していません。"); return; }
            if (module->PvPHotbarsActive) { ChatGui.PrintError("[MacroSlot] PvP中は実行できません。"); return; }
            var id = (uint)request.MacroNumber | (request.Shared ? 0x100u : 0u);
            module->SetAndSaveSlot((uint)(request.HotbarNumber - 1), (uint)(request.SlotNumber - 1), HotbarSlotType.Macro, id, false, false);
        }
        catch (Exception exception) { Log.Error(exception, "Failed to update macro slot"); ChatGui.PrintError("[MacroSlot] 変更に失敗しました。"); }
    }

    internal static bool TryParse(string arguments, out SlotRequest request, out string error)
    {
        request = default; error = string.Empty;
        var parts = arguments.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 4) { error = "引数は4個必要です。"; return false; }
        var shared = parts[0].ToLowerInvariant() switch { "personal" => false, "shared" => true, _ => (bool?)null };
        if (shared is null) { error = "第1引数は personal または shared を指定してください。"; return false; }
        if (!int.TryParse(parts[1], out var macroNumber) || macroNumber is < 0 or > 99) { error = "マクロ番号は0～99で指定してください。"; return false; }
        if (!int.TryParse(parts[2], out var hotbarNumber) || hotbarNumber is < 1 or > 18) { error = "ホットバー番号は1～18で指定してください。"; return false; }
        var max = hotbarNumber <= 10 ? 12 : 16;
        if (!int.TryParse(parts[3], out var slotNumber) || slotNumber < 1 || slotNumber > max) { error = $"スロット番号は1～{max}で指定してください。"; return false; }
        request = new SlotRequest(shared.Value, macroNumber, hotbarNumber, slotNumber); return true;
    }

    internal readonly record struct SlotRequest(bool Shared, int MacroNumber, int HotbarNumber, int SlotNumber);
}
