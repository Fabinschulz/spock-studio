; DD-6: o comando spock no PATH do usuário, para os arquivos de IDE versionados chamarem "spock mcp --project-dir ..." sem o
; caminho desta máquina. Só a pasta bin\ entra no PATH, com uma cópia do lançador: o host e o motor não viram comandos.
; O PATH é lido e gravado pelo PowerShell porque as strings do NSIS têm teto de tamanho; um PATH cortado ao meio quebraria o
; computador da pessoa. O tipo do valor (REG_EXPAND_SZ) e as variáveis dentro dele (%USERPROFILE%...) ficam como estavam.

!macro NSIS_HOOK_POSTINSTALL
  CreateDirectory "$INSTDIR\bin"
  CopyFiles /SILENT "$INSTDIR\spock.exe" "$INSTDIR\bin\spock.exe"
  nsExec::ExecToLog `powershell.exe -NoProfile -NonInteractive -ExecutionPolicy Bypass -Command "& { $$k = 'HKCU:\Environment'; $$d = '$INSTDIR\bin'; $$p = (Get-Item $$k).GetValue('Path', '', 'DoNotExpandEnvironmentNames'); $$parts = @(($$p -split ';') | Where-Object { $$_ -ne '' }); if ($$parts -notcontains $$d) { Set-ItemProperty -Path $$k -Name Path -Type ExpandString -Value (($$parts + $$d) -join ';') } }"`
  Pop $0
  ; HWND_BROADCAST, WM_SETTINGCHANGE: terminais e IDEs abertos depois disso já enxergam o comando.
  SendMessage 0xFFFF 0x1A 0 "STR:Environment" /TIMEOUT=5000
!macroend

!macro NSIS_HOOK_PREUNINSTALL
  nsExec::ExecToLog `powershell.exe -NoProfile -NonInteractive -ExecutionPolicy Bypass -Command "& { $$k = 'HKCU:\Environment'; $$d = '$INSTDIR\bin'; $$p = (Get-Item $$k).GetValue('Path', '', 'DoNotExpandEnvironmentNames'); $$parts = @(($$p -split ';') | Where-Object { $$_ -ne '' }); if ($$parts -contains $$d) { Set-ItemProperty -Path $$k -Name Path -Type ExpandString -Value ((@($$parts | Where-Object { $$_ -ne $$d })) -join ';') } }"`
  Pop $0
  SendMessage 0xFFFF 0x1A 0 "STR:Environment" /TIMEOUT=5000
  Delete "$INSTDIR\bin\spock.exe"
  RMDir "$INSTDIR\bin"
!macroend
