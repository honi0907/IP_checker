# IP Checker — Build Rules

共通運用は [cursor-playbook `docs/COMMON_APP_RULES.md`](https://github.com/honi0907/cursor-playbook/blob/master/docs/COMMON_APP_RULES.md) を参照。  
ここは IP Checker 固有の成果物パスとコマンド。

## ビルド前

```powershell
Stop-Process -Name IPChecker -Force -ErrorAction SilentlyContinue
```

起動中は exe / DLL がロックされビルド・publish が失敗するため、**必ず先に終了**する。

## 開発ビルド

```powershell
dotnet build "IPChecker\IPChecker.csproj" -p:Platform=x64
```

## リリース（インストーラー + ポータブル ZIP）

```powershell
.\scripts\Build-Installer.ps1
```

| 成果物 | 出力先 |
|--------|--------|
| インストーラー | `dist\IPChecker-Setup-{version}-x64.exe` |
| ポータブル ZIP | `dist\IPChecker-{version}-x64-portable.zip` |
| publish フォルダ | `dist\publish\win-x64\` |

- **毎回**インストーラーとポータブル ZIP の両方を生成する（Inno Setup の有無にかかわらず ZIP は必須）。
- `dist` には当該バージョンの成果物のみ残す（旧 `IPChecker-Setup-*.exe` / `*-portable.zip` は削除）。

## バージョン

- `IPChecker.csproj` の `<Version>` / `installer\IPChecker.iss` の `MyAppVersion` を揃える。
- パッチは **0〜9**（例: `1.0.0` … `1.0.9`）。**`1.0.9` の次は `1.1.0`**（`1.0.10` にはしない）。
- リリースのたびに必ず上げ、同じ tag / ファイル名の再利用はしない。
