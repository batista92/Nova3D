# Nova3D v0.1

Toolkit 3D reutilizável para MonoGame, validado pelo benchmark do CityBuilder.

Inclui PBR, CSM, instancing, LOD, terrain, vegetação, streaming, água, post-FX,
importação glTF/GLB, asset management, shader hot reload, debug e profiling.

## Requisitos

- .NET 8 SDK ou superior;
- backend suportado pelo MonoGame DesktopGL.

## Instalar o template local

```powershell
.\eng\install-template.ps1
```

O instalador empacota `Nova3D` e `Nova3D.Templates`, registra o feed local e
instala o comando `nova3d`.

## Criar um jogo

```powershell
dotnet new nova3d -n MyGame
cd MyGame
dotnet run
```

O projeto gerado abre uma cena 3D com câmera e cubo rotativo e fornece pastas
para modelos, texturas, áudio, shaders, Content e código do jogo.

## Executar o benchmark

```powershell
dotnet tool restore
dotnet run
```

A cidade procedural inicia ativa. Assets opcionais colocados em
`LocalAssets/Models/validation` aparecem na galeria com `F4`; `LocalAssets/`
não é versionado. Consulte [ROADMAP.md](ROADMAP.md) para o histórico dos gates.
