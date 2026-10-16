using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using static FFXIVClientStructs.FFXIV.Client.UI.Misc.RaptureHotbarModule;

namespace MacroSlot;

public sealed class Plugin : IDalamudPlugin
{
    private const string Command = "/mslot";
    private const string Usage = "使い方: /mslot personal|shared <マクロ番号 0-99> <ホットバー 1-18> <スロット>";

    [PluginService] private static ICommandManager CommandManager { get; set; } = null!;
    [PluginService] private static IChatGui ChatGui { get; set; } = null!;
    [PluginService] private static IClientState ClientState { get; set; } = null!;
    [PluginService] private static IFramework Framework { get; set; } = null!;
    [PluginService] private static IPluginLog Log { get; set; } = null!;

    public Plugin()
    {
        if (!CommandManager.AddHandler(Command, new CommandInfo(OnCommand)
            {
                HelpMessage = "personal|shared <マクロ番号 0-99> <ホットバー 1-18> <スロット>",
            }))
        {
            throw new InvalidOperationException($"コマンド {Command} を登録できませんでした。");
        }
    }

    public void Dispose() => CommandManager.RemoveHandler(Command);

    private void OnCommand(string command, string arguments)
    {
        if (!TryParse(arguments, out var request, out var error))
        {
            ChatGui.PrintError($"[MacroSlot] {error}");
            ChatGui.Print(Usage);
            return;
        }

        if (!ClientState.IsLoggedIn)
        {
            ChatGui.PrintError("[MacroSlot] ログイン後に実行してください。");
            return;
        }

        _ = Framework.RunOnFrameworkThread(() => Apply(request));
    }

    private static unsafe void Apply(SlotRequest request)
    {
        try
        {
            if (!ClientState.IsLoggedIn)
            {
                ChatGui.PrintError("[MacroSlot] ログアウト中のため変更しませんでした。");
                return;
            }

            var module = RaptureHotbarModule.Instance();
            if (module is null || !module->ModuleReady || !module->DatFileLoadedSuccessfully)
            {
                ChatGui.PrintError("[MacroSlot] ホットバーデータの準備が完了していません。数秒後に再実行してください。");
                return;
            }

            if (module->PvPHotbarsActive)
            {
                ChatGui.PrintError("[MacroSlot] 誤ってPvP用ホットバーを変更しないよう、PvP中は実行できません。");
                return;
            }

            // The game stores the shared-macro page in bit 8 of a macro hotbar command ID.
            var macroCommandId = (uint)request.MacroNumber | (request.Shared ? 0x100u : 0u);

            // Native indices are zero-based. SetAndSaveSlot updates and persists this slot only.
            module->SetAndSaveSlot(
                (uint)(request.HotbarNumber - 1),
                (uint)(request.SlotNumber - 1),
                HotbarSlotType.Macro,
                macroCommandId,
                ignoreSharedHotbars: false,
                allowSaveToPvP: false);

            var kind = request.Shared ? "共通" : "個人";
            ChatGui.Print($"[MacroSlot] ホットバー{request.HotbarNumber} スロット{request.SlotNumber}を{kind}マクロ#{request.MacroNumber}に変更しました。");
            Log.Information(
                "Set hotbar {Hotbar}, slot {Slot} to {Kind} macro {Macro}",
                request.HotbarNumber,
                request.SlotNumber,
                request.Shared ? "shared" : "personal",
                request.MacroNumber);
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Failed to update a hotbar macro slot");
            ChatGui.PrintError("[MacroSlot] 変更に失敗しました。/xllog のエラーを確認してください。");
        }
    }

    internal static bool TryParse(string arguments, out SlotRequest request, out string error)
    {
        request = default;
        error = string.Empty;

        var parts = arguments.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 4)
        {
            error = "引数は4個必要です。";
            return false;
        }

        var shared = parts[0].ToLowerInvariant() switch
        {
            "personal" => false,
            "shared" => true,
            _ => (bool?)null,
        };

        if (shared is null)
        {
            error = "第1引数は personal または shared を指定してください。";
            return false;
        }

        if (!int.TryParse(parts[1], out var macroNumber) || macroNumber is < 0 or > 99)
        {
            error = "マクロ番号は 0～99 で指定してください。";
            return false;
        }

        if (!int.TryParse(parts[2], out var hotbarNumber) || hotbarNumber is < 1 or > 18)
        {
            error = "ホットバー番号は 1～18 で指定してください（11～18はクロスホットバー）。";
            return false;
        }

        var maximumSlot = hotbarNumber <= 10 ? 12 : 16;
        if (!int.TryParse(parts[3], out var slotNumber) || slotNumber < 1 || slotNumber > maximumSlot)
        {
            error = $"ホットバー{hotbarNumber}のスロット番号は 1～{maximumSlot} で指定してください。";
            return false;
        }

        request = new SlotRequest(shared.Value, macroNumber, hotbarNumber, slotNumber);
        return true;
    }

    internal readonly record struct SlotRequest(bool Shared, int MacroNumber, int HotbarNumber, int SlotNumber);
}

