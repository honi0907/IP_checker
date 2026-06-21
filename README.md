# IP Checker

WinUI 3 のデスクトップ常駐アプリ。有効なネットワークアダプタの IPv4 アドレスと DHCP（自動）/ 静的（手動）設定を一覧表示します。

## 機能

- コンパクト常駐ウィンドウ + システムトレイ
- 手動（静的）IP を視覚的に強調表示
- 詳細表示（アダプタ名・ゲートウェイ・DNS など）
- 共有センターへのショートカット
- ゲームコントローラー入力テスト（USB 接続時）

## 要件

- Windows 10 1809 以降（推奨: Windows 11）
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- 開発時: Visual Studio 2022+（WinUI 3 / Windows App SDK）

## ビルド

```powershell
Stop-Process -Name IPChecker -Force -ErrorAction SilentlyContinue
dotnet build "IPChecker\IPChecker.csproj" -p:Platform=x64
```

詳細な運用ルールは [BUILD_RULES.md](BUILD_RULES.md) を参照。

## リリース（インストーラー + ポータブル ZIP）

```powershell
.\scripts\Build-Installer.ps1
```

Inno Setup 6 が必要です。

## 実行

Visual Studio で `IPChecker\IPChecker.csproj` を開き、構成 **Debug | x64**、起動プロファイル **IPChecker (Unpackaged)** で F5。

Unpackaged ビルドの exe は `IPChecker\bin\x64\Debug\net8.0-windows10.0.26100.0\win-x64\` に出力されます（フォルダごと配布）。

## ライセンス

Private project — 利用条件はリポジトリオーナーに確認してください。
