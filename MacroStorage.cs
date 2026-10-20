using FFXIVClientStructs.FFXIV.Client.UI.Misc;

namespace MacroSlot;

public unsafe sealed class MacroStorage
{
    public bool TryWriteLine(bool shared, int macroNumber, int lineNumber, string line, out string error)
    {
        error = string.Empty;
        if (!Plugin.ClientState.IsLoggedIn)
        {
            error = "ログイン後に実行してください。";
            return false;
        }
        if (lineNumber is < 1 or > 15)
        {
            error = "行番号は1～15で指定してください。";
            return false;
        }
        var module = RaptureMacroModule.Instance();
        var macro = module == null ? null : module->GetMacro((uint)(shared ? 1 : 0), (uint)macroNumber);
        if (macro == null || macro->Lines[lineNumber - 1] == null)
        {
            error = "対象マクロを取得できませんでした。";
            return false;
        }
        macro->Lines[lineNumber - 1]->SetString(line);
        return true;
    }
}
