# Nova3D v0.1

Toolkit 3D reutilizável para MonoGame, validado pelo benchmark do CityBuilder.

Inclui PBR, CSM, instancing, LOD, terrain, vegetação, streaming, água, post-FX,
importação glTF/GLB, asset management, shader hot reload, debug e profiling.

Documentação: [índice por tarefa](Docs/README.md) ·
[primeiro projeto](Docs/getting-started.md) ·
[skill para agentes](Docs/agent-skill.md) ·
[guia para IA](Docs/AI_GUIDE.md) ·
[troubleshooting](Docs/troubleshooting.md)

## Requisitos

- .NET 8 SDK ou superior;
- backend suportado pelo MonoGame DesktopGL.

## Instalar o template local

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\install-template.ps1
```

O `Bypass` vale apenas para esse processo e não altera permanentemente a
política de execução do Windows.

O instalador empacota `Nova3D`, os módulos opcionais e `Nova3D.Templates`,
registra o feed local e instala o comando `nova3d`.

## Criar um jogo

```powershell
dotnet new nova3d -n MyGame
dotnet new nova3d -n MyPhysicsGame --physics
dotnet new nova3d -n MyUiGame --ui
dotnet new nova3d -n MyFullGame --physics --ui
cd MyGame
dotnet run
```

O projeto gerado abre uma cena 3D com câmera e cubo rotativo e fornece pastas
para modelos, texturas, áudio, shaders, Content e código do jogo.

Consulte o [guia de projeto](Docs/game-project-guide.md) para lifecycle,
ownership, áudio, configuração e geração de executável.

## Samples compiláveis

Consulte [Samples/README.md](Samples/README.md) para exemplos independentes de
renderização mínima, física, menus Gum e um vertical slice RollingBall.

O protocolo independente de validação por IA está em
[AI_GATE.md](AI_GATE.md).

## Executar o benchmark

```powershell
dotnet tool restore
dotnet run
```

A cidade procedural inicia ativa. Assets opcionais colocados em
`LocalAssets/Models/validation` aparecem na galeria com `F4`; `LocalAssets/`
não é versionado. Consulte [ROADMAP.md](ROADMAP.md) para o histórico dos gates.
