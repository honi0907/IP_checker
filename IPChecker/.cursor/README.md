# Cursor ルール — IP Checker

## 構成

| 種類 | ファイル | 編集 |
|------|---------|------|
| playbook 汎用 | `playbook-*.mdc` | 手編集しない（install で上書き） |
| プロジェクト固有 | `ipchecker-*.mdc` | このリポジトリで編集可 |

## 導入済み playbook（generic + winui）

- `playbook-common-app-rules.mdc`
- `playbook-debug-visual-boundary.mdc`
- `playbook-winui-second-window-borderless.mdc`

## 更新

```powershell
C:\Users\k-mizukami\cursor-playbook\scripts\Install-CursorRules.ps1 -ProjectPath "c:\Users\k-mizukami\Desktop\IP_checker\IPChecker"
```

## 参照

- https://github.com/honi0907/cursor-playbook
