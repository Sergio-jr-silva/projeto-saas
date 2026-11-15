' Nexo Compras Agent - Inicializador Silencioso (Zero Janela CMD)
Set WshShell = CreateObject("WScript.Shell")
Set fso = CreateObject("Scripting.FileSystemObject")

scriptDir = fso.GetParentFolderName(WScript.ScriptFullName)
exeRelease = scriptDir & "\agent-desktop\bin\Release\net9.0-windows\Nexo.Agent.Desktop.exe"
exeDebug = scriptDir & "\agent-desktop\bin\Debug\net9.0-windows\Nexo.Agent.Desktop.exe"

If fso.FileExists(exeRelease) Then
    WshShell.Run """" & exeRelease & """", 1, False
ElseIf fso.FileExists(exeDebug) Then
    WshShell.Run """" & exeDebug & """", 1, False
Else
    WshShell.Run "cmd /c cd /d """ & scriptDir & "\agent-desktop"" && dotnet run", 0, False
End If

