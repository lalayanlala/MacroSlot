using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Lumina.Excel.Sheets;

namespace MacroSlot;

public unsafe sealed class MacroAssistWindow : Window
{
    private readonly MacroStorage storage = new();
    private readonly List<MacroCommand> commands = MacroCommand.CreateDefaults();
    private MacroCommand? selected;
    private readonly List<string> arguments = [];
    private string filter = "/";
    private string actionFilter = string.Empty;
    private bool shared;
    private int macroNumber;
    private int lineNumber = 1;
    private string status = "マクロ管理画面を開き、書き込み先を選んでください。";

    public MacroAssistWindow() : base("Macro Assist###MacroAssist", ImGuiWindowFlags.NoScrollbar)
    {
        Size = new Vector2(430, 610);
        SizeConstraints = new WindowSizeConstraints { MinimumSize = new Vector2(380, 450), MaximumSize = new Vector2(680, 900) };
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
        DrawTarget();
        ImGui.Separator();
        DrawSearch();
        if (selected is not null)
        {
            ImGui.Separator();
            DrawArguments();
            ImGui.Separator();
            DrawPreview();
        }
        ImGui.Separator();
        ImGui.TextDisabled(status);
        ImGui.TextDisabled("/mslot personal 50 1 5 で個人マクロ50をホットバー1の5番へ配置できます。");
    }

    private void DrawTarget()
    {
        ImGui.TextUnformatted("書き込み先");
        ImGui.SameLine();
        if (ImGui.RadioButton("個人", !shared)) shared = false;
        ImGui.SameLine();
        if (ImGui.RadioButton("共有", shared)) shared = true;
        ImGui.SetNextItemWidth(130);
        ImGui.SliderInt("マクロ番号", ref macroNumber, 0, 99);
        ImGui.SameLine();
        ImGui.SetNextItemWidth(105);
        ImGui.SliderInt("行", ref lineNumber, 1, 15);
    }

    private void DrawSearch()
    {
        ImGui.TextUnformatted("コマンドを選ぶ");
        ImGui.SetNextItemWidth(-1);
        ImGui.InputTextWithHint("##commandfilter", "例: /ac  /wait  /micon", ref filter, 100);
        var needle = filter.Trim();
        foreach (var command in commands.Where(c => c.Matches(needle)).Take(20))
        {
            if (ImGui.Selectable($"{command.Name}    {command.Description}", selected == command))
            {
                selected = command;
                arguments.Clear();
                arguments.AddRange(Enumerable.Repeat(string.Empty, command.Arguments.Count));
                actionFilter = string.Empty;
                status = $"{command.Name} を選択しました。";
            }
            if (ImGui.IsItemHovered()) ImGui.SetTooltip(command.AliasText);
        }
    }

    private void DrawArguments()
    {
        ImGui.TextUnformatted($"{selected!.Name} の入力");
        if (selected.Arguments.Count == 0)
        {
            ImGui.TextDisabled("引数はありません。");
            return;
        }
        for (var i = 0; i < selected.Arguments.Count; i++)
        {
            ImGui.PushID(i);
            var arg = selected.Arguments[i];
            ImGui.TextUnformatted(arg.Label + (arg.Optional ? "（任意）" : ""));
            ImGui.SameLine();
            ImGui.SetNextItemWidth(-1);
            DrawArgument(i, arg);
            ImGui.PopID();
        }
    }

    private void DrawArgument(int index, MacroArgument arg)
    {
        var value = arguments[index];
        switch (arg.Kind)
        {
            case ArgumentKind.Target:
                if (ImGui.BeginCombo("##value", string.IsNullOrEmpty(value) ? "対象を選ぶ" : value))
                {
                    foreach (var target in Targets)
                        if (ImGui.Selectable(target, value == target)) arguments[index] = target;
                    ImGui.EndCombo();
                }
                break;
            case ArgumentKind.Toggle:
                if (ImGui.BeginCombo("##value", string.IsNullOrEmpty(value) ? "on / off" : value))
                {
                    foreach (var option in new[] { "on", "off" })
                        if (ImGui.Selectable(option, value == option)) arguments[index] = option;
                    ImGui.EndCombo();
                }
                break;
            case ArgumentKind.Seconds:
                var seconds = int.TryParse(value, out var parsed) ? parsed : 1;
                if (ImGui.SliderInt("##value", ref seconds, 1, 60)) arguments[index] = seconds.ToString();
                break;
            case ArgumentKind.Action:
                DrawActionPicker(index, value);
                break;
            default:
                ImGui.InputText("##value", ref value, 200);
                arguments[index] = value;
                break;
        }
    }

    private void DrawActionPicker(int index, string value)
    {
        if (!ImGui.BeginCombo("##action", string.IsNullOrWhiteSpace(value) ? "アクションを選ぶ" : value)) return;
        ImGui.InputTextWithHint("##filter", "アクション名で検索", ref actionFilter, 100);
        var sheet = Plugin.DataManager.GetExcelSheet<Action>();
        if (sheet is not null)
        {
            var shown = 0;
            foreach (var action in sheet)
            {
                var name = action.Name.ExtractText();
                if (string.IsNullOrWhiteSpace(name) || (!string.IsNullOrWhiteSpace(actionFilter) && !name.Contains(actionFilter, StringComparison.OrdinalIgnoreCase))) continue;
                if (ImGui.Selectable(name, value.Trim('"') == name))
                {
                    arguments[index] = $""{name}"";
                    actionFilter = string.Empty;
                }
                if (++shown == 50) break;
            }
        }
        ImGui.EndCombo();
    }

    private void DrawPreview()
    {
        var line = selected!.Build(arguments);
        ImGui.TextUnformatted("完成形");
        ImGui.TextWrapped(line);
        if (ImGui.Button("この行へ直接反映", new Vector2(-1, 0)))
        {
            if (storage.TryWriteLine(shared, macroNumber, lineNumber, line, out var error))
                status = $"{(shared ? "共有" : "個人")}マクロ{macroNumber}の{lineNumber}行目へ反映しました。";
            else
                status = error;
        }
    }

    private static readonly string[] Targets = ["<t>", "<mo>", "<tt>", "<me>", "<f>", "<lt>", "<le>", "<1>", "<2>", "<3>", "<4>", "<5>", "<6>", "<7>", "<8>", "<gtoff>"];
}

public enum ArgumentKind { Text, Action, Target, Toggle, Seconds }
public sealed record MacroArgument(string Label, ArgumentKind Kind, bool Optional = false);
public sealed record MacroCommand(string Name, string Description, IReadOnlyList<string> Aliases, IReadOnlyList<MacroArgument> Arguments)
{
    public string AliasText => string.Join(", ", new[] { Name }.Concat(Aliases));
    public bool Matches(string value) => string.IsNullOrWhiteSpace(value) || new[] { Name }.Concat(Aliases).Any(x => x.Contains(value, StringComparison.OrdinalIgnoreCase));
    public string Build(IReadOnlyList<string> values) => string.Join(' ', new[] { Name }.Concat(values.Where(x => !string.IsNullOrWhiteSpace(x))));
    public static List<MacroCommand> CreateDefaults() =>
    [
        new("/action", "アクションを実行", ["/ac"], [new("アクション", ArgumentKind.Action), new("対象", ArgumentKind.Target, true)]),
        new("/pvpaction", "PvPアクションを実行", ["/pvpac"], [new("アクション名", ArgumentKind.Text), new("対象", ArgumentKind.Target, true)]),
        new("/generalaction", "一般アクションを実行", ["/gaction"], [new("アクション名", ArgumentKind.Text), new("対象", ArgumentKind.Target, true)]),
        new("/petaction", "ペットアクションを実行", ["/pac"], [new("アクション名", ArgumentKind.Text), new("対象", ArgumentKind.Target, true)]),
        new("/wait", "次の行まで待機", [], [new("秒数", ArgumentKind.Seconds)]),
        new("/micon", "マクロアイコンを設定", ["/macroicon"], [new("アイコン名", ArgumentKind.Text), new("カテゴリ", ArgumentKind.Text, true)]),
        new("/macroerror", "マクロエラー表示を切替", ["/merror"], [new("設定", ArgumentKind.Toggle, true)]),
        new("/target", "対象をターゲット", ["/ta"], [new("対象", ArgumentKind.Target, true)]),
        new("/assist", "対象のターゲットを選択", ["/as"], [new("対象", ArgumentKind.Target, true)]),
        new("/focustarget", "フォーカスターゲットを設定", [], [new("対象", ArgumentKind.Target, true)]),
        new("/enemysign", "敵にマーカーを付与", ["/marking", "/mk"], [new("種類", ArgumentKind.Text), new("対象", ArgumentKind.Target, true)]),
        new("/say", "Sayで発言", ["/s"], [new("メッセージ", ArgumentKind.Text)]),
        new("/party", "パーティーチャットで発言", ["/p"], [new("メッセージ", ArgumentKind.Text)]),
        new("/alliance", "アライアンスチャットで発言", ["/a"], [new("メッセージ", ArgumentKind.Text)]),
        new("/echo", "自分だけに表示", ["/e"], [new("メッセージ", ArgumentKind.Text)]),
        new("/gearset", "ギアセットを操作", ["/gs"], [new("サブコマンド", ArgumentKind.Text)]),
        new("/hotbar", "ホットバーを編集", [], [new("サブコマンド", ArgumentKind.Text)]),
        new("/crosshotbar", "クロスホットバーを編集", ["/chotbar"], [new("サブコマンド", ArgumentKind.Text)]),
        new("/hud", "HUD要素を操作", [], [new("HUD要素", ArgumentKind.Text), new("設定", ArgumentKind.Toggle, true)]),
        new("/readycheck", "レディチェックを開始", ["/rcheck"], []),
        new("/countdown", "カウントダウンを操作", ["/cd"], [new("秒数または操作", ArgumentKind.Text, true)])
    ];
}
