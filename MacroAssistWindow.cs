using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace MacroSlot;

public unsafe sealed class MacroAssistWindow : Window
{
    private static readonly string[] Commands = ["/action", "/pvpaction", "/generalaction", "/petaction", "/wait", "/micon", "/macroerror", "/target", "/assist", "/focustarget", "/enemysign", "/say", "/party", "/alliance", "/echo", "/gearset", "/hotbar", "/crosshotbar", "/hud", "/readycheck", "/countdown"];
    private static readonly string[] Targets = ["<t>", "<mo>", "<tt>", "<me>", "<f>", "<lt>", "<le>", "<1>", "<2>", "<3>", "<4>", "<5>", "<6>", "<7>", "<8>"];
    private readonly MacroStorage storage = new();
    private string filter = "/";
    private string command = "/action";
    private string argument = string.Empty;
    private string target = "<t>";
    private string status = "コマンドを選んでください。";
    private bool shared;
    private int macroNumber;
    private int lineNumber = 1;

    public MacroAssistWindow() : base("Macro Assist###MacroAssist", ImGuiWindowFlags.NoScrollbar)
    {
        Size = new Vector2(430, 560);
        SizeConstraints = new WindowSizeConstraints { MinimumSize = new Vector2(380, 420), MaximumSize = new Vector2(680, 860) };
    }

    public void Open() => IsOpen = true;
    public void Toggle() => IsOpen = !IsOpen;

    public override void PreDraw()
    {
        var addon = (AtkUnitBase*)Plugin.GameGui.GetAddonByName("Macro");
        if (addon is null || !addon->IsVisible) return;
        ImGui.SetNextWindowPos(new Vector2(addon->X + addon->Width + 12, addon->Y), ImGuiCond.Always);
    }

    public override void Draw()
    {
        ImGui.TextUnformatted("書き込み先");
        ImGui.SameLine();
        if (ImGui.RadioButton("個人", !shared)) shared = false;
        ImGui.SameLine();
        if (ImGui.RadioButton("共有", shared)) shared = true;
        ImGui.SliderInt("マクロ番号", ref macroNumber, 0, 99);
        ImGui.SliderInt("行", ref lineNumber, 1, 15);
        ImGui.Separator();
        ImGui.TextUnformatted("コマンドを選ぶ");
        ImGui.InputTextWithHint("##filter", "例: /ac  /wait  /micon", ref filter, 100);
        foreach (var item in Commands.Where(x => x.Contains(filter, StringComparison.OrdinalIgnoreCase)).Take(20))
        {
            if (ImGui.Selectable(item, command == item))
            {
                command = item;
                status = item + " を選択しました。";
            }
        }
        ImGui.Separator();
        ImGui.TextUnformatted(command + " の引数");
        ImGui.InputTextWithHint("引数", "アクション名・発言内容など", ref argument, 200);
        if (ImGui.BeginCombo("対象", target))
        {
            foreach (var item in Targets)
                if (ImGui.Selectable(item, target == item)) target = item;
            ImGui.EndCombo();
        }
        var needsTarget = command is "/action" or "/pvpaction" or "/generalaction" or "/petaction" or "/target" or "/assist" or "/focustarget" or "/enemysign";
        var line = string.IsNullOrWhiteSpace(argument) ? command : command + " " + argument;
        if (needsTarget && !string.IsNullOrWhiteSpace(target)) line += " " + target;
        ImGui.Separator();
        ImGui.TextUnformatted("完成形");
        ImGui.TextWrapped(line);
        if (ImGui.Button("この行へ直接反映", new Vector2(-1, 0)))
        {
            if (storage.TryWriteLine(shared, macroNumber, lineNumber, line, out var error))
                status = $"{(shared ? "共有" : "個人")}マクロ{macroNumber}の{lineNumber}行目へ反映しました。";
            else
                status = error;
        }
        ImGui.Separator();
        ImGui.TextDisabled(status);
        ImGui.TextDisabled("/mslot personal 50 1 5 で個人マクロ50をホットバー1の5番へ配置できます。");
    }
}
